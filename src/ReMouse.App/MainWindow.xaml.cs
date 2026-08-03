using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ReMouse.Core.Input;
using ReMouse.Core.Settings;
using ReMouse.Windows.Hooks;
using ReMouse.Windows.Input;

namespace ReMouse.App;

public partial class MainWindow : Window
{
    private RadialMenuOverlayWindow? _overlay;
    private RadialMenuInputBridge? _bridge;
    private PixelInspectorOverlayWindow? _pixelOverlay;
    private PixelInspectorInputBridge? _pixelBridge;
    private GlobalMouseHookHost? _hook;
    private GlobalKeyboardHookHost? _keyboardHook;
    private Task? _eventPump;
    private Task? _pixelEventPump;
    private Task? _middleEffectPump;
    private MiddleChordInputBridge? _middleBridge;
    private JsonSettingsStore? _settingsStore;
    private ReMouseSettings _settings = ReMouseSettings.CreateDefault();
    private List<RadialMenuSlotSettings> _workingSlots = new();
    private int _workingDeadZoneRadius;
    private double _workingStartAngleDegrees;
    private string? _selectedSlotId;
    private readonly object _configurationLock = new();
    private bool _loadingPanel;
    private bool _started;
    private int _paused;
    private int _pauseRequestPosted;
    private int _copyRequestPosted;
    private int _closing;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        try
        {
            _settingsStore = new JsonSettingsStore();
            _settings = _settingsStore.Load();
            SettingsHintText.Text = FormatSettingsLoadHint(_settingsStore);
        }
        catch (Exception exception)
        {
            // A malformed or inaccessible settings file must not prevent the
            // global hook from starting. Keep safe in-memory defaults and let
            // the next explicit Save repair the file.
            _settingsStore = null;
            _settings = ReMouseSettings.CreateDefault();
            SettingsHintText.Text = $"Settings unavailable; using defaults: {exception.Message}";
        }
        LoadSettingsIntoPanel(_settings);

        var layout = ConfiguredRadialMenu.Create(_settings.RadialMenu);
        var coordinateMapper = ScreenCoordinateMapper.FromVisual(this);
        _overlay = new RadialMenuOverlayWindow();
        _bridge = new RadialMenuInputBridge(
            layout,
            coordinateMapper.ToDip,
            FindButtonForAction(_settings.SideButtonBindings, SideButtonAction.RadialMenu));
        _pixelOverlay = new PixelInspectorOverlayWindow(
            coordinateMapper.ToDip,
            coordinateMapper.GetVirtualDipBounds);
        _pixelBridge = new PixelInspectorInputBridge(
            FindButtonForAction(_settings.SideButtonBindings, SideButtonAction.PixelInspector));
        var effectSink = new WindowsInputEffectSink();
        _middleBridge = new MiddleChordInputBridge(_settings.Flick.Delta);
        var actionExecutor = new WindowsRadialMenuActionExecutor(effectSink);
        var pump = new RadialMenuEventPump(layout, _overlay, actionExecutor, _bridge);
        var pixelPump = new PixelInspectorEventPump(_pixelOverlay, _pixelBridge);
        var middlePump = new MiddleChordEffectPump(effectSink, _middleBridge);
        _eventPump = RunPumpAsync(pump, _bridge);
        _pixelEventPump = RunPixelPumpAsync(pixelPump, _pixelBridge);
        _middleEffectPump = RunMiddleEffectPumpAsync(middlePump, _middleBridge);
        _hook = new GlobalMouseHookHost(HandleGlobalMouseEvent);

        try
        {
            _hook.Start();
            var keyboardPauseAvailable = true;
            try
            {
                _keyboardHook = new GlobalKeyboardHookHost(RequestEmergencyPause, HandleGlobalKeyDown);
                _keyboardHook.Start();
            }
            catch (Exception exception)
            {
                _keyboardHook?.Dispose();
                _keyboardHook = null;
                keyboardPauseAvailable = false;
                StatusText.Text = $"Ready (global Ctrl+Alt+F12 pause unavailable): {exception.Message}";
            }

            StatusText.Text = $"Ready - XButton1: {FormatSideAction(_settings.SideButtonBindings.XButton1)}; " +
                              $"XButton2: {FormatSideAction(_settings.SideButtonBindings.XButton2)}." +
                              (keyboardPauseAvailable ? " Ctrl+Alt+F12 pauses remapping." : string.Empty);
        }
        catch (Exception exception)
        {
            // Start() can time out while its hook thread is still unwinding.
            // Dispose immediately so a failed startup cannot leave a native
            // global hook alive behind the fail-open bridges.
            _hook.Dispose();
            _hook = null;
            _keyboardHook?.Dispose();
            _keyboardHook = null;
            StatusText.Text = $"Global hook unavailable: {exception.Message}";
            _bridge.Complete();
            _pixelBridge.Complete();
            _middleBridge.Complete();
        }
    }

    private GlobalMouseDecision HandleGlobalMouseEvent(GlobalMouseEvent input)
    {
        if (Volatile.Read(ref _paused) != 0)
        {
            return new GlobalMouseDecision(false);
        }

        // Reconfigure all modal bridges as one fail-open hook transaction. A
        // setting swap is performed on the WPF thread, while hook callbacks
        // arrive concurrently. Never wait in the hook callback: if the UI is
        // applying settings (or another callback owns the short transaction),
        // preserve the original Windows event and return immediately.
        if (!Monitor.TryEnter(_configurationLock))
        {
            return new GlobalMouseDecision(false);
        }

        try
        {
            var bypassBefore = IsModalInputModeActive();
            var radialDecision = _bridge?.Handle(input) ?? new GlobalMouseDecision(false);
            var pixelDecision = _pixelBridge?.Handle(input) ?? new GlobalMouseDecision(false);
            var bypassAfter = ShouldBypassMiddleChord(input);
            if (bypassBefore || bypassAfter)
            {
                // A modal side-button mode can interrupt a middle chord after
                // its Down was already suppressed. Reset held state at both
                // sides of the transition so a later ordinary click cannot
                // inherit it.
                _middleBridge?.Cancel();
            }

            var middleDecision = bypassAfter
                ? new GlobalMouseDecision(false)
                : _middleBridge?.Handle(input) ?? new GlobalMouseDecision(false);
            return new GlobalMouseDecision(
                radialDecision.SuppressOriginal ||
                pixelDecision.SuppressOriginal ||
                middleDecision.SuppressOriginal);
        }
        finally
        {
            Monitor.Exit(_configurationLock);
        }
    }

    private async Task RunPumpAsync(
        RadialMenuEventPump pump,
        RadialMenuInputBridge bridge)
    {
        try
        {
            await pump.RunAsync(bridge.ReadAllAsync()).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Radial menu stopped: {exception.Message}";
        }
    }

    private async Task RunPixelPumpAsync(
        PixelInspectorEventPump pump,
        PixelInspectorInputBridge bridge)
    {
        try
        {
            await pump.RunAsync(bridge.ReadAllAsync()).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Pixel inspector stopped: {exception.Message}";
        }
    }

    private async Task RunMiddleEffectPumpAsync(
        MiddleChordEffectPump pump,
        MiddleChordInputBridge bridge)
    {
        try
        {
            await pump.RunAsync(bridge.EffectReader).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Dispatcher.Invoke(() => StatusText.Text = $"Middle flick stopped: {exception.Message}");
        }
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _closing, 1) != 0)
        {
            return;
        }

        App.NotifyPrimaryClosing();
        Volatile.Write(ref _paused, 1);
        _keyboardHook?.Dispose();
        _hook?.Dispose();
        _bridge?.Complete();
        _pixelBridge?.Complete();
        _middleBridge?.Complete();

        if (_eventPump is not null)
        {
            try
            {
                await _eventPump.ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Radial menu pump stopped: {exception}");
            }
        }

        if (_pixelEventPump is not null)
        {
            try
            {
                await _pixelEventPump.ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Pixel inspector pump stopped: {exception}");
            }
        }

        if (_middleEffectPump is not null)
        {
            try
            {
                await _middleEffectPump.ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Middle effect pump stopped: {exception}");
            }
        }

        _overlay?.Dismiss();
        _pixelOverlay?.Dismiss();
    }

    public void ActivateFromExternalLaunch()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        if (!IsVisible)
        {
            Show();
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void OnPauseButton(object sender, RoutedEventArgs e) =>
        SetPaused(Volatile.Read(ref _paused) == 0);

    private void RequestEmergencyPause()
    {
        if (Volatile.Read(ref _closing) != 0 || Interlocked.Exchange(ref _pauseRequestPosted, 1) != 0)
        {
            return;
        }

        // Flip the hook-visible gate before posting to WPF. A busy dispatcher
        // must not leave a window where subsequent mouse events are still
        // intercepted after the physical emergency chord.
        Volatile.Write(ref _paused, 1);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Interlocked.Exchange(ref _pauseRequestPosted, 0);
            if (Volatile.Read(ref _closing) == 0)
            {
                SetPaused(true);
            }
        }));
    }

    private void HandleGlobalKeyDown(uint virtualKey)
    {
        const uint VkC = 0x43;
        if (virtualKey != VkC ||
            Volatile.Read(ref _closing) != 0 ||
            Volatile.Read(ref _paused) != 0 ||
            !WindowsModifierState.IsControlDown() ||
            !(_pixelBridge?.TryGetClipboardText(out var text) ?? false) ||
            Interlocked.Exchange(ref _copyRequestPosted, 1) != 0)
        {
            return;
        }

        try
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    try
                    {
                        Clipboard.SetText(text);
                        StatusText.Text = "Pixel coordinates copied to clipboard.";
                    }
                    catch (Exception exception)
                    {
                        StatusText.Text = $"Could not copy pixel coordinates: {exception.Message}";
                    }
                    finally
                    {
                        Volatile.Write(ref _copyRequestPosted, 0);
                    }
                }));
        }
        catch
        {
            Volatile.Write(ref _copyRequestPosted, 0);
        }
    }

    private void SetPaused(bool paused)
    {
        if (Volatile.Read(ref _closing) != 0 && !paused)
        {
            return;
        }

        Volatile.Write(ref _paused, paused ? 1 : 0);
        PauseButton.Content = paused ? "Resume" : "Pause";
        if (paused)
        {
            _bridge?.Cancel();
            _pixelBridge?.Cancel();
            _middleBridge?.Cancel();
            _overlay?.Dismiss();
            _pixelOverlay?.Dismiss();
            StatusText.Text = "Paused. Press Resume to re-enable remapping (Ctrl+Alt+F12 is the emergency pause).";
        }
        else
        {
            StatusText.Text = $"Ready - XButton1: {FormatSideAction(_settings.SideButtonBindings.XButton1)}; " +
                              $"XButton2: {FormatSideAction(_settings.SideButtonBindings.XButton2)}.";
        }
    }

    private void LoadSettingsIntoPanel(ReMouseSettings settings)
    {
        _loadingPanel = true;
        try
        {
            LowerButtonActionCombo.SelectedValue = settings.SideButtonBindings.XButton1.ToString();
            UpperButtonActionCombo.SelectedValue = settings.SideButtonBindings.XButton2.ToString();
            FlickDeltaTextBox.Text = settings.Flick.Delta.ToString();
            _workingDeadZoneRadius = settings.RadialMenu.DeadZoneRadius;
            _workingStartAngleDegrees = settings.RadialMenu.StartAngleDegrees;

            _workingSlots = settings.RadialMenu.Slots.ToList();
            RadialSlotComboBox.Items.Clear();
            foreach (var slot in _workingSlots)
            {
                RadialSlotComboBox.Items.Add(new ComboBoxItem
                {
                    Content = slot.Label.Replace('\n', ' '),
                    Tag = slot.Id
                });
            }

            if (RadialSlotComboBox.Items.Count > 0)
            {
                RadialSlotComboBox.SelectedIndex = 0;
            }
        }
        finally
        {
            _loadingPanel = false;
        }

        if (RadialSlotComboBox.Items.Count > 0)
        {
            _selectedSlotId = ((ComboBoxItem)RadialSlotComboBox.SelectedItem).Tag as string;
            LoadSelectedSlot();
        }
    }

    private void LoadSelectedSlot()
    {
        var slot = _workingSlots.FirstOrDefault(candidate => candidate.Id == _selectedSlotId);
        if (slot is null)
        {
            return;
        }

        _loadingPanel = true;
        try
        {
            RadialLabelTextBox.Text = slot.Label;
            RadialActionComboBox.SelectedValue = slot.ActionKind.ToString();
            ShortcutTextBox.Tag = slot.ShortcutVirtualKeys.ToArray();
            ShortcutTextBox.Text = FormatShortcut(slot.ShortcutVirtualKeys);
            ApplicationPathTextBox.Text = slot.ExecutablePath;
            ApplicationArgumentsTextBox.Text = slot.Arguments;
            UpdateActionFields();
        }
        finally
        {
            _loadingPanel = false;
        }
    }

    private bool TryCommitCurrentSlot(out string? error)
    {
        error = null;
        if (_selectedSlotId is null)
        {
            return true;
        }

        var index = _workingSlots.FindIndex(slot => slot.Id == _selectedSlotId);
        if (index < 0)
        {
            return true;
        }

        if (!Enum.TryParse<RadialMenuActionKindProxy>(
                RadialActionComboBox.SelectedValue as string,
                out var proxy))
        {
            error = "Choose a radial action.";
            return false;
        }

        var actionKind = (ConfiguredRadialActionKind)proxy;
        var keys = ShortcutTextBox.Tag as ushort[] ?? Array.Empty<ushort>();
        try
        {
            _workingSlots[index] = new RadialMenuSlotSettings(
                _workingSlots[index].Id,
                RadialLabelTextBox.Text,
                actionKind,
                keys,
                ApplicationPathTextBox.Text,
                ApplicationArgumentsTextBox.Text);
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private void OnSideButtonSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingPanel)
        {
            SettingsHintText.Text = "Unsaved changes";
        }
    }

    private void OnRadialSlotChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingPanel)
        {
            return;
        }

        if (!TryCommitCurrentSlot(out var error))
        {
            SettingsHintText.Text = error ?? "The current slot is invalid.";
            _loadingPanel = true;
            RadialSlotComboBox.SelectedValue = _selectedSlotId;
            _loadingPanel = false;
            return;
        }

        _selectedSlotId = (RadialSlotComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
        LoadSelectedSlot();
    }

    private void OnRadialActionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingPanel)
        {
            UpdateActionFields();
            SettingsHintText.Text = "Unsaved changes";
        }
    }

    private void UpdateActionFields()
    {
        var action = RadialActionComboBox.SelectedValue as string;
        var isShortcut = string.Equals(action, nameof(ConfiguredRadialActionKind.Shortcut), StringComparison.Ordinal);
        var isApplication = string.Equals(action, nameof(ConfiguredRadialActionKind.LaunchApplication), StringComparison.Ordinal);
        ShortcutTextBox.IsEnabled = isShortcut;
        ShortcutTextBox.Opacity = isShortcut ? 1 : 0.45;
        ClearShortcutButton.IsEnabled = isShortcut;
        ClearShortcutButton.Opacity = isShortcut ? 1 : 0.45;
        ApplicationPathTextBox.IsEnabled = isApplication;
        ApplicationArgumentsTextBox.IsEnabled = isApplication;
    }

    private void OnShortcutPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Back or Key.Delete)
        {
            ClearShortcut();
            e.Handled = true;
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0 || key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift)
        {
            e.Handled = true;
            return;
        }

        var keys = new List<ushort>(4);
        var modifiers = Keyboard.Modifiers;
        if (modifiers.HasFlag(ModifierKeys.Control)) keys.Add(0x11);
        if (modifiers.HasFlag(ModifierKeys.Alt)) keys.Add(0x12);
        if (modifiers.HasFlag(ModifierKeys.Shift)) keys.Add(0x10);
        if (modifiers.HasFlag(ModifierKeys.Windows)) keys.Add(0x5B);
        keys.Add((ushort)virtualKey);
        ShortcutTextBox.Tag = keys.ToArray();
        ShortcutTextBox.Text = FormatShortcut(keys);
        SettingsHintText.Text = "Unsaved changes";
        e.Handled = true;
    }

    private void OnClearShortcut(object sender, RoutedEventArgs e) =>
        ClearShortcut();

    private void ClearShortcut()
    {
        ShortcutTextBox.Tag = Array.Empty<ushort>();
        ShortcutTextBox.Text = string.Empty;
        SettingsHintText.Text = "Shortcut cleared; unsaved changes";
    }

    private void OnBrowseApplication(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            Title = "Choose an application"
        };
        if (dialog.ShowDialog(this) == true)
        {
            ApplicationPathTextBox.Text = dialog.FileName;
            SettingsHintText.Text = "Unsaved changes";
        }
    }

    private void OnChooseRunningApplication(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<WindowsApplicationCandidate> applications;
        try
        {
            applications = WindowsApplicationCatalog.GetRunningApplications();
        }
        catch (Exception exception)
        {
            SettingsHintText.Text = $"Could not list running apps: {exception.Message}";
            return;
        }

        if (applications.Count == 0)
        {
            SettingsHintText.Text = "No visible running apps were found; use Browse... to choose an .exe.";
            return;
        }

        ChooseApplication(
            applications,
            "Choose running application",
            "Choose a visible running app; reMOUSE will wake it instead of opening a duplicate.",
            "Selected a running app; Save settings to use wake behavior.");
    }

    private void OnChooseStartMenuApplication(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<WindowsApplicationCandidate> applications;
        try
        {
            applications = WindowsApplicationCatalog.GetStartMenuApplications();
        }
        catch (Exception exception)
        {
            SettingsHintText.Text = $"Could not list Start Menu apps: {exception.Message}";
            return;
        }

        if (applications.Count == 0)
        {
            SettingsHintText.Text = "No executable Start Menu shortcuts were found; use Browse... to choose an .exe.";
            return;
        }

        ChooseApplication(
            applications,
            "Choose Start Menu application",
            "Choose a Start Menu shortcut; reMOUSE stores its executable target and arguments.",
            "Selected a Start Menu app; Save settings to use wake behavior.");
    }

    private void ChooseApplication(
        IReadOnlyList<WindowsApplicationCandidate> applications,
        string title,
        string description,
        string successMessage)
    {
        var picker = new ApplicationPickerWindow(applications, title, description)
        {
            Owner = this
        };
        if (picker.ShowDialog() == true && picker.SelectedApplication is { } selected)
        {
            ApplicationPathTextBox.Text = selected.ExecutablePath;
            ApplicationArgumentsTextBox.Text = selected.Arguments;
            SettingsHintText.Text = successMessage;
        }
    }

    private void OnResetSettings(object sender, RoutedEventArgs e)
    {
        LoadSettingsIntoPanel(ReMouseSettings.CreateDefault());
        SettingsHintText.Text = "Defaults loaded in the editor; click Save settings to apply them.";
    }

    private void OnSaveSettings(object sender, RoutedEventArgs e)
    {
        if (!TryCommitCurrentSlot(out var slotError))
        {
            SettingsHintText.Text = slotError ?? "The current slot is invalid.";
            return;
        }

        try
        {
            var lower = ParseSideButtonAction(LowerButtonActionCombo);
            var upper = ParseSideButtonAction(UpperButtonActionCombo);
            if (lower != SideButtonAction.None && lower == upper)
            {
                throw new ArgumentException("Assign radial menu and pixel inspector to different side buttons.");
            }
            if (!int.TryParse(FlickDeltaTextBox.Text, out var delta))
            {
                throw new ArgumentException("Flick delta must be a positive integer.");
            }

            var flick = new FlickSettings(delta);
            flick.Validate();
            var radial = new RadialMenuSettings(
                _workingSlots,
                _workingDeadZoneRadius,
                _workingStartAngleDegrees);
            // Validate the complete runtime action graph before writing the
            // file, so a bad editor state cannot become the next startup's
            // recovery event.
            _ = ConfiguredRadialMenu.Create(radial);
            var updated = new ReMouseSettings(
                DefaultSettings.CurrentSchemaVersion,
                new SideButtonBindings(lower, upper),
                flick,
                radial);
            (_settingsStore ??= new JsonSettingsStore()).Save(updated);
            ApplyRuntimeSettings(updated);
            _settings = updated;
            SettingsHintText.Text = "Saved and applied immediately.";
            StatusText.Text = "Settings saved and applied.";
        }
        catch (ArgumentException exception)
        {
            SettingsHintText.Text = exception.Message;
        }
        catch (IOException exception)
        {
            SettingsHintText.Text = $"Could not save settings: {exception.Message}";
        }
        catch (UnauthorizedAccessException exception)
        {
            SettingsHintText.Text = $"Could not save settings: {exception.Message}";
        }
    }

    private void ApplyRuntimeSettings(ReMouseSettings settings)
    {
        var layout = ConfiguredRadialMenu.Create(settings.RadialMenu);
        lock (_configurationLock)
        {
            _bridge?.Reconfigure(
                layout,
                FindButtonForAction(settings.SideButtonBindings, SideButtonAction.RadialMenu));
            _pixelBridge?.Reconfigure(
                FindButtonForAction(settings.SideButtonBindings, SideButtonAction.PixelInspector));
            _middleBridge?.Reconfigure(settings.Flick.Delta);
        }
    }

    private static SideButtonAction ParseSideButtonAction(ComboBox combo)
    {
        if (combo.SelectedValue is string value && Enum.TryParse<SideButtonAction>(value, out var action))
        {
            return action;
        }

        throw new ArgumentException("Choose a valid side-button action.");
    }

    private static string FormatShortcut(IReadOnlyList<ushort> keys)
    {
        if (keys.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            "+",
            keys.Select(key => key switch
            {
                0x10 => "Shift",
                0x11 => "Ctrl",
                0x12 => "Alt",
                0x5B => "Win",
                _ => KeyInterop.KeyFromVirtualKey(key).ToString()
            }));
    }

    private enum RadialMenuActionKindProxy
    {
        NoOp,
        Shortcut,
        LaunchApplication
    }

    private static GlobalMouseButton? FindButtonForAction(
        SideButtonBindings bindings,
        SideButtonAction action) =>
        bindings.XButton1 == action
            ? GlobalMouseButton.XButton1
            : bindings.XButton2 == action
                ? GlobalMouseButton.XButton2
                : null;

    private static string FormatSideAction(SideButtonAction action) => action switch
    {
        SideButtonAction.PixelInspector => "pixel inspector",
        SideButtonAction.RadialMenu => "radial menu",
        _ => "pass through"
    };

    private static string FormatSettingsLoadHint(JsonSettingsStore store) => store.LastLoadStatus switch
    {
        SettingsLoadStatus.CreatedDefaults => "Created safe default settings.",
        SettingsLoadStatus.RecoveredCorruptFile when store.LastRecoveryBackupPath is { } backup =>
            $"Recovered invalid settings; backup saved as {Path.GetFileName(backup)}.",
        SettingsLoadStatus.RecoveredCorruptFile => "Recovered invalid settings; defaults are loaded.",
        SettingsLoadStatus.UsedDefaultsForInvalidDocument =>
            "Settings were invalid or from an unsupported version; defaults are loaded. Save to repair.",
        SettingsLoadStatus.UsedDefaultsForUnreadableFile =>
            "Settings could not be read; defaults are loaded. Check permissions, then Save to repair.",
        _ => "Settings loaded."
    };

    private bool ShouldBypassMiddleChord(GlobalMouseEvent input)
    {
        if (input.Kind != GlobalMouseEventKind.Button ||
            input.Button is not (GlobalMouseButton.Left or GlobalMouseButton.Right or GlobalMouseButton.Middle))
        {
            return false;
        }

        return IsModalInputModeActive();
    }

    private bool IsModalInputModeActive() =>
        (_bridge?.IsOpen ?? false) || (_pixelBridge?.IsActive ?? false);
}
