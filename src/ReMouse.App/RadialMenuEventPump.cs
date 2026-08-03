using ReMouse.Core.Input;

namespace ReMouse.App;

public sealed class RadialMenuEventPump
{
    private readonly RadialMenuLayout _layout;
    private readonly IRadialMenuOverlayView _view;
    private readonly IRadialMenuActionExecutor _actionExecutor;
    private readonly RadialMenuInputBridge? _inputBridge;
    private bool _overlayVisible;

    public RadialMenuEventPump(
        RadialMenuLayout layout,
        IRadialMenuOverlayView view,
        IRadialMenuActionExecutor actionExecutor,
        RadialMenuInputBridge? inputBridge = null)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
        _inputBridge = inputBridge;
    }

    public async Task RunAsync(
        IAsyncEnumerable<RadialMenuUiEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        try
        {
            await foreach (var uiEvent in events.WithCancellation(cancellationToken).ConfigureAwait(true))
            {
                switch (uiEvent)
                {
                    case RadialMenuUiEvent.Open open:
                        _overlayVisible = true;
                        _view.Show(_inputBridge?.CurrentLayout ?? _layout, open.Center);
                        break;

                    case RadialMenuUiEvent.Preview preview:
                        _view.Update(preview.Hit);
                        break;

                    case RadialMenuUiEvent.Commit commit:
                        DismissOverlay();
                        if (commit.Item is not null)
                        {
                            await _actionExecutor.ExecuteAsync(commit.Item.Action, cancellationToken)
                                .ConfigureAwait(true);
                        }

                        break;

                    case RadialMenuUiEvent.Cancel:
                        DismissOverlay();
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(uiEvent), uiEvent, "Unknown radial UI event.");
                }
            }
        }
        finally
        {
            try
            {
                DismissOverlay();
            }
            finally
            {
                _inputBridge?.Complete();
            }
        }
    }

    private void DismissOverlay()
    {
        if (_overlayVisible)
        {
            _view.Dismiss();
            _overlayVisible = false;
        }
    }

}
