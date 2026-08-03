namespace ReMouse.Core.Input;

/// <summary>
/// Interprets a held middle button plus a left/right click as a horizontal
/// flick. A plain middle click remains a middle click, while all events that
/// participate in a chord are marked for suppression by the future hook host.
/// This class is deliberately platform-independent and must be called in input
/// order from one serialized event consumer.
/// </summary>
public sealed class MiddleChordGesture
{
    public const int DefaultDragThreshold = 8;
    private int _horizontalWheelDelta;
    private readonly int _dragThreshold;
    private readonly HashSet<MouseButtonId> _heldButtons = new();
    private readonly HashSet<MouseButtonId> _suppressedButtons = new();
    private bool _middleHeld;
    private bool _chordUsed;
    private bool _dragPassthrough;
    private bool _dragPending;
    private long _dragSequence;
    private int _middleStartX;
    private int _middleStartY;

    public MiddleChordGesture(
        int horizontalWheelDelta = 120,
        int dragThreshold = DefaultDragThreshold)
    {
        if (horizontalWheelDelta <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(horizontalWheelDelta),
                "The flick delta must be a positive non-zero value.");
        }

        if (dragThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dragThreshold),
                "The middle-drag threshold must be positive and non-zero.");
        }

        _horizontalWheelDelta = horizontalWheelDelta;
        _dragThreshold = dragThreshold;
    }

    public int HorizontalWheelDelta => _horizontalWheelDelta;

    public int DragThreshold => _dragThreshold;

    /// <summary>
    /// Applies a new flick delta without replacing the state machine. Keeping
    /// the same instance preserves the monotonic drag sequence so an ordering
    /// marker that is still queued from the previous configuration can never
    /// unlock a newly-started drag.
    /// </summary>
    public InputHandlingDecision Reconfigure(int horizontalWheelDelta)
    {
        if (horizontalWheelDelta <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(horizontalWheelDelta),
                "The flick delta must be a positive non-zero value.");
        }

        var release = Cancel();
        _horizontalWheelDelta = horizontalWheelDelta;
        return release;
    }

    /// <summary>
    /// Aborts any in-flight chord when another modal input mode takes over.
    /// The original events that were already suppressed are intentionally not
    /// replayed; the owning hook has decided that the modal mode owns the
    /// crossing-boundary input sequence.
    /// </summary>
    public InputHandlingDecision Cancel()
    {
        var release = _dragPassthrough
            ? InputHandlingDecision.Suppress(new InputEffect.MiddleButtonUp())
            : InputHandlingDecision.PassThrough();

        _heldButtons.Clear();
        _suppressedButtons.Clear();
        _middleHeld = false;
        _chordUsed = false;
        _dragPassthrough = false;
        _dragPending = false;
        _dragSequence++;
        _middleStartX = 0;
        _middleStartY = 0;

        return release;
    }

    public InputHandlingDecision Handle(MouseButtonEvent input)
    {
        return input.IsDown
            ? HandleDown(input)
            : HandleUp(input.Button);
    }

    public InputHandlingDecision HandleMove(MouseMoveEvent input)
    {
        if (!_middleHeld || _chordUsed)
        {
            return InputHandlingDecision.PassThrough();
        }

        if (_dragPassthrough)
        {
            if (!_dragPending)
            {
                return InputHandlingDecision.PassThrough();
            }

            // A move can arrive after the first marker has been queued but
            // before the pump consumes it. Extend the barrier with a newer
            // token so that the old marker cannot unlock native movement ahead
            // of this replay move.
            var pendingSequenceId = ++_dragSequence;
            return InputHandlingDecision.Suppress(
                new InputEffect.MouseMove(input.X, input.Y),
                new InputEffect.MiddleDragReady(pendingSequenceId));
        }

        var deltaX = (long)input.X - _middleStartX;
        var deltaY = (long)input.Y - _middleStartY;
        // Coordinates come from a signed 32-bit Windows screen position. The
        // difference between two virtual-screen points can exceed Int32 and
        // its square can exceed Int64, so use double for the distance check to
        // avoid a wraparound turning a very large move into a small one.
        var thresholdSquared = (double)_dragThreshold * _dragThreshold;
        var distanceSquared =
            ((double)deltaX * deltaX) +
            ((double)deltaY * deltaY);
        if (distanceSquared < thresholdSquared)
        {
            return InputHandlingDecision.PassThrough();
        }

        // The physical middle Down was held back while deciding whether this
        // is a click/chord. Once it is clearly a drag, replay only that Down;
        // the current and subsequent movement events remain native.
        _dragPassthrough = true;
        _dragPending = true;
        var sequenceId = ++_dragSequence;
        return InputHandlingDecision.Suppress(
            new InputEffect.MiddleButtonDown(),
            new InputEffect.MouseMove(input.X, input.Y),
            new InputEffect.MiddleDragReady(sequenceId));
    }

    /// <summary>
    /// Called by the effect consumer after the synthetic Down and all queued
    /// replay moves preceding the ordering marker have been delivered.
    /// </summary>
    public void MarkDragReady(long sequenceId)
    {
        if (_dragPassthrough && _dragPending && sequenceId == _dragSequence)
        {
            _dragPending = false;
        }
    }

    private InputHandlingDecision HandleDown(MouseButtonEvent input)
    {
        var button = input.Button;
        if (_heldButtons.Contains(button))
        {
            // A duplicate down must never create a second flick or a second
            // synthetic middle click. The first middle down is always held
            // back while we wait to learn whether it becomes a chord, so a
            // duplicate middle down must remain suppressed as well.
            return button == MouseButtonId.Middle || _suppressedButtons.Contains(button)
                ? InputHandlingDecision.Suppress()
                : InputHandlingDecision.PassThrough();
        }

        _heldButtons.Add(button);

        if (button == MouseButtonId.Middle)
        {
            _middleHeld = true;
            _chordUsed = false;
            _dragPassthrough = false;
            _dragPending = false;
            _middleStartX = input.X;
            _middleStartY = input.Y;
            return InputHandlingDecision.Suppress();
        }

        if (!_middleHeld || _dragPassthrough)
        {
            return InputHandlingDecision.PassThrough();
        }

        _suppressedButtons.Add(button);
        _chordUsed = true;

        var delta = button == MouseButtonId.Left
            ? -_horizontalWheelDelta
            : _horizontalWheelDelta;

        return InputHandlingDecision.Suppress(new InputEffect.HorizontalWheel(delta));
    }

    private InputHandlingDecision HandleUp(MouseButtonId button)
    {
        if (!_heldButtons.Remove(button))
        {
            // Preserve an orphan release unless it belongs to a button that
            // was already consumed by a chord and is being recovered now.
            return _suppressedButtons.Remove(button)
                ? InputHandlingDecision.Suppress()
                : InputHandlingDecision.PassThrough();
        }

        if (button == MouseButtonId.Middle)
        {
            _middleHeld = false;
            if (_dragPassthrough)
            {
                _dragPassthrough = false;
                _dragPending = false;
                _chordUsed = false;
                return InputHandlingDecision.Suppress(new InputEffect.MiddleButtonUp());
            }

            var wasChordUsed = _chordUsed;
            _chordUsed = false;

            return wasChordUsed
                ? InputHandlingDecision.Suppress()
                : InputHandlingDecision.Suppress(new InputEffect.MiddleClick());
        }

        return _suppressedButtons.Remove(button)
            ? InputHandlingDecision.Suppress()
            : InputHandlingDecision.PassThrough();
    }
}
