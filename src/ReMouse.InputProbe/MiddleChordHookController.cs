using ReMouse.Core.Input;

namespace ReMouse.InputProbe;

/// <summary>
/// Runs the pure middle-chord state machine on the hook thread and queues only
/// the resulting effects. It never calls Win32, writes output, or blocks.
/// </summary>
internal sealed class MiddleChordHookController
{
    private readonly MiddleChordGesture _gesture;
    private readonly ProbeEventChannel _events;
    private readonly object _stateLock = new();
    private bool _disabled;

    public MiddleChordHookController(ProbeEventChannel events, int horizontalWheelDelta)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _gesture = new MiddleChordGesture(horizontalWheelDelta);
    }

    public InputHandlingDecision Handle(MouseButtonEvent input)
    {
        lock (_stateLock)
        {
            if (_disabled || _events.IsClosed)
            {
                return InputHandlingDecision.PassThrough();
            }

            var decision = _gesture.Handle(input);
            return QueueDecision(decision);
        }
    }

    public InputHandlingDecision HandleMove(MouseMoveEvent input)
    {
        lock (_stateLock)
        {
            if (_disabled || _events.IsClosed)
            {
                return InputHandlingDecision.PassThrough();
            }

            return QueueDecision(_gesture.HandleMove(input));
        }
    }

    public void Cancel()
    {
        lock (_stateLock)
        {
            if (_disabled || _events.IsClosed)
            {
                return;
            }

            QueueDecision(_gesture.Cancel());
        }
    }

    public void MarkDragReady(long sequenceId)
    {
        lock (_stateLock)
        {
            _gesture.MarkDragReady(sequenceId);
        }
    }

    private InputHandlingDecision QueueDecision(InputHandlingDecision decision)
    {
        foreach (var effect in decision.Effects)
        {
            if (_events.TryWrite(effect))
            {
                continue;
            }

            _disabled = true;
            return InputHandlingDecision.PassThrough();
        }

        return decision;
    }
}
