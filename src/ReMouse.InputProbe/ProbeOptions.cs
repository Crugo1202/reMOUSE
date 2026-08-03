using ReMouse.Core.Settings;

namespace ReMouse.InputProbe;

internal enum KeyboardLoggingMode
{
    None,
    BrowserNavigation,
    All
}

internal sealed record ProbeOptions(
    KeyboardLoggingMode KeyboardLogging,
    bool IncludeInjected,
    bool ShowHelp)
{
    public const int DefaultFlickDelta = 120;

    public bool ShowBindings { get; init; }

    public string? SettingsPath { get; init; }

    public bool MiddleFlickEnabled { get; init; }

    public int FlickDelta { get; init; } = DefaultFlickDelta;

    public bool FlickDeltaSpecified { get; init; }

    public static ProbeOptions Parse(string[] args)
    {
        var keyboardLogging = KeyboardLoggingMode.None;
        var includeInjected = false;
        var showHelp = false;
        var middleFlickEnabled = false;
        var flickDelta = DefaultFlickDelta;
        var flickDeltaSpecified = false;
        string? settingsPath = null;

        for (var index = 0; index < args.Length; index++)
        {
            var rawArgument = args[index];
            var argument = rawArgument.Trim().ToLowerInvariant();
            switch (argument)
            {
                case "--keyboard":
                    keyboardLogging = KeyboardLoggingMode.BrowserNavigation;
                    break;
                case "--keyboard-all":
                    keyboardLogging = KeyboardLoggingMode.All;
                    break;
                case "--include-injected":
                    includeInjected = true;
                    break;
                case "--middle-flick":
                    middleFlickEnabled = true;
                    break;
                case "--flick-delta":
                    if (index + 1 >= args.Length ||
                        string.IsNullOrWhiteSpace(args[index + 1]) ||
                        args[index + 1].TrimStart().StartsWith("-", StringComparison.Ordinal) ||
                        !int.TryParse(args[index + 1], out flickDelta) ||
                        flickDelta is < FlickSettings.MinimumDelta or > FlickSettings.MaximumDelta)
                    {
                        throw new ArgumentException(
                            $"--flick-delta requires an integer from {FlickSettings.MinimumDelta} to {FlickSettings.MaximumDelta}.");
                    }

                    index++;
                    flickDeltaSpecified = true;
                    break;
                case "--settings":
                    if (index + 1 >= args.Length ||
                        string.IsNullOrWhiteSpace(args[index + 1]) ||
                        args[index + 1].TrimStart().StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new ArgumentException("--settings requires a file path.");
                    }

                    settingsPath = args[++index];
                    break;
                case "--help":
                case "-h":
                case "/?":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {rawArgument}");
            }
        }

        return new ProbeOptions(keyboardLogging, includeInjected, showHelp)
        {
            ShowBindings = true,
            SettingsPath = settingsPath,
            MiddleFlickEnabled = middleFlickEnabled,
            FlickDelta = flickDelta,
            FlickDeltaSpecified = flickDeltaSpecified
        };
    }

    public static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("ReMouse.InputProbe");
        writer.WriteLine();
        writer.WriteLine("Observes low-level XButton events without blocking or injecting input.");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  ReMouse.InputProbe.exe");
        writer.WriteLine("  ReMouse.InputProbe.exe --keyboard");
        writer.WriteLine("  ReMouse.InputProbe.exe --keyboard-all");
        writer.WriteLine("  ReMouse.InputProbe.exe --include-injected");
        writer.WriteLine("  ReMouse.InputProbe.exe --middle-flick --flick-delta 120");
        writer.WriteLine("  ReMouse.InputProbe.exe --settings <path>");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --keyboard          Log browser back/forward keyboard events.");
        writer.WriteLine("  --keyboard-all      Log every low-level keyboard event.");
        writer.WriteLine("  --include-injected  Include events marked as injected by another process.");
        writer.WriteLine("  --middle-flick      Opt in to middle+left/right horizontal flick handling.");
        writer.WriteLine("  --flick-delta <n>   Horizontal wheel delta for each flick (default: 120).");
        writer.WriteLine("  --settings <path>   Use a specific settings.json path (diagnostics/tests).");
        writer.WriteLine("  --help              Show this help.");
    }
}
