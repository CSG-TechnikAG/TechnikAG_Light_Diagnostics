using System.Diagnostics;
using System.IO;
using System.Text;
using TechnikAG_Light_Diagnostics.Models;

namespace TechnikAG_Light_Diagnostics.Services;

public sealed class AppUpdateService
{
    public const string UpdateSource = "technikag-apps";
    public const string UpdatePackage = "TechnikAG_Light_Diagnostics";

    private readonly CdnClient _cdnClient;

    public AppUpdateService(CdnClient cdnClient)
    {
        _cdnClient = cdnClient;
    }

    public async Task<AppUpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var files = await _cdnClient
            .GetFilesAsync(UpdateSource, UpdatePackage, "windows", "x64", cancellationToken)
            .ConfigureAwait(false);

        var latest = files
            .Where(file => file.Available &&
                           file.FileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(file => file.Version, SemanticVersionComparer.Instance)
            .FirstOrDefault();

        if (latest is null || !VersionUtil.IsNewer(latest.Version, VersionUtil.CurrentAppVersion))
        {
            return null;
        }

        return new AppUpdateInfo(VersionUtil.Normalize(latest.Version), latest);
    }

    public async Task<string> DownloadUpdateAsync(
        AppUpdateInfo update,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var safeVersion = string.Concat(update.Version.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var destination = Path.Combine(
            Path.GetTempPath(),
            $"TechnikAG_Light_Diagnostics_{safeVersion}.exe");

        if (File.Exists(destination))
        {
            File.Delete(destination);
        }

        await _cdnClient
            .DownloadAsync(update.File, destination, progress, cancellationToken)
            .ConfigureAwait(false);

        return destination;
    }

    public void LaunchUpdate(string downloadedFile)
    {
        var currentExecutable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExecutable))
        {
            throw new InvalidOperationException("Could not determine the current application path.");
        }

        if (!File.Exists(downloadedFile))
        {
            throw new FileNotFoundException("The downloaded update could not be found.", downloadedFile);
        }

        var scriptPath = Path.Combine(
            Path.GetTempPath(),
            $"technikag-light-diagnostics-update-{Guid.NewGuid():N}.ps1");

        var script = BuildUpdateScript(
            Environment.ProcessId,
            currentExecutable,
            downloadedFile);

        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var powershellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");

        if (!File.Exists(powershellPath))
        {
            powershellPath = "powershell.exe";
        }

        var requiresElevation = IsInsideProgramFiles(currentExecutable);
        var startInfo = new ProcessStartInfo
        {
            FileName = powershellPath,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = requiresElevation,
            CreateNoWindow = !requiresElevation,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (requiresElevation)
        {
            startInfo.Verb = "runas";
        }

        var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Could not start the update helper.");
        }
    }

    private static string BuildUpdateScript(int processId, string targetPath, string sourcePath)
    {
        static string Quote(string value) => value.Replace("'", "''");

        return $$"""
                $ErrorActionPreference = 'Stop'
                $processId = {{processId}}
                $target = '{{Quote(targetPath)}}'
                $source = '{{Quote(sourcePath)}}'

                while (Get-Process -Id $processId -ErrorAction SilentlyContinue) {
                    Start-Sleep -Milliseconds 400
                }

                $copied = $false
                for ($attempt = 0; $attempt -lt 30; $attempt++) {
                    try {
                        Copy-Item -LiteralPath $source -Destination $target -Force
                        $copied = $true
                        break
                    } catch {
                        Start-Sleep -Milliseconds 500
                    }
                }

                if (-not $copied) {
                    Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
                    Start-Process -FilePath $target -WorkingDirectory (Split-Path -Path $target -Parent)
                    exit 1
                }

                Remove-Item -LiteralPath $source -Force -ErrorAction SilentlyContinue
                Start-Process -FilePath $target -WorkingDirectory (Split-Path -Path $target -Parent)
                Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
                """;
    }

    private static bool IsInsideProgramFiles(string executablePath)
    {
        var directory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        foreach (var programFiles in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            if (string.IsNullOrWhiteSpace(programFiles))
            {
                continue;
            }

            var root = programFiles.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
