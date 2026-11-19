using System.Diagnostics;
using System.IO;

namespace TechnikAG_Light_Diagnostics.Services;

public static class ShellRunner
{
    public static (string FileName, string Arguments) ParseCommandLine(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return (string.Empty, string.Empty);
        }

        var command = commandLine.Trim();

        if (command.StartsWith('"'))
        {
            var closingQuote = command.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                return (command[1..closingQuote], command[(closingQuote + 1)..].Trim());
            }
        }

        var exeIndex = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            return (command[..(exeIndex + 4)], command[(exeIndex + 4)..].Trim());
        }

        if (command.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
        {
            var arguments = command
                .Replace("msiexec.exe", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("msiexec", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            return ("msiexec.exe", arguments);
        }

        return (command.Trim('"'), string.Empty);
    }

    public static Process? Start(string fileName, string? arguments = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true
        };

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            startInfo.Arguments = arguments;
        }

        return Process.Start(startInfo);
    }

    public static Process? StartElevated(string fileName, string? arguments = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true,
            Verb = "runas"
        };

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            startInfo.Arguments = arguments;
        }

        return Process.Start(startInfo);
    }

    public static async Task<int?> WaitForExitCodeAsync(Process process, int timeoutMilliseconds = -1)
    {
        try
        {
            if (timeoutMilliseconds < 0)
            {
                await process.WaitForExitAsync();
            }
            else
            {
                using var cancellationTokenSource = new CancellationTokenSource(timeoutMilliseconds);
                await process.WaitForExitAsync(cancellationTokenSource.Token);
            }

            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static async Task WaitForExitAsync(Process process, int timeoutMilliseconds = -1)
    {
        try
        {
            if (timeoutMilliseconds < 0)
            {
                await process.WaitForExitAsync();
                return;
            }

            using var cancellationTokenSource = new CancellationTokenSource(timeoutMilliseconds);
            await process.WaitForExitAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // The uninstaller is still running; that is fine.
        }
        catch
        {
            // The process may have already exited.
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A leftover temp file is harmless.
        }
    }
}
