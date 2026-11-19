using System.IO;

namespace TechnikAG_Light_Diagnostics.Services;

public static class FixtureStore
{
    public static string FixturesPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "QLC+",
        "Fixtures");

    public static IReadOnlyList<string> FindInstalled()
    {
        try
        {
            if (!Directory.Exists(FixturesPath))
            {
                return Array.Empty<string>();
            }

            return Directory
                .EnumerateFiles(FixturesPath, "*.qxf", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static void EnsureDirectory() => Directory.CreateDirectory(FixturesPath);

    public static FixtureUninstallResult Uninstall(IEnumerable<string> fileNames)
    {
        var errors = new List<string>();
        var successCount = 0;

        foreach (var fileName in fileNames)
        {
            try
            {
                var path = Path.Combine(FixturesPath, fileName);
                if (!File.Exists(path))
                {
                    errors.Add($"{fileName}: file not found");
                    continue;
                }

                File.Delete(path);
                successCount++;
            }
            catch (Exception exception)
            {
                errors.Add($"{fileName}: {exception.Message}");
            }
        }

        return new FixtureUninstallResult(successCount, errors);
    }
}

public sealed record FixtureUninstallResult(int SuccessCount, IReadOnlyList<string> Errors)
{
    public int FailureCount => Errors.Count;
}
