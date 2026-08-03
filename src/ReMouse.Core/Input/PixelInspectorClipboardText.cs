using System.Globalization;

namespace ReMouse.Core.Input;

public static class PixelInspectorClipboardText
{
    public static string Format(PixelInspectorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var builder = new System.Text.StringBuilder();
        builder.Append("Cursor X=")
            .Append(snapshot.Cursor.X.ToString(CultureInfo.InvariantCulture))
            .Append(" Y=")
            .Append(snapshot.Cursor.Y.ToString(CultureInfo.InvariantCulture));

        if (snapshot.Selection is not { } selection)
        {
            return builder.ToString();
        }

        builder.AppendLine()
            .Append("TL (").Append(selection.TopLeft.X.ToString(CultureInfo.InvariantCulture))
            .Append(", ").Append(selection.TopLeft.Y.ToString(CultureInfo.InvariantCulture)).Append(")")
            .AppendLine()
            .Append("TR (").Append(selection.TopRight.X.ToString(CultureInfo.InvariantCulture))
            .Append(", ").Append(selection.TopRight.Y.ToString(CultureInfo.InvariantCulture)).Append(")")
            .AppendLine()
            .Append("BL (").Append(selection.BottomLeft.X.ToString(CultureInfo.InvariantCulture))
            .Append(", ").Append(selection.BottomLeft.Y.ToString(CultureInfo.InvariantCulture)).Append(")")
            .AppendLine()
            .Append("BR (").Append(selection.BottomRight.X.ToString(CultureInfo.InvariantCulture))
            .Append(", ").Append(selection.BottomRight.Y.ToString(CultureInfo.InvariantCulture)).Append(")")
            .AppendLine()
            .Append("W ").Append(selection.Width.ToString(CultureInfo.InvariantCulture))
            .Append(" H ").Append(selection.Height.ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}
