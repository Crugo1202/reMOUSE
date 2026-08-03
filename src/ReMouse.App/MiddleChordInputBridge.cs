using System.Threading.Channels;
using ReMouse.Core.Input;
using ReMouse.Windows.Hooks;

namespace ReMouse.App;

/// <summary>
/// Runs the platform-neutral middle-chord state machine on the low-level hook
/// thread and queues only effects. The potentially blocking SendInput call is
/// deliberately left to <see cref="MiddleChordEffectPump"/>.
/// </summary>
public sealed class MiddleChordInputBridge : IDisposable
{
    private MiddleChordGesture _gesture;
    private readonly Channel<InputEffect> _effects = Channel.CreateUnbounded<InputEffect>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false
        });
    private readonly object _stateLock = new();
    private int _closed;
    private bool _disabled;

    public MiddleChordInputBridge(int horizontalWheelDelta = 120)
    {
        _gesture = new MiddleChordGesture(horizontalWheelDelta);
    }

    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    public int HorizontalWheelDelta
    {
        get
        {
            lock (_stateLock)
            {
                return _gesture.HorizontalWheelDelta;
            }
        }
    }

    /// <summary>
    /// Applies flick settings without restarting the global hook. Any
    /// in-flight chord is cancelled first so no old-delta effect can appear
    /// after the new configuration becomes active. The existing gesture is
    /// retained so queued ordering markers remain tied to their old sequence.
    /// </summary>
    public void Reconfigure(int horizontalWheelDelta)
    {
        lock (_stateLock)
        {
            if (IsClosed)
            {
                return;
            }

            PublishDecision(_gesture.Reconfigure(horizontalWheelDelta));
        }
    }

    public void Cancel()
    {
        lock (_stateLock)
        {
            if (!IsClosed)
            {
                PublishDecision(_gesture.Cancel());
            }
        }
    }

    internal void MarkDragReady(long sequenceId)
    {
        lock (_stateLock)
        {
            _gesture.MarkDragReady(sequenceId);
        }
    }

    public IAsyncEnumerable<InputEffect> ReadEffectsAsync(
        CancellationToken cancellationToken = default) =>
        _effects.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Exposes the single-consumer reader used by the effect pump. The pump
    /// can drain already-buffered replay moves without waiting, keeping a
    /// high-frequency drag from accumulating stale coordinates while the hook
    /// thread remains bounded and non-blocking.
    /// </summary>
    public ChannelReader<InputEffect> EffectReader => _effects.Reader;

    public GlobalMouseDecision Handle(GlobalMouseEvent input)
    {
        lock (_stateLock)
        {
            if (_disabled || IsClosed || input.IsInjected)
            {
                return new GlobalMouseDecision(false);
            }

            if (input.Kind == GlobalMouseEventKind.Move)
            {
                return PublishDecision(_gesture.HandleMove(new MouseMoveEvent(input.X, input.Y)));
            }

            if (input.Kind != GlobalMouseEventKind.Button)
            {
                return new GlobalMouseDecision(false);
            }

            var button = input.Button switch
            {
                GlobalMouseButton.Left => MouseButtonId.Left,
                GlobalMouseButton.Right => MouseButtonId.Right,
                GlobalMouseButton.Middle => MouseButtonId.Middle,
                _ => (MouseButtonId?)null
            };
            if (button is not { } mouseButton)
            {
                return new GlobalMouseDecision(false);
            }

            var decision = _gesture.Handle(new MouseButtonEvent(mouseButton, input.IsDown, input.X, input.Y));
            return PublishDecision(decision);
        }
    }

    private GlobalMouseDecision PublishDecision(InputHandlingDecision decision)
    {
        foreach (var effect in decision.Effects)
        {
            if (_effects.Writer.TryWrite(effect))
            {
                continue;
            }

            // Do not swallow the original button when the effect consumer has
            // stopped. The next callback is fail-open as well.
            _disabled = true;
            return new GlobalMouseDecision(false);
        }

        return new GlobalMouseDecision(decision.SuppressOriginal);
    }

    public void Complete()
    {
        lock (_stateLock)
        {
            if (Volatile.Read(ref _closed) != 0)
            {
                return;
            }

            // If a drag boundary was queued or delivered, append its matching
            // Up before closing the channel. This keeps shutdown ordered with
            // the synthetic Down and does not touch the hook thread.
            PublishDecision(_gesture.Cancel());
            if (Interlocked.Exchange(ref _closed, 1) == 0)
            {
                _effects.Writer.TryComplete();
            }
        }
    }

    /// <summary>
    /// Used by the effect pump after a sink fault/cancellation. The pump has
    /// already performed its best-effort release, so reset state without
    /// appending another Up to a channel that may already be closed.
    /// </summary>
    internal void CompleteAfterPump()
    {
        lock (_stateLock)
        {
            _gesture.Cancel();
            if (Interlocked.Exchange(ref _closed, 1) == 0)
            {
                _effects.Writer.TryComplete();
            }
        }
    }

    public void Dispose() => Complete();
}
