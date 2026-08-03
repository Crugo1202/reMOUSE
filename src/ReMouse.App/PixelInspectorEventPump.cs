using ReMouse.Core.Input;

namespace ReMouse.App;

/// <summary>
/// Consumes pixel-inspector UI events on WPF's dispatcher context. Selection
/// completion keeps the inspector visible; only the next XButton1 toggle
/// dismisses it.
/// </summary>
public sealed class PixelInspectorEventPump
{
    private readonly IPixelInspectorOverlayView _view;
    private readonly PixelInspectorInputBridge? _inputBridge;
    private bool _overlayVisible;

    public PixelInspectorEventPump(
        IPixelInspectorOverlayView view,
        PixelInspectorInputBridge? inputBridge = null)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _inputBridge = inputBridge;
    }

    public async Task RunAsync(
        IAsyncEnumerable<PixelInspectorUiEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        try
        {
            await foreach (var uiEvent in events.WithCancellation(cancellationToken).ConfigureAwait(true))
            {
                switch (uiEvent)
                {
                    case PixelInspectorUiEvent.Open open:
                        _overlayVisible = true;
                        _view.Show(open.Snapshot);
                        break;

                    case PixelInspectorUiEvent.Update update:
                        if (!_overlayVisible)
                        {
                            _overlayVisible = true;
                            _view.Show(update.Snapshot);
                        }
                        else
                        {
                            _view.Update(update.Snapshot);
                        }

                        break;

                    case PixelInspectorUiEvent.SelectionCompleted completed:
                        if (!_overlayVisible)
                        {
                            _overlayVisible = true;
                            _view.Show(completed.Snapshot);
                        }
                        else
                        {
                            _view.Update(completed.Snapshot);
                        }

                        break;

                    case PixelInspectorUiEvent.Dismiss:
                        DismissOverlay();
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(uiEvent), uiEvent, "Unknown pixel inspector UI event.");
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
