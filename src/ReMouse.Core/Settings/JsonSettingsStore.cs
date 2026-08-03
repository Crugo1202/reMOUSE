using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReMouse.Core.Input;

namespace ReMouse.Core.Settings;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly string _settingsPath;

    public JsonSettingsStore(string? settingsPath = null)
    {
        var requestedPath = settingsPath ?? GetDefaultSettingsPath();
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            throw new ArgumentException("Settings path cannot be empty.", nameof(settingsPath));
        }

        _settingsPath = Path.GetFullPath(requestedPath);
    }

    public string SettingsPath => _settingsPath;

    public SettingsLoadStatus LastLoadStatus { get; private set; } = SettingsLoadStatus.NotLoaded;

    public string? LastRecoveryBackupPath { get; private set; }

    public ReMouseSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            var defaults = ReMouseSettings.CreateDefault();
            var saved = TrySaveDefaults(defaults);
            LastRecoveryBackupPath = null;
            LastLoadStatus = saved
                ? SettingsLoadStatus.CreatedDefaults
                : SettingsLoadStatus.UsedDefaultsForUnreadableFile;
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath, Encoding.UTF8);
            var document = JsonSerializer.Deserialize<SettingsDocument>(json, JsonOptions);
            var usedFallback = false;
            var settings = document?.ToSettings(ref usedFallback);
            var normalized = settings?.Normalize();
            LastRecoveryBackupPath = null;
            if (settings is null || normalized is null)
            {
                LastLoadStatus = SettingsLoadStatus.UsedDefaultsForInvalidDocument;
                return ReMouseSettings.CreateDefault();
            }

            if (usedFallback || !ReferenceEquals(normalized, settings))
            {
                LastLoadStatus = SettingsLoadStatus.UsedDefaultsForInvalidDocument;
                return normalized;
            }

            LastLoadStatus = SettingsLoadStatus.Loaded;
            return normalized;
        }
        catch (JsonException)
        {
            LastRecoveryBackupPath = RecoverCorruptFile();
            LastLoadStatus = LastRecoveryBackupPath is null
                ? SettingsLoadStatus.UsedDefaultsForInvalidDocument
                : SettingsLoadStatus.RecoveredCorruptFile;
            return ReMouseSettings.CreateDefault();
        }
        catch (IOException)
        {
            // A locked, missing, or otherwise unreadable settings file must not
            // prevent the input hook from starting or shutting down.
            LastRecoveryBackupPath = null;
            LastLoadStatus = SettingsLoadStatus.UsedDefaultsForUnreadableFile;
            return ReMouseSettings.CreateDefault();
        }
        catch (UnauthorizedAccessException)
        {
            LastRecoveryBackupPath = null;
            LastLoadStatus = SettingsLoadStatus.UsedDefaultsForUnreadableFile;
            return ReMouseSettings.CreateDefault();
        }
    }

    public void Save(ReMouseSettings settings)
    {
        var normalized = settings.Normalize();
        var directory = Path.GetDirectoryName(_settingsPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Settings path must include a directory.");
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private bool TrySaveDefaults(ReMouseSettings defaults)
    {
        try
        {
            Save(defaults);
            return true;
        }
        catch (IOException)
        {
            // Defaults are still returned in memory when the directory cannot
            // be created. Persistence can be retried by the next save.
        }
        catch (UnauthorizedAccessException)
        {
            // See the IOException case above.
        }

        return false;
    }

    private string? RecoverCorruptFile()
    {
        try
        {
            var backupPath = $"{_settingsPath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json";
            File.Move(_settingsPath, backupPath, overwrite: false);
            return backupPath;
        }
        catch (IOException)
        {
            // Recovery is best effort. The caller still receives safe defaults.
        }
        catch (UnauthorizedAccessException)
        {
            // Recovery is best effort. The caller still receives safe defaults.
        }

        return null;
    }

    private static string GetDefaultSettingsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("Local application data path is unavailable.");
        }

        return Path.Combine(localAppData, "reMOUSE", "settings.json");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Temporary cleanup is best effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Temporary cleanup is best effort.
        }
    }

    private sealed class SettingsDocument
    {
        public int? SchemaVersion { get; set; }
        public SideButtonBindingsDocument? SideButtonBindings { get; set; }
        public FlickSettingsDocument? Flick { get; set; }
        public RadialMenuSettingsDocument? RadialMenu { get; set; }

        public ReMouseSettings? ToSettings(ref bool usedFallback)
        {
            if (SchemaVersion is null || SideButtonBindings is null)
            {
                return null;
            }

            var defaults = DefaultSettings.SideButtonBindings;
            var defaultFlick = DefaultSettings.Flick;
            var defaultRadialMenu = DefaultSettings.RadialMenu;
            return new ReMouseSettings(
                SchemaVersion.Value,
                new SideButtonBindings(
                    SideButtonBindings.XButton1 ?? defaults.XButton1,
                    SideButtonBindings.XButton2 ?? defaults.XButton2),
                Flick?.ToSettings(defaultFlick, ref usedFallback) ?? defaultFlick,
                RadialMenu?.ToSettings(defaultRadialMenu, ref usedFallback) ?? defaultRadialMenu);
        }
    }

    private sealed class SideButtonBindingsDocument
    {
        public SideButtonAction? XButton1 { get; set; }
        public SideButtonAction? XButton2 { get; set; }
    }

    private sealed class FlickSettingsDocument
    {
        public int? Delta { get; set; }

        public FlickSettings ToSettings(FlickSettings fallback, ref bool usedFallback)
        {
            if (Delta is null)
            {
                return fallback;
            }

            try
            {
                return new FlickSettings(Delta.Value);
            }
            catch (ArgumentOutOfRangeException)
            {
                usedFallback = true;
                return fallback;
            }
        }
    }

    private sealed class RadialMenuSettingsDocument
    {
        public List<RadialMenuSlotSettingsDocument>? Slots { get; set; }
        public int? DeadZoneRadius { get; set; }
        public double? StartAngleDegrees { get; set; }

        public RadialMenuSettings ToSettings(RadialMenuSettings fallback, ref bool usedFallback)
        {
            if (Slots is null)
            {
                return fallback;
            }

            try
            {
                var slots = Slots.Select(slot =>
                    slot is null
                        ? throw new ArgumentException("A radial slot cannot be null.", nameof(Slots))
                        : slot.ToSettings()).ToArray();
                return new RadialMenuSettings(
                    slots,
                    DeadZoneRadius ?? fallback.DeadZoneRadius,
                    StartAngleDegrees ?? fallback.StartAngleDegrees);
            }
            catch (ArgumentException)
            {
                usedFallback = true;
                return fallback;
            }
        }
    }

    private sealed class RadialMenuSlotSettingsDocument
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public ConfiguredRadialActionKind? ActionKind { get; set; }
        public ushort[]? ShortcutVirtualKeys { get; set; }
        public string? ExecutablePath { get; set; }
        public string? Arguments { get; set; }

        public RadialMenuSlotSettings ToSettings() =>
            new(
                Id ?? string.Empty,
                Label ?? string.Empty,
                ActionKind ?? ConfiguredRadialActionKind.NoOp,
                ShortcutVirtualKeys,
                ExecutablePath,
                Arguments);
    }
}
