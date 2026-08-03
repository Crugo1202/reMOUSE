namespace ReMouse.Core.Settings;

public sealed record FlickSettings
{
    public const int MinimumDelta = 1;
    public const int MaximumDelta = 1200;

    public FlickSettings(int delta)
    {
        Delta = delta;
        Validate();
    }

    public int Delta { get; }

    public void Validate()
    {
        if (Delta is < MinimumDelta or > MaximumDelta)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Delta),
                Delta,
                $"Flick delta must be between {MinimumDelta} and {MaximumDelta}.");
        }
    }
}
