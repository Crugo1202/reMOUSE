using ReMouse.Core.Input;
using ReMouse.Core.Settings;

namespace ReMouse.InputProbe;

internal sealed class ProbeEventProcessor : IAsyncDisposable
{
    private readonly ProbeEventChannel _events;
    private readonly ProbeOptions _options;
    private readonly TextWriter _output;
    private readonly TextWriter _warnings;
    private readonly SideButtonMapper _mapper;
    private readonly SideButtonMapper _injectedMapper;
    private readonly IInputEffectSink? _effectSink;
    private readonly MiddleChordHookController? _middleChord;
    private CancellationTokenSource? _stop;
    private Task? _worker;
    private bool _syntheticMiddleDownOutstanding;

    public ProbeEventProcessor(
        ProbeEventChannel events,
        ProbeOptions options,
        TextWriter? output = null,
        TextWriter? warnings = null,
        IInputEffectSink? effectSink = null,
        MiddleChordHookController? middleChord = null)
        : this(
            events,
            options,
            new SideButtonMapper(DefaultSettings.SideButtonBindings),
            output,
            warnings,
            effectSink,
            middleChord)
    {
    }

    public ProbeEventProcessor(
        ProbeEventChannel events,
        ProbeOptions options,
        SideButtonMapper mapper,
        TextWriter? output = null,
        TextWriter? warnings = null,
        IInputEffectSink? effectSink = null,
        MiddleChordHookController? middleChord = null)
    {
        _events = events;
        _options = options;
        _mapper = mapper;
        _injectedMapper = new SideButtonMapper(mapper.Bindings);
        _effectSink = effectSink;
        _output = output ?? Console.Out;
        _warnings = warnings ?? Console.Error;
        _middleChord = middleChord;
    }

    public bool HasProcessingErrors { get; private set; }

    public void Start()
    {
        if (_worker is not null)
        {
            throw new InvalidOperationException("Event processor is already running.");
        }

        _stop = new CancellationTokenSource();
        _worker = ProcessAsync(_stop.Token);
    }

    public async Task StopAsync()
    {
        if (_worker is null || _stop is null)
        {
            return;
        }

        _events.Complete();

        try
        {
            await _worker.ConfigureAwait(false);
        }
        finally
        {
            _stop.Cancel();
            _stop.Dispose();
            _stop = null;
            _worker = null;
        }

        ReportPressedButtonsAtShutdown();
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var workItem in _events.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                Process(workItem);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            HasProcessingErrors = true;
            _events.Fail(exception);
            _warnings.WriteLine($"Warning: event processor stopped: {exception.Message}");
        }
        finally
        {
            if (_syntheticMiddleDownOutstanding && _effectSink is not null)
            {
                try
                {
                    _effectSink.Apply(new InputEffect.MiddleButtonUp());
                }
                catch (Exception exception)
                {
                    _warnings.WriteLine($"Warning: synthetic middle release failed: {exception.Message}");
                }

                _syntheticMiddleDownOutstanding = false;
            }
        }
    }

    private void Process(ProbeWorkItem workItem)
    {
        if (workItem.InputEvent is { } inputEvent)
        {
            ProcessInput(inputEvent);
            return;
        }

        if (workItem.SettingsToApply is { } settings)
        {
            try
            {
                _mapper.SetBindings(settings.SideButtonBindings);
                _injectedMapper.SetBindings(settings.SideButtonBindings);
                workItem.Applied?.TrySetResult(true);
            }
            catch (Exception exception)
            {
                workItem.Applied?.TrySetException(exception);
                throw;
            }

            return;
        }

        if (workItem.EffectToApply is { } effect)
        {
            ProcessEffect(effect);
        }
    }

    private void ProcessEffect(InputEffect effect)
    {
        if (effect is InputEffect.MiddleDragReady ready)
        {
            _middleChord?.MarkDragReady(ready.SequenceId);
            return;
        }

        if (_effectSink is null)
        {
            throw new InvalidOperationException(
                "An input effect was queued but no input effect sink is configured.");
        }

        if (effect is InputEffect.MiddleButtonDown)
        {
            // Track before Apply because SendInput can fail after partially
            // delivering a native packet.
            _syntheticMiddleDownOutstanding = true;
        }

        _effectSink.Apply(effect);

        if (effect is InputEffect.MiddleButtonUp)
        {
            _syntheticMiddleDownOutstanding = false;
        }
    }

    private void ProcessInput(ProbeEvent inputEvent)
    {
        if (inputEvent.Kind == ProbeEventKind.XButton && inputEvent.XButton is { } xButton)
        {
            ProcessXButton(inputEvent, xButton);
            return;
        }

        if (inputEvent.Kind == ProbeEventKind.Keyboard)
        {
            ProcessKeyboard(inputEvent);
        }
    }

    private void ProcessXButton(ProbeEvent inputEvent, XButtonId xButton)
    {
        var name = xButton == XButtonId.XButton1 ? "XButton1" : "XButton2";
        var action = inputEvent.IsDown ? "Down" : "Up";

        var mapper = inputEvent.IsInjected ? _injectedMapper : _mapper;
        var dispatch = inputEvent.IsDown
            ? mapper.OnDown(xButton)
            : mapper.OnUp(xButton);

        if (!inputEvent.IsInjected && dispatch.IsDuplicateDown)
        {
            _warnings.WriteLine($"Warning: duplicate {name} Down while already down.");
        }
        else if (!inputEvent.IsInjected && dispatch.IsOrphanUp)
        {
            _warnings.WriteLine($"Warning: {name} Up without a preceding Down.");
        }

        var raw = _options.ShowBindings
            ? $"Raw {name} {action} | Binding: {GetActionName(dispatch.Action)}"
            : $"{name} {action}";
        var line = inputEvent.IsInjected ? $"[Injected] {raw}" : raw;
        _output.WriteLine(line);
    }

    private void ProcessKeyboard(ProbeEvent inputEvent)
    {
        var name = KeyboardKeyName.Get(inputEvent.VirtualKey);
        var action = inputEvent.IsDown ? "Down" : "Up";
        var injectedPrefix = inputEvent.IsInjected ? "[Injected] " : string.Empty;
        _output.WriteLine($"{injectedPrefix}Keyboard {name} {action} (VK 0x{inputEvent.VirtualKey:X2})");
    }

    private void ReportPressedButtonsAtShutdown()
    {
        foreach (var button in new[] { XButtonId.XButton1, XButtonId.XButton2 })
        {
            if (_mapper.GetActiveAction(button) is { } activeAction)
            {
                var name = button == XButtonId.XButton1 ? "XButton1" : "XButton2";
                _warnings.WriteLine(
                    $"Warning: {name} ({GetActionName(activeAction)}) was still down when the probe stopped.");
            }
        }
    }

    private static string GetActionName(SideButtonAction action) => action switch
    {
        SideButtonAction.None => "None",
        SideButtonAction.PixelInspector => "PixelInspector",
        SideButtonAction.RadialMenu => "RadialMenu",
        _ => "Invalid"
    };
}
