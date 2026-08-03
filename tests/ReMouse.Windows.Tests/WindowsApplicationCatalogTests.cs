using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReMouse.Windows.Input;

namespace ReMouse.Windows.Tests;

[TestClass]
public sealed class WindowsApplicationCatalogTests
{
    [TestMethod]
    public void CandidateKeepsTheOriginalTwoFieldConstructionSurface()
    {
        var candidate = new WindowsApplicationCandidate("Editor", "Editor.exe");

        var (displayName, executablePath) = candidate;

        Assert.AreEqual("Editor", displayName);
        Assert.AreEqual("Editor.exe", executablePath);
        Assert.AreEqual(string.Empty, candidate.Arguments);
        Assert.AreEqual(WindowsApplicationSource.Unknown, candidate.Source);
    }

    [TestMethod]
    public void StartMenuCatalogResolvesTargetsAndPreservesShortcutArguments()
    {
        using var fixture = new CatalogFixture();
        var executable = fixture.CreateFile("Editor.exe");
        var shortcut = fixture.CreateFile("Editor.lnk");

        var applications = WindowsApplicationCatalog.GetStartMenuApplications(
            new[] { fixture.Root },
            path => string.Equals(path, shortcut, StringComparison.OrdinalIgnoreCase)
                ? new WindowsShortcutTarget(executable, "--safe")
                : null);

        Assert.AreEqual(1, applications.Count);
        var application = applications[0];
        Assert.AreEqual("Editor", application.DisplayName);
        Assert.AreEqual(executable, application.ExecutablePath);
        Assert.AreEqual("--safe", application.Arguments);
        Assert.AreEqual(WindowsApplicationSource.StartMenu, application.Source);
        StringAssert.Contains(application.DisplayText, "Start Menu");
    }

    [TestMethod]
    public void StartMenuCatalogSkipsMissingTargetsAndDeduplicatesSameLaunch()
    {
        using var fixture = new CatalogFixture();
        var executable = fixture.CreateFile("Editor.exe");
        var firstShortcut = fixture.CreateFile(Path.Combine("Vendor", "Editor.lnk"));
        var secondShortcut = fixture.CreateFile(Path.Combine("Vendor", "Editor-copy.lnk"));
        fixture.CreateFile("Missing.lnk");

        var applications = WindowsApplicationCatalog.GetStartMenuApplications(
            new[] { fixture.Root, fixture.Root },
            path => path switch
            {
                var value when string.Equals(value, firstShortcut, StringComparison.OrdinalIgnoreCase) =>
                    new WindowsShortcutTarget(executable, "--safe"),
                var value when string.Equals(value, secondShortcut, StringComparison.OrdinalIgnoreCase) =>
                    new WindowsShortcutTarget(executable, "--safe"),
                _ => new WindowsShortcutTarget(Path.Combine(fixture.Root, "does-not-exist.exe"), "")
            });

        Assert.AreEqual(1, applications.Count);
        var application = applications[0];
        Assert.AreEqual("--safe", application.Arguments);
    }

    private sealed class CatalogFixture : IDisposable
    {
        public CatalogFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "reMOUSE-catalog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string CreateFile(string relativePath)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
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
