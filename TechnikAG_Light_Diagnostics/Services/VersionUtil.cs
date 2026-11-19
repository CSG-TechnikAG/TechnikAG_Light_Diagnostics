using System.Reflection;

namespace TechnikAG_Light_Diagnostics.Services;

public static class VersionUtil
{
    public static string CurrentAppVersion { get; } = ReadCurrentVersion();

    public static string Normalize(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "0.0.0";
        }

        var normalized = version.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var metadataIndex = normalized.IndexOf('+');
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        return normalized;
    }

    public static int Compare(string? left, string? right)
    {
        var leftParts = Parse(left);
        var rightParts = Parse(right);

        if (leftParts is null || rightParts is null)
        {
            return string.Compare(
                Normalize(left),
                Normalize(right),
                StringComparison.OrdinalIgnoreCase);
        }

        for (var i = 0; i < Math.Max(leftParts.Length, rightParts.Length); i++)
        {
            var leftPart = i < leftParts.Length ? leftParts[i] : 0;
            var rightPart = i < rightParts.Length ? rightParts[i] : 0;

            if (leftPart != rightPart)
            {
                return leftPart.CompareTo(rightPart);
            }
        }

        return 0;
    }

    public static bool IsNewer(string? candidate, string? current) => Compare(candidate, current) > 0;

    private static int[]? Parse(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var normalized = Normalize(version);
        var prereleaseIndex = normalized.IndexOf('-');
        if (prereleaseIndex >= 0)
        {
            normalized = normalized[..prereleaseIndex];
        }

        var rawParts = normalized.Split(
            new[] { '.', '_', '-' },
            StringSplitOptions.RemoveEmptyEntries);

        if (rawParts.Length == 0)
        {
            return null;
        }

        var parts = new List<int>(rawParts.Length);
        foreach (var rawPart in rawParts)
        {
            var digits = new string(rawPart.TakeWhile(char.IsDigit).ToArray());
            if (digits.Length == 0)
            {
                break;
            }

            parts.Add(int.Parse(digits));
        }

        return parts.Count == 0 ? null : parts.ToArray();
    }

    private static string ReadCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return Normalize(informationalVersion);
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}

public sealed class SemanticVersionComparer : IComparer<string>
{
    public static SemanticVersionComparer Instance { get; } = new();

    public int Compare(string? x, string? y) => VersionUtil.Compare(x, y);
}
