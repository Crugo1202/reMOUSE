namespace ReMouse.Core.Input;

public sealed record RadialMenuItem
{
    public RadialMenuItem(string id, string label)
        : this(id, label, new RadialMenuAction.NoOp())
    {
    }

    public RadialMenuItem(string id, string label, RadialMenuAction action)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A radial menu item needs a non-empty id.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("A radial menu item needs a non-empty label.", nameof(label));
        }

        ArgumentNullException.ThrowIfNull(action);

        Id = id.Trim();
        Label = label.Trim();
        Action = action;
    }

    public string Id { get; }

    public string Label { get; }

    public RadialMenuAction Action { get; }
}
