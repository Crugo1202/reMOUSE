using System.Threading.Channels;
using ReMouse.Core.Input;
using ReMouse.Windows.Hooks;
using ReMouse.Windows.Input;

namespace ReMouse.App;

/// <summary>
/// Converts synchronous WH_MOUSE_LL events into a bounded Core decision plus
/// asynchronous UI updates. XButton1 is the lower Terra side button and is
/// the only button that toggles this mode.
/// </summary>
public sealed class PixelInspectorInputBridge : IDisposable
{
    private readonly PixelInspectorSession _session = new();
    private readonly Func<bool> _isShiftDown;
    private GlobalMouseButton? _toggleButton;
    private readonly Channel<PixelInspectorUiEvent> _events = Channel.CreateUnbounded<PixelInspectorUiEvent>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            // Hook thread publishes; shutdown/UI may complete the bridge.
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    private readonly object _stateLock = new();
    private int _closed;
    private bool _disabled;
    private bool _toggleButtonDown;

    public PixelInspectorInputBridge(
        GlobalMouseButton? toggleButton = GlobalMouseButton.XButton1,
        Func<bool>? isShiftDown = null)
    {
        ValidateToggleButton(toggleButton);

        _toggleButton = toggleButton;
        _isShiftDown = isShiftDown ?? WindowsModifierState.IsShiftDown;
    }

    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    public bool IsActive
    {
        get
        {
            lock (_stateLock)
            {
                return _session.IsActive;
            }
        }
    }

    public bool TryGetClipboardText(out string text)
    {
        lock (_stateLock)
        {
            if (IsClosed || !_session.IsActive)
            {
                text = string.Empty;
                return false;
            }

            text = PixelInspectorClipboardText.Format(_session.Snapshot);
            return true;
        }
    }

    public void Reconfigure(GlobalMouseButton? toggleButton)
    {
        ValidateToggleButton(toggleButton);
        lock (_stateLock)
        {
            if (IsClosed)
            {
                return;
            }

            var wasActive = _session.IsActive;
            _session.EmergencyCancel();
            _toggleButtonDown = false;
            if (wasActive)
            {
                _events.Writer.TryWrite(new PixelInspectorUiEvent.Dismiss());
            }

            _toggleButton = toggleButton;
        }
    }

    public IAsyncEnumerable<PixelInspectorUiEvent> ReadAllAsync(
        CancellationToken cancellationToken = default) =>
        _events.Reader.ReadAllAsync(cancellationToken);

    public GlobalMouseDecision Handle(GlobalMouseEvent input)
    {
        lock (_stateLock)
        {
            if (_disabled || IsClosed || input.IsInjected)
            {
                return new GlobalMouseDecision(false);
            }

            if (input.Kind == GlobalMouseEventKind.Move)
            {
                return HandleMove(new PixelPoint(input.X, input.Y));
            }

            if (input.Button == _toggleButton)
            {
                return input.IsDown
                    ? HandleToggleDown(new PixelPoint(input.X, input.Y))
                    : HandleToggleUp();
            }

            if (input.Button == GlobalMouseButton.Left)
            {
                return HandleLeftButton(input.IsDown, new PixelPoint(input.X, input.Y));
            }

            return new GlobalMouseDecision(false);
        }
    }

    /// <summary>
    /// Cancels the active inspection without closing the bridge. This is used
    /// by the emergency-pause path and leaves the feature available on Resume.
    /// </summary>
    public void Cancel()
    {
        lock (_stateLock)
        {
            if (IsClosed)
            {
                return;
            }

            var wasActive = _session.IsActive;
            _toggleButtonDown = false;
            _session.EmergencyCancel();
            if (wasActive)
            {
                _events.Writer.TryWrite(new PixelInspectorUiEvent.Dismiss());
            }
        }
    }

    public void Complete()
    {
        lock (_stateLock)
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0)
            {
                if (_session.IsActive)
                {
                    _events.Writer.TryWrite(new PixelInspectorUiEvent.Dismiss());
                }

                _events.Writer.TryComplete();
            }

            _session.Cancel();
            _toggleButtonDown = false;
        }
    }

    public void Dispose() => Complete();

    private static void ValidateToggleButton(GlobalMouseButton? toggleButton)
    {
        if (toggleButton is not null &&
            toggleButton is not GlobalMouseButton.XButton1 and not GlobalMouseButton.XButton2)
        {
            throw new ArgumentOutOfRangeException(nameof(toggleButton), toggleButton, "Pixel inspector must use an XButton.");
        }
    }

    private GlobalMouseDecision HandleToggleDown(PixelPoint point)
    {
        if (_toggleButtonDown)
        {
            // Keep the physical side-button pair suppressed; never toggle a
            // second time for a duplicate Down.
            return new GlobalMouseDecision(true);
        }

        _toggleButtonDown = true;
        var decision = _session.Handle(PixelInspectorInput.Toggle(point));
        var published = decision.Snapshot.IsActive
            ? TryPublish(new PixelInspectorUiEvent.Open(decision.Snapshot))
            : TryPublish(new PixelInspectorUiEvent.Dismiss());

        if (!published)
        {
            _session.Cancel();
            _toggleButtonDown = false;
            return new GlobalMouseDecision(false);
        }

        return new GlobalMouseDecision(decision.SuppressOriginal);
    }

    private GlobalMouseDecision HandleToggleUp()
    {
        if (!_toggleButtonDown)
        {
            return new GlobalMouseDecision(false);
        }

        _toggleButtonDown = false;
        return new GlobalMouseDecision(true);
    }

    private GlobalMouseDecision HandleMove(PixelPoint point)
    {
        var modifiers = _session.IsActive && _isShiftDown()
            ? PixelInspectorModifiers.Shift
            : PixelInspectorModifiers.None;
        var decision = _session.Handle(PixelInspectorInput.Move(point, modifiers));
        if (decision.Snapshot.IsActive && !TryPublish(new PixelInspectorUiEvent.Update(decision.Snapshot)))
        {
            _session.Cancel();
        }

        // Cursor movement remains native; only the selection/button events are
        // consumed by the inspector.
        return new GlobalMouseDecision(decision.SuppressOriginal);
    }

    private GlobalMouseDecision HandleLeftButton(bool isDown, PixelPoint point)
    {
        var modifiers = _session.IsActive && _isShiftDown()
            ? PixelInspectorModifiers.Shift
            : PixelInspectorModifiers.None;
        var input = isDown
            ? PixelInspectorInput.LeftDown(point, modifiers)
            : PixelInspectorInput.LeftUp(point, modifiers);
        var decision = _session.Handle(input);

        if (decision.Snapshot.IsActive)
        {
            PixelInspectorUiEvent uiEvent = decision.SelectionCompleted
                ? new PixelInspectorUiEvent.SelectionCompleted(decision.Snapshot)
                : new PixelInspectorUiEvent.Update(decision.Snapshot);
            if (!TryPublish(uiEvent))
            {
                _session.Cancel();
            }
        }

        return new GlobalMouseDecision(decision.SuppressOriginal);
    }

    private bool TryPublish(PixelInspectorUiEvent uiEvent)
    {
        if (_events.Writer.TryWrite(uiEvent))
        {
            return true;
        }

        _disabled = true;
        Interlocked.Exchange(ref _closed, 1);
        _events.Writer.TryComplete();
        return false;
    }
}
