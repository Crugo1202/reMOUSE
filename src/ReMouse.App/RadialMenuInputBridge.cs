using System.Threading.Channels;
using ReMouse.Core.Input;
using ReMouse.Windows.Hooks;

namespace ReMouse.App;

/// <summary>
/// Adapts synchronous global mouse-hook decisions to asynchronous WPF work.
/// Only the hook thread calls <see cref="Handle"/>; the UI consumes the
/// resulting events through <see cref="ReadAllAsync"/>.
/// </summary>
public sealed class RadialMenuInputBridge : IDisposable
{
    private RadialMenuSession _session;
    private RadialMenuLayout _layout;
    private readonly Func<int, int, ScreenPoint> _coordinateMapper;
    private GlobalMouseButton? _toggleButton;
    private readonly Channel<RadialMenuUiEvent> _events = Channel.CreateUnbounded<RadialMenuUiEvent>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            // Hook thread publishes input events; shutdown/pump may complete
            // the bridge from the UI thread.
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    private readonly object _stateLock = new();
    private int _closed;
    private bool _disabled;

    public RadialMenuInputBridge(
        RadialMenuLayout layout,
        Func<int, int, ScreenPoint>? coordinateMapper = null,
        GlobalMouseButton? toggleButton = GlobalMouseButton.XButton2)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _session = new RadialMenuSession(_layout);
        _coordinateMapper = coordinateMapper ?? ((x, y) => new ScreenPoint(x, y));
        ValidateToggleButton(toggleButton);

        _toggleButton = toggleButton;
    }

    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    public bool IsOpen
    {
        get
        {
            lock (_stateLock)
            {
                return _session.IsOpen;
            }
        }
    }

    internal RadialMenuLayout CurrentLayout
    {
        get
        {
            lock (_stateLock)
            {
                return _layout;
            }
        }
    }

    public void Reconfigure(RadialMenuLayout layout, GlobalMouseButton? toggleButton)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ValidateToggleButton(toggleButton);

        lock (_stateLock)
        {
            if (IsClosed)
            {
                return;
            }

            if (_session.IsOpen)
            {
                _session.Cancel();
                _events.Writer.TryWrite(new RadialMenuUiEvent.Cancel());
            }

            _layout = layout;
            _session = new RadialMenuSession(layout);
            _toggleButton = toggleButton;
        }
    }

    public IAsyncEnumerable<RadialMenuUiEvent> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _events.Reader.ReadAllAsync(cancellationToken);

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
                return HandleMove(_coordinateMapper(input.X, input.Y));
            }

            if (input.Button != _toggleButton)
            {
                return new GlobalMouseDecision(false);
            }

            return input.IsDown
                ? HandleDown(_coordinateMapper(input.X, input.Y))
                : HandleUp(_coordinateMapper(input.X, input.Y));
        }
    }

    /// <summary>
    /// Cancels the current menu without closing the bridge. Used by the
    /// emergency-pause path so Resume can continue using the same hook.
    /// </summary>
    public void Cancel()
    {
        lock (_stateLock)
        {
            if (IsClosed || !_session.IsOpen)
            {
                return;
            }

            _session.Cancel();
            _events.Writer.TryWrite(new RadialMenuUiEvent.Cancel());
        }
    }

    public void Complete()
    {
        lock (_stateLock)
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0)
            {
                if (_session.IsOpen)
                {
                    _events.Writer.TryWrite(new RadialMenuUiEvent.Cancel());
                }

                _events.Writer.TryComplete();
            }

            if (_session.IsOpen)
            {
                _session.Cancel();
            }
        }
    }

    public void Dispose() => Complete();

    private GlobalMouseDecision HandleDown(ScreenPoint center)
    {
        if (_session.IsOpen)
        {
            return new GlobalMouseDecision(true);
        }

        _session.Open(center);
        if (!TryPublish(new RadialMenuUiEvent.Open(center)))
        {
            _session.Cancel();
            return new GlobalMouseDecision(false);
        }

        return new GlobalMouseDecision(true);
    }

    private GlobalMouseDecision HandleMove(ScreenPoint cursor)
    {
        if (!_session.IsOpen)
        {
            return new GlobalMouseDecision(false);
        }

        var hit = _session.Update(cursor);
        if (!TryPublish(new RadialMenuUiEvent.Preview(hit)))
        {
            _session.Cancel();
        }

        return new GlobalMouseDecision(false);
    }

    private GlobalMouseDecision HandleUp(ScreenPoint cursor)
    {
        if (!_session.IsOpen)
        {
            return new GlobalMouseDecision(false);
        }

        var selected = _session.Commit(cursor);
        if (!TryPublish(new RadialMenuUiEvent.Commit(selected)))
        {
            return new GlobalMouseDecision(false);
        }

        return new GlobalMouseDecision(true);
    }

    private bool TryPublish(RadialMenuUiEvent uiEvent)
    {
        if (_events.Writer.TryWrite(uiEvent))
        {
            return true;
        }

        _disabled = true;
        Interlocked.Exchange(ref _closed, 1);
        _events.Writer.TryComplete();
        return false;
    }

    private static void ValidateToggleButton(GlobalMouseButton? toggleButton)
    {
        if (toggleButton is not null &&
            toggleButton is not GlobalMouseButton.XButton1 and not GlobalMouseButton.XButton2)
        {
            throw new ArgumentOutOfRangeException(nameof(toggleButton), toggleButton, "Radial menu must use an XButton.");
        }
    }
}
