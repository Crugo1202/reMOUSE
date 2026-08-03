using ReMouse.Core.Input;

namespace ReMouse.App;

public sealed class RadialMenuOverlayController
{
    private readonly RadialMenuLayout _layout;
    private readonly RadialMenuSession _session;
    private readonly IRadialMenuOverlayView _view;
    private readonly IRadialMenuActionExecutor _actionExecutor;

    public RadialMenuOverlayController(
        RadialMenuLayout layout,
        IRadialMenuOverlayView view,
        IRadialMenuActionExecutor actionExecutor)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _session = new RadialMenuSession(layout);
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
    }

    public bool IsOpen => _session.IsOpen;

    public void Begin(ScreenPoint center)
    {
        _session.Open(center);
        try
        {
            _view.Show(_layout, center);
        }
        catch
        {
            _session.Cancel();
            throw;
        }
    }

    public RadialMenuHit Update(ScreenPoint cursor)
    {
        var hit = _session.Update(cursor);
        _view.Update(hit);
        return hit;
    }

    public async ValueTask<RadialMenuItem?> ReleaseAsync(
        ScreenPoint cursor,
        CancellationToken cancellationToken = default)
    {
        RadialMenuItem? selected;
        try
        {
            selected = _session.Commit(cursor);
        }
        finally
        {
            _view.Dismiss();
        }

        if (selected is not null)
        {
            await _actionExecutor.ExecuteAsync(selected.Action, cancellationToken).ConfigureAwait(false);
        }

        return selected;
    }

    public void Cancel()
    {
        if (!_session.IsOpen)
        {
            return;
        }

        _session.Cancel();
        _view.Dismiss();
    }
}
