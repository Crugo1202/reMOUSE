namespace ReMouse.Core.Input;

public abstract record RadialMenuAction
{
    private RadialMenuAction()
    {
    }

    public sealed record NoOp : RadialMenuAction;

    public sealed record LaunchApplication : RadialMenuAction
    {
        public LaunchApplication(string executablePath, string? arguments = null)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new ArgumentException(
                    "An application action needs an executable path.",
                    nameof(executablePath));
            }

            ExecutablePath = executablePath.Trim();
            Arguments = arguments?.Trim() ?? string.Empty;
        }

        public string ExecutablePath { get; }

        public string Arguments { get; }
    }

    public sealed record Shortcut : RadialMenuAction
    {
        public Shortcut(InputEffect.KeySequence sequence)
        {
            Sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
        }

        public InputEffect.KeySequence Sequence { get; }
    }
}
