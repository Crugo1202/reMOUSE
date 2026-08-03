namespace ReMouse.Core.Settings;

/// <summary>
/// Describes how the last settings load completed so the UI can explain a
/// recovery instead of silently replacing the user's configuration.
/// </summary>
public enum SettingsLoadStatus
{
    NotLoaded,
    Loaded,
    CreatedDefaults,
    RecoveredCorruptFile,
    UsedDefaultsForInvalidDocument,
    UsedDefaultsForUnreadableFile
}
