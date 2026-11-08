namespace DesktopCompanion.Core.Sensors;

/// <summary>Validates that a shortcut path stays within a configured Start Menu root.</summary>
public static class StartMenuShortcutValidator
{
    public static bool IsAllowedShortcut(string? shortcutPath, IEnumerable<string> allowedRoots)
    {
        if (string.IsNullOrWhiteSpace(shortcutPath) || allowedRoots is null)
            return false;

        string fullShortcutPath;
        try
        {
            fullShortcutPath = Path.GetFullPath(shortcutPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (!string.Equals(Path.GetExtension(fullShortcutPath), ".lnk", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var root in allowedRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            string fullRoot;
            try
            {
                fullRoot = Path.GetFullPath(root);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            var rootPrefix = Path.EndsInDirectorySeparator(fullRoot)
                ? fullRoot
                : fullRoot + Path.DirectorySeparatorChar;
            if (fullShortcutPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
