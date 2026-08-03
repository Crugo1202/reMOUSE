using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Core.Input;
using ReMouse.Core.Settings;

namespace ReMouse.Core.Tests;

[TestClass]
public sealed class JsonSettingsStoreTests
{
    [TestMethod]
    public void DefaultStoreUsesTheLocalAppDataSettingsPath()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "reMOUSE",
            "settings.json");

        var store = new JsonSettingsStore();

        Assert.AreEqual(expected, store.SettingsPath);
    }

    [TestMethod]
    public void BareRelativePathIsNormalizedToTheCurrentDirectory()
    {
        var store = new JsonSettingsStore("settings.json");

        Assert.AreEqual(Path.GetFullPath("settings.json"), store.SettingsPath);
    }

    [TestMethod]
    public void MissingSettingsAreCreatedWithSafeDefaults()
    {
        using var fixture = new SettingsFixture();
        var store = fixture.CreateStore();

        var settings = store.Load();

        Assert.AreEqual(ReMouseSettings.CreateDefault(), settings);
        Assert.AreEqual(SettingsLoadStatus.CreatedDefaults, store.LastLoadStatus);
        Assert.IsTrue(File.Exists(store.SettingsPath));
    }

    [TestMethod]
    public void SavedBindingsRoundTripAsStringEnums()
    {
        using var fixture = new SettingsFixture();
        var store = fixture.CreateStore();
        var expected = new ReMouseSettings(
            DefaultSettings.CurrentSchemaVersion,
            new SideButtonBindings(SideButtonAction.RadialMenu, SideButtonAction.PixelInspector));

        store.Save(expected);
        var loaded = store.Load();

        Assert.AreEqual(expected, loaded);
        Assert.AreEqual(SettingsLoadStatus.Loaded, store.LastLoadStatus);
        StringAssert.Contains(File.ReadAllText(store.SettingsPath), "radialMenu");
        StringAssert.Contains(File.ReadAllText(store.SettingsPath), "pixelInspector");
    }

    [TestMethod]
    public void MissingBindingFieldsUseTheCorrespondingDefault()
    {
        using var fixture = new SettingsFixture();
        var store = fixture.CreateStore();
        File.WriteAllText(
            store.SettingsPath,
            "{\"schemaVersion\":1,\"sideButtonBindings\":{\"xButton1\":\"none\"}}");

        var loaded = store.Load();

        Assert.AreEqual(SideButtonAction.None, loaded.SideButtonBindings.XButton1);
        Assert.AreEqual(SideButtonAction.RadialMenu, loaded.SideButtonBindings.XButton2);
    }

    [TestMethod]
    public void SavingAnExistingFileReplacesItWithoutLeavingTemporaryFiles()
    {
        using var fixture = new SettingsFixture();
        var store = fixture.CreateStore();
        var first = new ReMouseSettings(
            DefaultSettings.CurrentSchemaVersion,
            new SideButtonBindings(SideButtonAction.None, SideButtonAction.RadialMenu));
        var second = new ReMouseSettings(
            DefaultSettings.CurrentSchemaVersion,
            new SideButtonBindings(SideButtonAction.RadialMenu, SideButtonAction.PixelInspector));

        store.Save(first);
        store.Save(second);

        Assert.AreEqual(second, store.Load());
        Assert.AreEqual(0, Directory.GetFiles(fixture.DirectoryPath, "settings.json.*.tmp").Length);
    }

    [TestMethod]
    public void InvalidEnumFallsBackToSafeDefaultsAndDoesNotThrow()
    {
        using var fixture = new SettingsFixture();
        var store = fixture.CreateStore();
        Directory.CreateDirectory(fixture.DirectoryPath);
        File.WriteAllText(
            store.SettingsPath,
            "{\"schemaVersion\":1,\"sideButtonBindings\":{\"xButton1\":\"notAnAction\",\"xButton2\":\"radialMenu\"}}");

        var loaded = store.Load();

        Assert.AreEqual(ReMouseSettings.CreateDefault(), loaded);
        Assert.AreEqual(SettingsLoadStatus.RecoveredCorruptFile, store.LastLoadStatus);
    }

    [TestMethod]
    public void MalformedJsonFallsBackToDefaultsAndAttemptsRecovery()
    {
        using var fixture = new SettingsFixture();
        var store = fixture.CreateStore();
        Directory.CreateDirectory(fixture.DirectoryPath);
        File.WriteAllText(store.SettingsPath, "{ not valid json");

        var loaded = store.Load();

        Assert.AreEqual(ReMouseSettings.CreateDefault(), loaded);
        Assert.AreEqual(SettingsLoadStatus.RecoveredCorruptFile, store.LastLoadStatus);
        Assert.IsNotNull(store.LastRecoveryBackupPath);
        Assert.IsFalse(File.Exists(store.SettingsPath));
        Assert.IsTrue(Directory.GetFiles(fixture.DirectoryPath, "settings.json.corrupt-*.json").Length >= 1);
    }

    [TestMethod]
    public void UnsupportedSchemaFallsBackToDefaults()
    {
        using var fixture = new SettingsFixture();
        var store = fixture.CreateStore();
        Directory.CreateDirectory(fixture.DirectoryPath);
        File.WriteAllText(
            store.SettingsPath,
            "{\"schemaVersion\":999,\"sideButtonBindings\":{\"xButton1\":\"radialMenu\",\"xButton2\":\"pixelInspector\"}}");

        var loaded = store.Load();

        Assert.AreEqual(ReMouseSettings.CreateDefault(), loaded);
        Assert.AreEqual(SettingsLoadStatus.UsedDefaultsForInvalidDocument, store.LastLoadStatus);
    }

    [TestMethod]
    public void FlickAndRadialConfigurationRoundTrip()
    {
        using var fixture = new SettingsFixture();
        var store = fixture.CreateStore();
        var radial = new RadialMenuSettings(
            new[]
            {
                new RadialMenuSlotSettings(
                    "designer",
                    "Designer",
                    ConfiguredRadialActionKind.LaunchApplication,
                    executablePath: "C:\\Tools\\designer.exe",
                    arguments: "--new"),
                new RadialMenuSlotSettings(
                    "copy",
                    "Copy",
                    ConfiguredRadialActionKind.Shortcut,
                    new ushort[] { 0x11, 0x43 })
            },
            deadZoneRadius: 44,
            startAngleDegrees: -45);
        var expected = new ReMouseSettings(
            DefaultSettings.CurrentSchemaVersion,
            DefaultSettings.SideButtonBindings,
            new FlickSettings(240),
            radial);

        store.Save(expected);
        var loaded = store.Load();

        Assert.AreEqual(expected, loaded);
        StringAssert.Contains(File.ReadAllText(store.SettingsPath), "designer.exe");
        StringAssert.Contains(File.ReadAllText(store.SettingsPath), "flick");
        StringAssert.Contains(File.ReadAllText(store.SettingsPath), "radialMenu");
    }

    [TestMethod]
    public void InvalidFlickConfigurationFallsBackToDefaults()
    {
        using var fixture = new SettingsFixture();
        var store = fixture.CreateStore();
        Directory.CreateDirectory(fixture.DirectoryPath);
        File.WriteAllText(
            store.SettingsPath,
            "{\"schemaVersion\":1,\"sideButtonBindings\":{\"xButton1\":\"none\",\"xButton2\":\"radialMenu\"},\"flick\":{\"delta\":9999},\"radialMenu\":{\"slots\":[{\"id\":\"tool\",\"label\":\"Tool\",\"actionKind\":\"launchApplication\",\"executablePath\":\"tool.exe\"}]}}" );

        var loaded = store.Load();

        Assert.AreEqual(DefaultSettings.Flick, loaded.Flick);
        Assert.AreEqual(SettingsLoadStatus.UsedDefaultsForInvalidDocument, store.LastLoadStatus);
        Assert.AreEqual(SideButtonAction.None, loaded.SideButtonBindings.XButton1);
        Assert.AreEqual(1, loaded.RadialMenu.Slots.Count);
        Assert.AreEqual("tool.exe", loaded.RadialMenu.Slots[0].ExecutablePath);
    }

    [TestMethod]
    public void RadialSettingsKeepImmutableSnapshots()
    {
        var keys = new ushort[] { 0x11, 0x43 };
        var slot = new RadialMenuSlotSettings("copy", "Copy", ConfiguredRadialActionKind.Shortcut, keys);
        var slots = new[] { slot };
        var radial = new RadialMenuSettings(slots);

        keys[0] = 0x12;
        slots[0] = new RadialMenuSlotSettings("other", "Other", ConfiguredRadialActionKind.NoOp);

        CollectionAssert.AreEqual(new ushort[] { 0x11, 0x43 }, slot.ShortcutVirtualKeys.ToArray());
        Assert.AreEqual("copy", radial.Slots[0].Id);
    }

    [TestMethod]
    public void NullRadialSlotFallsBackWithoutThrowingOrLosingBindings()
    {
        using var fixture = new SettingsFixture();
        var store = fixture.CreateStore();
        Directory.CreateDirectory(fixture.DirectoryPath);
        File.WriteAllText(
            store.SettingsPath,
            "{\"schemaVersion\":1,\"sideButtonBindings\":{\"xButton1\":\"none\",\"xButton2\":\"radialMenu\"},\"flick\":{\"delta\":240},\"radialMenu\":{\"slots\":[null]}}" );

        var loaded = store.Load();

        Assert.AreEqual(SideButtonAction.None, loaded.SideButtonBindings.XButton1);
        Assert.AreEqual(SettingsLoadStatus.UsedDefaultsForInvalidDocument, store.LastLoadStatus);
        Assert.AreEqual(SideButtonAction.RadialMenu, loaded.SideButtonBindings.XButton2);
        Assert.AreEqual(new FlickSettings(240), loaded.Flick);
        Assert.AreEqual(DefaultSettings.RadialMenu, loaded.RadialMenu);
    }

    [TestMethod]
    public void MissingSettingsReportUnreadableWhenDefaultsCannotBePersisted()
    {
        using var fixture = new SettingsFixture();
        var store = fixture.CreateStore();
        Directory.CreateDirectory(store.SettingsPath);

        var loaded = store.Load();

        Assert.AreEqual(ReMouseSettings.CreateDefault(), loaded);
        Assert.AreEqual(SettingsLoadStatus.UsedDefaultsForUnreadableFile, store.LastLoadStatus);
    }

    private sealed class SettingsFixture : IDisposable
    {
        public SettingsFixture()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "reMOUSE-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public JsonSettingsStore CreateStore() =>
            new(Path.Combine(DirectoryPath, "settings.json"));

        public void Dispose()
        {
            try
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
            catch (IOException)
            {
                // Test cleanup is best effort.
            }
            catch (UnauthorizedAccessException)
            {
                // Test cleanup is best effort.
            }
        }
    }
}
