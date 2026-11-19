using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using TechnikAG_Light_Diagnostics.Models;

namespace TechnikAG_Light_Diagnostics.Services;

public static class QlcPlusScanner
{
    private static readonly string[] UninstallRegistryPaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    public static IReadOnlyList<QlcInstallation> FindInstallations()
    {
        var installations = new List<QlcInstallation>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidatePaths = new List<string>();

        void AddCandidate(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!candidatePaths.Any(existing =>
                    existing.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                candidatePaths.Add(path);
            }
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        AddCandidate(Path.Combine(programFiles, "QLC+"));
        AddCandidate(Path.Combine(programFilesX86, "QLC+"));
        AddCandidate(Path.Combine(localAppData, "QLC+"));
        AddCandidate(@"C:\QLC++");
        AddCandidate(@"C:\QLC+");
        AddCandidate(@"C:\QLC");
        AddCandidate(@"C:\qlcplus");

        TryAddMatchingDirectories(@"C:\", candidatePaths);
        TryAddMatchingDirectories(programFiles, candidatePaths);
        TryAddMatchingDirectories(programFilesX86, candidatePaths);

        foreach (var path in candidatePaths)
        {
            AddFromDirectory(path, installations, seenPaths);
        }

        AddFromRegistry(Registry.LocalMachine, installations, seenPaths);
        AddFromRegistry(Registry.CurrentUser, installations, seenPaths);

        return installations
            .OrderByDescending(installation => installation.Version, SemanticVersionComparer.Instance)
            .ToList();
    }

    private static void TryAddMatchingDirectories(string root, List<string> paths)
    {
        try
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            foreach (var directory in new DirectoryInfo(root).GetDirectories())
            {
                try
                {
                    if (directory.Name.Contains("qlc", StringComparison.OrdinalIgnoreCase) ||
                        directory.Name.Contains("qplus", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!paths.Any(path =>
                                path.Equals(directory.FullName, StringComparison.OrdinalIgnoreCase)))
                        {
                            paths.Add(directory.FullName);
                        }
                    }
                }
                catch
                {
                    // Ignore directories we cannot inspect.
                }
            }
        }
        catch
        {
            // The root may be inaccessible.
        }
    }

    private static void AddFromDirectory(
        string installPath,
        List<QlcInstallation> installations,
        HashSet<string> seenPaths)
    {
        try
        {
            if (!Directory.Exists(installPath) || !seenPaths.Add(installPath))
            {
                return;
            }

            var executablePath = FindExecutable(installPath);
            var uninstallerPath = Path.Combine(installPath, "uninstall.exe");
            if (executablePath is null || !File.Exists(uninstallerPath))
            {
                return;
            }

            installations.Add(CreateInstallation(
                installPath,
                executablePath,
                uninstallerPath,
                fallbackVersion: null));
        }
        catch
        {
            // Ignore inaccessible installations.
        }
    }

    private static void AddFromRegistry(
        RegistryKey hive,
        List<QlcInstallation> installations,
        HashSet<string> seenPaths)
    {
        foreach (var registryPath in UninstallRegistryPaths)
        {
            try
            {
                using var uninstallKey = hive.OpenSubKey(registryPath);
                if (uninstallKey is null)
                {
                    continue;
                }

                foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = uninstallKey.OpenSubKey(subKeyName);
                        var displayName = subKey?.GetValue("DisplayName")?.ToString();
                        if (displayName is null ||
                            (!displayName.Contains("QLC+", StringComparison.OrdinalIgnoreCase) &&
                             !displayName.Contains("QLC++", StringComparison.OrdinalIgnoreCase) &&
                             !displayName.Contains("Q Light Controller", StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        var installLocation = subKey!.GetValue("InstallLocation")?.ToString();
                        if (string.IsNullOrWhiteSpace(installLocation) ||
                            !Directory.Exists(installLocation) ||
                            !seenPaths.Add(installLocation))
                        {
                            continue;
                        }

                        var executablePath = FindExecutable(installLocation);
                        if (executablePath is null)
                        {
                            continue;
                        }

                        var uninstallString = subKey.GetValue("UninstallString")?.ToString();
                        var uninstallerPath = string.IsNullOrWhiteSpace(uninstallString)
                            ? Path.Combine(installLocation, "uninstall.exe")
                            : ShellRunner.ParseCommandLine(uninstallString).FileName;

                        if (!File.Exists(uninstallerPath))
                        {
                            continue;
                        }

                        var fallbackVersion = subKey.GetValue("DisplayVersion")?.ToString();
                        installations.Add(CreateInstallation(
                            installLocation,
                            executablePath,
                            uninstallerPath,
                            fallbackVersion));
                    }
                    catch
                    {
                        // Ignore malformed registry entries.
                    }
                }
            }
            catch
            {
                // The registry hive or key may be inaccessible.
            }
        }
    }

    private static QlcInstallation CreateInstallation(
        string installPath,
        string executablePath,
        string uninstallerPath,
        string? fallbackVersion)
    {
        var fileInfo = new FileInfo(executablePath);
        var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
        var version = !string.IsNullOrWhiteSpace(versionInfo.FileVersion)
            ? versionInfo.FileVersion
            : fallbackVersion ?? "Unknown";

        var directoryInfo = new DirectoryInfo(installPath);
        var sizeMb = GetDirectorySize(directoryInfo) / (1024 * 1024);

        return new QlcInstallation
        {
            InstallPath = installPath,
            ExecutablePath = executablePath,
            ArchitectureId = DetectArchitecture(installPath, executablePath, version),
            Version = version,
            UninstallerPath = uninstallerPath,
            SizeMB = sizeMb,
            InstallDate = fileInfo.CreationTime
        };
    }

    private static string DetectArchitecture(string installPath, string executablePath, string version)
    {
        var executableName = Path.GetFileName(executablePath);

        if (installPath.Contains("QLC++", StringComparison.OrdinalIgnoreCase))
        {
            return QlcArchitectures.QlcPlusPlus.Id;
        }

        if (installPath.Contains("QLC+5", StringComparison.OrdinalIgnoreCase) ||
            executableName.StartsWith("qlcplus5", StringComparison.OrdinalIgnoreCase) ||
            executableName.Contains("qlcplus-qml", StringComparison.OrdinalIgnoreCase) ||
            IsVersionAtLeast(version, "5.0"))
        {
            return QlcArchitectures.QlcPlus5.Id;
        }

        // The TechnikAG QLC++ builds continue the 4.x line from 4.15 onwards and
        // install to C:\QLC++. Older QLC++ installations in C:\QLC+ are still
        // recognised by their 4.15+ version number.
        if (IsVersionAtLeast(version, "4.15"))
        {
            return QlcArchitectures.QlcPlusPlus.Id;
        }

        return QlcArchitectures.QlcPlus4.Id;
    }

    private static bool IsVersionAtLeast(string version, string minimum)
    {
        var firstPart = VersionUtil.Normalize(version).Split('.')[0];
        return int.TryParse(firstPart, out _) && VersionUtil.Compare(version, minimum) >= 0;
    }

    private static string? FindExecutable(string installPath)
    {
        foreach (var executableName in new[]
                 {
                     "qlcplus5.exe",
                     "qlcplus.exe",
                     "qlcplus-qml.exe"
                 })
        {
            var candidate = Path.Combine(installPath, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static long GetDirectorySize(DirectoryInfo directory)
    {
        try
        {
            long size = 0;

            foreach (var file in directory.GetFiles())
            {
                size += file.Length;
            }

            foreach (var subDirectory in directory.GetDirectories())
            {
                size += GetDirectorySize(subDirectory);
            }

            return size;
        }
        catch
        {
            return 0;
        }
    }
}
