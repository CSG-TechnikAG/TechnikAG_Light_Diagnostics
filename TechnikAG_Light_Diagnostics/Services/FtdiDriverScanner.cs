using Microsoft.Win32;
using TechnikAG_Light_Diagnostics.Models;

namespace TechnikAG_Light_Diagnostics.Services;

public static class FtdiDriverScanner
{
    private static readonly string[] UninstallRegistryPaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    public static IReadOnlyList<FtdiDriver> FindInstalledDrivers()
    {
        var drivers = new List<FtdiDriver>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddFromHive(Registry.LocalMachine, drivers, seen);
        AddFromHive(Registry.CurrentUser, drivers, seen);

        return drivers
            .OrderBy(driver => driver.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddFromHive(
        RegistryKey hive,
        List<FtdiDriver> drivers,
        HashSet<string> seen)
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
                            !displayName.Contains("FTDI", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var uninstallString = subKey!.GetValue("UninstallString")?.ToString();
                        if (string.IsNullOrWhiteSpace(uninstallString))
                        {
                            continue;
                        }

                        var uniqueKey = $"{displayName}|{uninstallString}";
                        if (!seen.Add(uniqueKey))
                        {
                            continue;
                        }

                        drivers.Add(new FtdiDriver
                        {
                            DisplayName = displayName,
                            Version = subKey.GetValue("DisplayVersion")?.ToString() ?? "Unknown",
                            Publisher = subKey.GetValue("Publisher")?.ToString() ?? "Unknown",
                            UninstallString = uninstallString,
                            RegistryKey = $"{registryPath}\\{subKeyName}"
                        });
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
}
