using System.Text;
using ReMouse.Core.Input;
using ReMouse.Core.Settings;
using ReMouse.Windows.Input;

namespace ReMouse.InputProbe;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        ProbeOptions options;
        try
        {
            options = ProbeOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            ProbeOptions.PrintHelp(Console.Error);
            return 2;
        }

        if (options.ShowHelp)
        {
            ProbeOptions.PrintHelp(Console.Out);
            return 0;
        }

        JsonSettingsStore? settingsStore = null;
        ReMouseSettings settings;
        try
        {
            settingsStore = new JsonSettingsStore(options.SettingsPath);
            settings = settingsStore.Load();
        }
        catch (Exception exception)
        {
            // Settings are optional at runtime. A path/IO problem must not stop
            // the raw observer from starting or prevent hook cleanup.
            Console.Error.WriteLine($"Warning: settings unavailable; using defaults ({exception.Message})");
            settings = ReMouseSettings.CreateDefault();
        }

        if (!options.FlickDeltaSpecified)
        {
            options = options with { FlickDelta = settings.Flick.Delta };
        }

        var mapper = new SideButtonMapper(settings.SideButtonBindings);
        WindowsInputEffectSink? effectSink = options.MiddleFlickEnabled
            ? new WindowsInputEffectSink()
            : null;
        using var stop = new CancellationTokenSource();
        using var events = new ProbeEventChannel();
        using var hook = new LowLevelHookHost(events, options);
        await using var processor = new ProbeEventProcessor(
            events,
            options,
            mapper,
            effectSink: effectSink,
            middleChord: hook.MiddleChordController);

        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stop.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        Action<ReMouseSettings>? saveSettings = settingsStore is null ? null : settingsStore.Save;
        var swapCoordinator = new SideButtonSwapCoordinator(
            mapper,
            events,
            settings,
            saveSettings);
        var commandTask = Task.CompletedTask;

        try
        {
            hook.Start();
            processor.Start();

            Console.WriteLine("ReMouse.InputProbe listening.");
            if (options.MiddleFlickEnabled)
            {
                Console.WriteLine(
                    $"Middle flick enabled (delta {options.FlickDelta}); chord events may be blocked and effects injected.");
            }
            else
            {
                Console.WriteLine("Only observing low-level input; no events are blocked or injected.");
            }
            Console.WriteLine("Press Ctrl+C to stop.");
            PrintBindings(mapper, Console.Out);
            Console.WriteLine("Commands: S = swap and save, P = print bindings, Q = quit.");

            commandTask = RunConsoleCommandsAsync(mapper, swapCoordinator, stop);

            await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // Normal Ctrl+C shutdown.
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Fatal: {exception.Message}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            stop.Cancel();
            hook.Dispose();
            try
            {
                // Let an in-flight Swap finish its save/enqueue/ack transaction
                // before completing the processor channel.
                await commandTask;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Warning: command loop shutdown failed ({exception.Message})");
            }
            await processor.StopAsync();
        }

        return processor.HasProcessingErrors ? 1 : 0;
    }

    private static async Task RunConsoleCommandsAsync(
        SideButtonMapper mapper,
        SideButtonSwapCoordinator swapCoordinator,
        CancellationTokenSource stop)
    {
        var cancellationToken = stop.Token;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    return;
                }

                switch (line.Trim().ToLowerInvariant())
                {
                    case "s":
                    case "swap":
                        await SwapAndSaveAsync(mapper, swapCoordinator);
                        break;
                    case "p":
                    case "print":
                        PrintBindings(mapper, Console.Out);
                        break;
                    case "q":
                    case "quit":
                    case "exit":
                        stop.Cancel();
                        return;
                    case "":
                        break;
                    default:
                        Console.Error.WriteLine("Command not recognized. Use S, P, or Q.");
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Warning: console command loop stopped ({exception.Message})");
        }
    }

    private static async Task SwapAndSaveAsync(
        SideButtonMapper mapper,
        SideButtonSwapCoordinator swapCoordinator)
    {
        var result = await swapCoordinator.TrySwapAndSaveAsync().ConfigureAwait(false);
        if (result.Succeeded)
        {
            Console.WriteLine("Side-button bindings swapped.");
            PrintBindings(mapper, Console.Out);
            return;
        }

        if (result.RollbackError is not null)
        {
            Console.Error.WriteLine(
                $"Critical: swap failed and settings rollback failed ({result.RollbackError.Message})");
        }

        Console.Error.WriteLine($"Warning: swap was not applied ({result.Error?.Message})");
    }

    private static void PrintBindings(SideButtonMapper mapper, TextWriter writer)
    {
        var bindings = mapper.Bindings;
        writer.WriteLine($"XButton1 (Terra Pro lower side button): {bindings.XButton1}");
        writer.WriteLine($"XButton2 (Terra Pro upper side button): {bindings.XButton2}");
    }
}
