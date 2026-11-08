using DesktopCompanion.Core.Sensors;
using Xunit;

namespace DesktopCompanion.Tests.Sensors;

public sealed class StartMenuShortcutValidatorTests
{
    [Fact]
    public void IsAllowedShortcut_accepts_lnk_under_an_allowed_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "Start Menu");
        var shortcut = Path.Combine(root, "Programs", "Example.lnk");

        Assert.True(StartMenuShortcutValidator.IsAllowedShortcut(shortcut, [root]));
    }

    [Fact]
    public void IsAllowedShortcut_rejects_paths_outside_the_allowed_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "Start Menu");
        var shortcut = Path.Combine(Path.GetTempPath(), "Other", "Example.lnk");

        Assert.False(StartMenuShortcutValidator.IsAllowedShortcut(shortcut, [root]));
    }

    [Fact]
    public void IsAllowedShortcut_rejects_path_traversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "Start Menu");
        var shortcut = Path.Combine(root, "..", "Outside", "Example.lnk");

        Assert.False(StartMenuShortcutValidator.IsAllowedShortcut(shortcut, [root]));
    }

    [Fact]
    public void IsAllowedShortcut_rejects_non_shortcut_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "Start Menu");
        var executable = Path.Combine(root, "Programs", "Example.exe");

        Assert.False(StartMenuShortcutValidator.IsAllowedShortcut(executable, [root]));
    }

    [Fact]
    public void IsAllowedShortcut_rejects_the_root_itself_and_empty_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), "Start Menu");

        Assert.False(StartMenuShortcutValidator.IsAllowedShortcut(root, [root]));
        Assert.False(StartMenuShortcutValidator.IsAllowedShortcut(" ", [root]));
    }
}
