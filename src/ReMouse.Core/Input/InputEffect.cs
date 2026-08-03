namespace ReMouse.Core.Input;

public abstract record InputEffect
{
    private InputEffect()
    {
    }

    public sealed record HorizontalWheel : InputEffect
    {
        public HorizontalWheel(int delta)
        {
            if (delta == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delta), "Wheel delta cannot be zero.");
            }

            Delta = delta;
        }

        public int Delta { get; }
    }

    public sealed record MiddleClick : InputEffect;

    public sealed record MiddleButtonDown : InputEffect;

    public sealed record MiddleButtonUp : InputEffect;

    /// <summary>
    /// Replays a physical cursor position while the synthetic middle-button
    /// Down is being ordered through the asynchronous effect pump. This keeps
    /// the first drag movement behind the synthetic Down without doing any
    /// SendInput work on the low-level hook thread.
    /// </summary>
    public sealed record MouseMove(int X, int Y) : InputEffect;

    /// <summary>
    /// Internal ordering marker. The effect pump consumes this marker after
    /// all preceding Down/Move effects have been delivered, then acknowledges
    /// the drag boundary so subsequent physical moves may pass through.
    /// </summary>
    public sealed record MiddleDragReady : InputEffect
    {
        public MiddleDragReady(long sequenceId)
        {
            if (sequenceId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequenceId));
            }

            SequenceId = sequenceId;
        }

        public long SequenceId { get; }
    }

    public sealed record KeySequence : InputEffect
    {
        public KeySequence(IReadOnlyList<InputKeyStroke> strokes)
        {
            if (strokes is null)
            {
                throw new ArgumentNullException(nameof(strokes));
            }

            if (strokes.Count == 0)
            {
                throw new ArgumentException("A key sequence must contain at least one stroke.", nameof(strokes));
            }

            var snapshot = strokes.ToArray();
            var heldKeys = new HashSet<ushort>();

            foreach (var stroke in snapshot)
            {
                if (stroke.IsDown)
                {
                    if (!heldKeys.Add(stroke.VirtualKey))
                    {
                        throw new ArgumentException(
                            $"Virtual key 0x{stroke.VirtualKey:X2} has more than one Down without an Up.",
                            nameof(strokes));
                    }
                }
                else if (!heldKeys.Remove(stroke.VirtualKey))
                {
                    throw new ArgumentException(
                        $"Virtual key 0x{stroke.VirtualKey:X2} has an Up without a matching Down.",
                        nameof(strokes));
                }
            }

            if (heldKeys.Count != 0)
            {
                throw new ArgumentException(
                    "Every key Down must have a matching Up in the same sequence.",
                    nameof(strokes));
            }

            Strokes = snapshot;
        }

        public IReadOnlyList<InputKeyStroke> Strokes { get; }
    }
}
