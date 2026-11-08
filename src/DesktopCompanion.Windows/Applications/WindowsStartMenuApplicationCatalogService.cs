using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using DesktopCompanion.Core.Abstractions;
using DesktopCompanion.Core.Models;
using DesktopCompanion.Core.Sensors;

namespace DesktopCompanion.Windows.Applications;

/// <summary>
/// Discovers local Start Menu shortcuts and launches only shortcuts from the two
/// trusted Start Menu roots. It never accepts raw executable paths or commands.
/// </summary>
public sealed class WindowsStartMenuApplicationCatalogService : IApplicationCatalogService
{
    private const int MaximumApplications = 500;
    private const int MaximumDirectoryDepth = 8;
    private readonly object _sync = new();
    private readonly Dictionary<string, string> _shortcutPaths = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<InstalledApplication>> GetApplicationsAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(() => DiscoverApplications(cancellationToken), cancellationToken);

    public Task LaunchAsync(string applicationId, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            string shortcutPath;
            lock (_sync)
            {
                if (!_shortcutPaths.TryGetValue(applicationId, out var resolvedPath))
                    throw new KeyNotFoundException("This application is not in the current Start Menu catalogue.");
                shortcutPath = resolvedPath;
            }

            var roots = GetStartMenuRoots();
            if (!StartMenuShortcutValidator.IsAllowedShortcut(shortcutPath, roots) ||
                !File.Exists(shortcutPath))
                throw new InvalidOperationException("The selected Start Menu shortcut is no longer available.");

            cancellationToken.ThrowIfCancellationRequested();
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = shortcutPath,
                WorkingDirectory = Path.GetDirectoryName(shortcutPath)!,
                UseShellExecute = true
            });

            if (process is null)
                throw new InvalidOperationException("Windows did not start the selected application.");
        }, cancellationToken);

    private IReadOnlyList<InstalledApplication> DiscoverApplications(CancellationToken cancellationToken)
    {
        var roots = GetStartMenuRoots();
        var shortcuts = new List<InstalledApplication>();
        var discoveredPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var shortcutPath in EnumerateShortcuts(root, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!StartMenuShortcutValidator.IsAllowedShortcut(shortcutPath, roots))
                    continue;

                var name = Path.GetFileNameWithoutExtension(shortcutPath).Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var fullPath = Path.GetFullPath(shortcutPath);
                var id = CreateStableId(fullPath);
                if (!discoveredPaths.TryAdd(id, fullPath))
                    continue;

                shortcuts.Add(new InstalledApplication(id, name));
                if (shortcuts.Count >= MaximumApplications)
                    break;
            }

            if (shortcuts.Count >= MaximumApplications)
                break;
        }

        lock (_sync)
        {
            _shortcutPaths.Clear();
            foreach (var (id, path) in discoveredPaths)
                _shortcutPaths[id] = path;
        }

        return shortcuts
            .OrderBy(application => application.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateShortcuts(
        string root,
        CancellationToken cancellationToken)
    {
        bool rootExists;
        try
        {
            rootExists = Directory.Exists(root);
        }
        catch (Exception exception) when (IsFileSystemAccessException(exception))
        {
            rootExists = false;
        }

        if (!rootExists)
            yield break;

        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((root, 0));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (current, depth) = pending.Pop();

            // Directory enumeration is eager here so I/O errors are contained
            // per folder rather than escaping through the UI refresh operation.
            FileInfo[] files;
            try
            {
                files = new DirectoryInfo(current).GetFiles("*.lnk", SearchOption.TopDirectoryOnly);
            }
            catch (Exception exception) when (IsFileSystemAccessException(exception))
            {
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool isShortcut;
                try
                {
                    isShortcut = (file.Attributes & FileAttributes.ReparsePoint) == 0;
                }
                catch (Exception exception) when (IsFileSystemAccessException(exception))
                {
                    continue;
                }

                if (isShortcut)
                    yield return file.FullName;
            }

            if (depth >= MaximumDirectoryDepth)
                continue;

            DirectoryInfo[] directories;
            try
            {
                directories = new DirectoryInfo(current).GetDirectories();
            }
            catch (Exception exception) when (IsFileSystemAccessException(exception))
            {
                continue;
            }

            foreach (var directory in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool isRegularDirectory;
                try
                {
                    isRegularDirectory = (directory.Attributes & FileAttributes.ReparsePoint) == 0;
                }
                catch (Exception exception) when (IsFileSystemAccessException(exception))
                {
                    continue;
                }

                if (isRegularDirectory)
                    pending.Push((directory.FullName, depth + 1));
            }
        }
    }

    private static bool IsFileSystemAccessException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or System.Security.SecurityException;

    private static IReadOnlyList<string> GetStartMenuRoots() =>
        new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        }
    .Where(path => !string.IsNullOrWhiteSpace(path))
    .Select(path => Path.GetFullPath(path))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

    private static string CreateStableId(string shortcutPath)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(shortcutPath.ToUpperInvariant()));
        return Convert.ToHexString(bytes.AsSpan(0, 16)).ToLowerInvariant();
    }
}
