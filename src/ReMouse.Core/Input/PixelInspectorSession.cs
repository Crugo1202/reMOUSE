namespace ReMouse.Core.Input;

/// <summary>
/// State machine for the lower-side-button pixel inspector. It keeps raw pixel
/// coordinates, suppresses only the side/left events it consumes, and leaves
/// ordinary movement untouched.
/// </summary>
public sealed class PixelInspectorSession
{
    private bool _active;
    private bool _selecting;
    // Keep physical left-button pairing separate from the inspector's
    // selection state. This lets us fail-open for a Down that happened before
    // the mode was enabled, while still suppressing the matching Up for a
    // Down that we did consume if the mode is closed mid-drag.
    private bool _leftButtonDown;
    private bool _leftDownConsumed;
    private PixelPoint _cursor;
    private PixelPoint? _selectionStart;
    private PixelRectangle? _selection;

    public bool IsActive => _active;

    public PixelInspectorSnapshot Snapshot =>
        new(_active, _cursor, _selectionStart, _selection);

    /// <summary>
    /// Cancels the visual inspector state without changing the physical
    /// left-button pairing flags. A consumed Down may still be waiting for its
    /// matching Up when the mode is closed.
    /// </summary>
    public void Cancel()
    {
        _active = false;
        _selecting = false;
        _selectionStart = null;
        _selection = null;
    }

    /// <summary>
    /// Emergency-pause reset. Unlike a normal mode close, this also forgets
    /// physical button pairing because the paused hook intentionally stops
    /// observing mouse releases until Resume.
    /// </summary>
    public void EmergencyCancel()
    {
        Cancel();
        _leftButtonDown = false;
        _leftDownConsumed = false;
    }

    public PixelInspectorDecision Handle(PixelInspectorInput input)
    {
        return input.Kind switch
        {
            PixelInspectorInputKind.Toggle => HandleToggle(input.Point),
            PixelInspectorInputKind.Move => HandleMove(input.Point, input.Modifiers),
            PixelInspectorInputKind.LeftButton => HandleLeftButton(input.IsDown, input.Point, input.Modifiers),
            _ => throw new ArgumentOutOfRangeException(nameof(input), input, "Unknown pixel inspector input kind.")
        };
    }

    private PixelInspectorDecision HandleToggle(PixelPoint point)
    {
        _cursor = point;
        if (_active)
        {
            _active = false;
            _selecting = false;
            _selectionStart = null;
            _selection = null;
            return CreateDecision(suppressOriginal: true, modeChanged: true, selectionCompleted: false);
        }

        _active = true;
        _selecting = false;
        _selectionStart = null;
        _selection = null;
        return CreateDecision(suppressOriginal: true, modeChanged: true, selectionCompleted: false);
    }

    private PixelInspectorDecision HandleMove(
        PixelPoint point,
        PixelInspectorModifiers modifiers)
    {
        _cursor = point;
        if (_active && _selecting && _selectionStart is { } start)
        {
            _selection = PixelRectangle.FromCorners(
                start,
                ConstrainSelectionPoint(start, point, modifiers));
        }

        return CreateDecision(suppressOriginal: false, modeChanged: false, selectionCompleted: false);
    }

    private PixelInspectorDecision HandleLeftButton(
        bool isDown,
        PixelPoint point,
        PixelInspectorModifiers modifiers)
    {
        _cursor = point;
        if (isDown)
        {
            if (_leftButtonDown)
            {
                // A repeated Down is part of the same physical press. Never
                // move the drag anchor or change whether its original Down
                // was forwarded to the underlying application.
                return CreateDecision(
                    suppressOriginal: _leftDownConsumed,
                    modeChanged: false,
                    selectionCompleted: false);
            }

            _leftButtonDown = true;
            _leftDownConsumed = _active;
            if (!_active)
            {
                return CreateDecision(suppressOriginal: false, modeChanged: false, selectionCompleted: false);
            }

            _selecting = true;
            _selectionStart = point;
            _selection = PixelRectangle.FromCorners(point, point);
            return CreateDecision(suppressOriginal: true, modeChanged: false, selectionCompleted: false);
        }

        // A button-up is paired with the physical Down, not with the current
        // mode. If no Down was observed, or that Down was passed through,
        // fail-open so an application never loses its release event.
        var suppressOriginal = _leftButtonDown && _leftDownConsumed;
        _leftButtonDown = false;
        _leftDownConsumed = false;
        if (!suppressOriginal)
        {
            return CreateDecision(suppressOriginal: false, modeChanged: false, selectionCompleted: false);
        }

        if (!_active || !_selecting || _selectionStart is not { } start)
        {
            return CreateDecision(suppressOriginal: true, modeChanged: false, selectionCompleted: false);
        }

        _selecting = false;
        _selection = PixelRectangle.FromCorners(
            start,
            ConstrainSelectionPoint(start, point, modifiers));
        return CreateDecision(suppressOriginal: true, modeChanged: false, selectionCompleted: true);
    }

    private static PixelPoint ConstrainSelectionPoint(
        PixelPoint start,
        PixelPoint point,
        PixelInspectorModifiers modifiers)
    {
        if ((modifiers & PixelInspectorModifiers.Shift) == 0)
        {
            return point;
        }

        var deltaX = (double)point.X - start.X;
        var deltaY = (double)point.Y - start.Y;
        var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= double.Epsilon)
        {
            return start;
        }

        var angle = Math.Atan2(deltaY, deltaX);
        var snappedAngle = Math.Round(angle / (Math.PI / 4), MidpointRounding.AwayFromZero) * (Math.PI / 4);
        var constrainedX = start.X + (Math.Cos(snappedAngle) * distance);
        var constrainedY = start.Y + (Math.Sin(snappedAngle) * distance);
        return new PixelPoint(ClampToInt(constrainedX), ClampToInt(constrainedY));
    }

    private static int ClampToInt(double value)
    {
        if (value <= int.MinValue)
        {
            return int.MinValue;
        }

        if (value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private PixelInspectorDecision CreateDecision(
        bool suppressOriginal,
        bool modeChanged,
        bool selectionCompleted)
    {
        return new PixelInspectorDecision(
            suppressOriginal,
            new PixelInspectorSnapshot(_active, _cursor, _selectionStart, _selection),
            modeChanged,
            selectionCompleted);
    }
}
