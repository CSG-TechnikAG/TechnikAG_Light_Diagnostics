using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using TechnikAG_Light_Diagnostics.Dialogs;
using TechnikAG_Light_Diagnostics.Models;
using TechnikAG_Light_Diagnostics.Services;

namespace TechnikAG_Light_Diagnostics;

public partial class MainWindow : Window
{
    private readonly CdnClient _cdnClient = new();
    private readonly AppUpdateService _updateService;
    private readonly ObservableCollection<LogEntry> _activity = new();
    private readonly Dictionary<string, IReadOnlyList<CdnFile>> _qlcCatalog = new(StringComparer.OrdinalIgnoreCase);

    private bool _isBusy;
    private AppUpdateInfo? _availableUpdate;
    private IReadOnlyList<QlcInstallation> _qlcInstallations = Array.Empty<QlcInstallation>();

    public MainWindow()
    {
        InitializeComponent();

        _updateService = new AppUpdateService(_cdnClient);
        ActivityLog.ItemsSource = _activity;
        VersionBadge.Text = $"v{VersionUtil.CurrentAppVersion}";

        Loaded += MainWindow_Loaded;
        SourceInitialized += (_, _) => WindowAppearance.EnableDarkTitleBar(this);
        Closed += (_, _) => _cdnClient.Dispose();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Log($"Technik-AG Light Diagnostics v{VersionUtil.CurrentAppVersion} started.");
        Log("Version checks and downloads are provided by cdn.by-cra.net.");

        await RefreshLocalStatusAsync();
        await LoadQlcCatalogsAsync();
        await CheckForUpdatesAsync(userInitiated: false);
    }

    private async Task RefreshLocalStatusAsync()
    {
        try
        {
            _qlcInstallations = await Task.Run(QlcPlusScanner.FindInstallations);
            UpdateQlcStatuses();

            var fixtureCount = await Task.Run(() => FixtureStore.FindInstalled().Count);
            FixtureStatusText.Text = fixtureCount == 0
                ? "No fixture definitions installed for the current user."
                : $"{fixtureCount} fixture definition(s) installed in {FixtureStore.FixturesPath}.";

            var drivers = await Task.Run(FtdiDriverScanner.FindInstalledDrivers);
            DriverStatusText.Text = drivers.Count == 0
                ? "No FTDI driver package detected."
                : $"Detected {drivers.Count} FTDI driver package(s): " +
                  string.Join(" | ", drivers.Select(driver => driver.DisplayName));
        }
        catch (Exception exception)
        {
            Log($"Local status check failed: {exception.Message}");
        }
    }

    private async Task LoadQlcCatalogsAsync()
    {
        var tasks = QlcArchitectures.All.Select(async architecture =>
        {
            try
            {
                var files = await _cdnClient.GetFilesAsync(
                    architecture.CdnSource,
                    architecture.CdnPackage,
                    "windows");
                return (architecture.Id, Files: files);
            }
            catch (Exception exception)
            {
                Log($"Could not load {architecture.DisplayName} versions: {exception.Message}");
                return (architecture.Id, Files: (IReadOnlyList<CdnFile>)Array.Empty<CdnFile>());
            }
        });

        var results = await Task.WhenAll(tasks);
        foreach (var result in results)
        {
            _qlcCatalog[result.Id] = result.Files;
        }

        UpdateQlcStatuses();
        Log("Loaded QLC+ version catalogs: " +
            string.Join(", ", QlcArchitectures.All.Select(architecture =>
                $"{architecture.DisplayName} {GetLatestQlcVersion(architecture.Id) ?? "n/a"}")));
    }

    private void UpdateQlcStatuses()
    {
        foreach (var row in GetQlcRows())
        {
            var installed = _qlcInstallations
                .Where(installation => installation.ArchitectureId == row.Architecture.Id)
                .OrderByDescending(installation => installation.Version, SemanticVersionComparer.Instance)
                .FirstOrDefault();

            var latest = GetLatestQlcVersion(row.Architecture.Id);
            var status = installed switch
            {
                null when latest is null => "Not installed. Version list unavailable.",
                null => $"Not installed · CDN latest: {latest}",
                _ when latest is null => $"Installed: {installed.Version}",
                _ when VersionUtil.IsNewer(latest, installed.Version) =>
                    $"Installed: {installed.Version} · update available: {latest}",
                _ => $"Installed: {installed.Version} · up to date"
            };

            row.StatusText.Text = status;
            row.InstallButton.Content = installed is null
                ? "Install"
                : latest is not null && VersionUtil.IsNewer(latest, installed.Version)
                    ? "Update"
                    : "Reinstall";
        }
    }

    private IEnumerable<(QlcArchitecture Architecture, System.Windows.Controls.TextBlock StatusText, System.Windows.Controls.Button InstallButton)>
        GetQlcRows()
    {
        yield return (QlcArchitectures.QlcPlus4, QlcPlus4StatusText, QlcPlus4InstallButton);
        yield return (QlcArchitectures.QlcPlus5, QlcPlus5StatusText, QlcPlus5InstallButton);
        yield return (QlcArchitectures.QlcPlusPlus, QlcPlusCppStatusText, QlcPlusCppInstallButton);
    }

    private string? GetLatestQlcVersion(string architectureId)
    {
        if (!_qlcCatalog.TryGetValue(architectureId, out var files))
        {
            return null;
        }

        return files
            .Where(file => file.Available &&
                           file.FileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(file => file.Version, SemanticVersionComparer.Instance)
            .FirstOrDefault()?
            .Version;
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _isBusy = busy;

        foreach (var button in new[]
                 {
                     QlcPlus4InstallButton,
                     QlcPlus5InstallButton,
                     QlcPlusCppInstallButton,
                     QlcRefreshButton,
                     QlcUninstallButton,
                     FixtureInstallButton,
                     FixtureUninstallButton,
                     DriverInstallButton,
                     DriverUninstallButton,
                     CheckUpdatesButton,
                     UpdateNowButton
                 })
        {
            button.IsEnabled = !busy;
        }

        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ProgressDetailText.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;

        if (busy)
        {
            Progress.IsIndeterminate = true;
            Progress.Value = 0;
            ProgressDetailText.Text = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            StatusText.Text = status;
        }
    }

    private void SetStatus(string status) => StatusText.Text = status;

    private void UpdateProgress(DownloadProgress progress)
    {
        if (progress.Percentage is double percentage)
        {
            Progress.IsIndeterminate = false;
            Progress.Value = percentage;
            ProgressDetailText.Text =
                $"{progress.FileName} · {FormatBytes(progress.BytesReceived)} / " +
                $"{FormatBytes(progress.TotalBytes)} ({percentage:0}%)";
        }
        else
        {
            Progress.IsIndeterminate = true;
            ProgressDetailText.Text =
                $"{progress.FileName} · {FormatBytes(progress.BytesReceived)}";
        }
    }

    private async Task RunOperationAsync(
        string status,
        Func<IProgress<DownloadProgress>, Task> operation)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true, status);
        var progress = new Progress<DownloadProgress>(UpdateProgress);

        try
        {
            await operation(progress);
        }
        catch (Exception exception)
        {
            Log($"Error: {exception.Message}");
            MessageBox.Show(
                this,
                exception.Message,
                "Operation failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false, "Ready");
        }
    }

    private void Log(string message)
    {
        _activity.Add(new LogEntry($"[{DateTime.Now:HH:mm:ss}] {message}"));

        if (_activity.Count > 500)
        {
            _activity.RemoveAt(0);
        }

        ActivityLog.ScrollIntoView(_activity[^1]);
    }

    private void ShowCdnEmpty(string resourceName)
    {
        Log($"No {resourceName} found on the CDN.");
        MessageBox.Show(
            this,
            $"No {resourceName} are currently available from cdn.by-cra.net.\n\n" +
            "The CDN may not have been updated yet. Please try again later.",
            "Nothing available",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static string FormatBytes(long? bytes)
    {
        if (bytes is not long value)
        {
            return "?";
        }

        var units = new[] { "B", "KB", "MB", "GB" };
        double size = value;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private static string GetDriverLabel(string version) => version.ToLowerInvariant() switch
    {
        "jmsusb2dmx" => "JMS USB2DMX",
        "enttecdmxusbpro" => "ENTTEC DMX USB Pro",
        _ => version
    };

    private async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (_isBusy)
        {
            return;
        }

        if (userInitiated)
        {
            SetBusy(true, "Checking for updates on cdn.by-cra.net...");
        }

        try
        {
            var update = await _updateService.CheckForUpdateAsync();

            if (update is null)
            {
                _availableUpdate = null;
                UpdateBanner.Visibility = Visibility.Collapsed;
                Log($"No update available. v{VersionUtil.CurrentAppVersion} is the latest version.");

                if (userInitiated)
                {
                    MessageBox.Show(
                        this,
                        $"You are running the latest version (v{VersionUtil.CurrentAppVersion}).",
                        "Up to date",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            else
            {
                _availableUpdate = update;
                UpdateBannerText.Text =
                    $"Version {update.Version} is available. You are running v{VersionUtil.CurrentAppVersion}.";
                UpdateBanner.Visibility = Visibility.Visible;
                Log($"Update {update.Version} is available on cdn.by-cra.net.");
            }
        }
        catch (Exception exception)
        {
            Log($"Update check failed: {exception.Message}");

            if (userInitiated)
            {
                MessageBox.Show(
                    this,
                    $"Could not check for updates.\n\n{exception.Message}",
                    "Update check failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        finally
        {
            if (userInitiated)
            {
                SetBusy(false, "Ready");
            }
        }
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(userInitiated: true);
    }

    private async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        var update = _availableUpdate;
        if (update is null)
        {
            await CheckForUpdatesAsync(userInitiated: true);
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"Download and install Technik-AG Light Diagnostics v{update.Version}?\n\n" +
            "The application will close, replace itself and restart.\n" +
            "Your QLC+ and fixture data will not be touched.",
            "Install update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await RunOperationAsync($"Downloading update v{update.Version}...", async progress =>
        {
            var downloadedFile = await _updateService.DownloadUpdateAsync(update, progress);
            Log($"Update v{update.Version} downloaded. Restarting to apply it.");
            _updateService.LaunchUpdate(downloadedFile);
            Application.Current.Shutdown();
        });
    }

    private async void QlcPlus4Install_Click(object sender, RoutedEventArgs e) =>
        await InstallQlcAsync(QlcArchitectures.QlcPlus4);

    private async void QlcPlus5Install_Click(object sender, RoutedEventArgs e) =>
        await InstallQlcAsync(QlcArchitectures.QlcPlus5);

    private async void QlcPlusCppInstall_Click(object sender, RoutedEventArgs e) =>
        await InstallQlcAsync(QlcArchitectures.QlcPlusPlus);

    private async void QlcRefresh_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("Refreshing QLC+ versions on cdn.by-cra.net...", async _ =>
        {
            await _cdnClient.UpdatePackagesAsync();
            _qlcCatalog.Clear();
            await LoadQlcCatalogsAsync();
            await RefreshLocalStatusAsync();
            Log("QLC+ version index refreshed. New installers are mirrored by the CDN update run.");
        });
    }

    private async Task InstallQlcAsync(QlcArchitecture architecture)
    {
        await RunOperationAsync(
            $"Loading {architecture.DisplayName} versions from cdn.by-cra.net...",
            async progress =>
            {
                var files = await GetQlcFilesAsync(architecture);
                var groups = GroupQlcVersions(files);

                if (groups.Count == 0)
                {
                    ShowCdnEmpty($"{architecture.DisplayName} installers");
                    return;
                }

                var installed = _qlcInstallations
                    .Where(installation => installation.ArchitectureId == architecture.Id)
                    .OrderByDescending(installation => installation.Version, SemanticVersionComparer.Instance)
                    .FirstOrDefault();

                var options = groups
                    .Select(group => new SelectionOption(
                        $"{architecture.DisplayName} {group.Key}",
                        $"{group.First().FileName} · Windows x64" +
                        (installed is not null ? $" · installed: {installed.Version}" : string.Empty),
                        group.Key))
                    .ToList();

                var dialog = new SelectionDialog(
                    $"Install {architecture.DisplayName}",
                    $"Select a {architecture.DisplayName} version",
                    $"{architecture.Description} Downloads are served by cdn.by-cra.net.",
                    options,
                    multiSelect: false,
                    confirmText: "Install selected version")
                {
                    Owner = this
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                var selectedVersion = dialog.SelectedValues[0];
                var file = groups
                    .First(group => group.Key == selectedVersion)
                    .OrderBy(candidate => candidate.FileName, StringComparer.OrdinalIgnoreCase)
                    .First();

                SetStatus($"Downloading {architecture.DisplayName} {file.Version}...");
                var destination = Path.Combine(Path.GetTempPath(), file.FileName);

                try
                {
                    await _cdnClient.DownloadAsync(file, destination, progress);
                }
                catch
                {
                    ShellRunner.TryDelete(destination);
                    throw;
                }

                Log($"Downloaded {architecture.DisplayName} {file.Version} to {destination}.");

                var runInstaller = MessageBox.Show(
                    this,
                    $"{architecture.DisplayName} {file.Version} has been downloaded.\n\n" +
                    "Do you want to run the installer now?",
                    "Download complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (runInstaller == MessageBoxResult.Yes)
                {
                    ShellRunner.Start(destination);
                    Log($"{architecture.DisplayName} installer started.");
                    MessageBox.Show(
                        this,
                        $"The {architecture.DisplayName} installer has been launched.\n\n" +
                        "Follow the installation wizard to complete the setup.",
                        "Installer started",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        this,
                        $"The installer has been saved to:\n{destination}\n\nYou can run it manually later.",
                        "Installer saved",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                await RefreshLocalStatusAsync();
            });
    }

    private async Task<IReadOnlyList<CdnFile>> GetQlcFilesAsync(QlcArchitecture architecture)
    {
        if (_qlcCatalog.TryGetValue(architecture.Id, out var cached) && cached.Count > 0)
        {
            return cached;
        }

        var files = await _cdnClient.GetFilesAsync(
            architecture.CdnSource,
            architecture.CdnPackage,
            "windows");

        _qlcCatalog[architecture.Id] = files;
        UpdateQlcStatuses();
        return files;
    }

    private static List<IGrouping<string, CdnFile>> GroupQlcVersions(IReadOnlyList<CdnFile> files)
    {
        return files
            .Where(file => file.Available &&
                           file.FileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .GroupBy(file => file.Version)
            .OrderByDescending(group => group.Key, SemanticVersionComparer.Instance)
            .ToList();
    }

    private async void QlcUninstall_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("Searching for QLC+ installations...", async _ =>
        {
            var installations = await Task.Run(QlcPlusScanner.FindInstallations);

            if (installations.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "No QLC+ installations were found on this system.\n\n" +
                    "QLC+ might not be installed, or it may be in a non-standard location.",
                    "No installations found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var options = installations
                .Select(installation =>
                {
                    var architecture = QlcArchitectures.Find(installation.ArchitectureId);
                    var architectureName = architecture?.DisplayName ?? "QLC+";
                    return new SelectionOption(
                        $"{architectureName} {installation.Version}",
                        $"{installation.InstallPath} · {installation.SizeMB} MB · " +
                        $"installed {installation.InstallDate?.ToString("yyyy-MM-dd") ?? "unknown"}",
                        installation.InstallPath);
                })
                .ToList();

            var dialog = new SelectionDialog(
                "Uninstall QLC+ / QLC++",
                "Select an installation",
                "The vendor uninstaller is launched next. Administrator rights may be required.",
                options,
                multiSelect: false,
                confirmText: "Uninstall selected")
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var selected = installations.First(installation =>
                installation.InstallPath == dialog.SelectedValues[0]);
            var selectedArchitecture = QlcArchitectures.Find(selected.ArchitectureId);
            var selectedArchitectureName = selectedArchitecture?.DisplayName ?? "QLC+";

            var confirmation = MessageBox.Show(
                this,
                $"Uninstall {selectedArchitectureName} {selected.Version}?\n\n" +
                $"Installation path: {selected.InstallPath}\n\n" +
                "This launches the official uninstaller.",
                "Confirm uninstall",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            if (!File.Exists(selected.UninstallerPath))
            {
                MessageBox.Show(
                    this,
                    $"The uninstaller was not found at:\n{selected.UninstallerPath}\n\n" +
                    "The installation may have been moved or corrupted.",
                    "Uninstaller not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var process = ShellRunner.StartElevated(selected.UninstallerPath);
            Log($"{selectedArchitectureName} uninstaller started for {selected.InstallPath}.");
            MessageBox.Show(
                this,
                "The QLC+ uninstaller has been launched.\n\n" +
                "Follow the wizard to complete the removal.",
                "Uninstaller started",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (process is not null)
            {
                await ShellRunner.WaitForExitAsync(process);
            }

            await RefreshLocalStatusAsync();
        });
    }

    private async void FixtureInstall_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("Loading fixture definitions from cdn.by-cra.net...", async progress =>
        {
            var files = await _cdnClient.GetFilesAsync("cra", "qlc-fixtures");
            var groups = files
                .Where(file => file.Available &&
                               file.FileName.EndsWith(".qxf", StringComparison.OrdinalIgnoreCase))
                .GroupBy(file => file.Version)
                .OrderByDescending(group => group.Key, SemanticVersionComparer.Instance)
                .ToList();

            if (groups.Count == 0)
            {
                ShowCdnEmpty("fixture definitions");
                return;
            }

            var options = groups
                .Select(group => new SelectionOption(
                    $"Fixture pack {group.Key}",
                    $"{group.Count()} fixture definition(s)",
                    group.Key))
                .ToList();

            var dialog = new SelectionDialog(
                "Install fixture definitions",
                "Select one or more fixture packs",
                "All .qxf files in the selected packs are installed for the current Windows user.",
                options,
                multiSelect: true,
                confirmText: "Install selected packs",
                note: $"Destination: {FixtureStore.FixturesPath}")
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            FixtureStore.EnsureDirectory();

            var selectedVersions = dialog.SelectedValues.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var selectedFiles = groups
                .Where(group => selectedVersions.Contains(group.Key))
                .SelectMany(group => group)
                .GroupBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(file => file.Version, SemanticVersionComparer.Instance)
                    .First())
                .OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var installedCount = 0;
            var errors = new List<string>();

            foreach (var file in selectedFiles)
            {
                string? destination = null;
                try
                {
                    SetStatus($"Downloading fixture {file.FileName}...");
                    destination = Path.Combine(FixtureStore.FixturesPath, file.FileName);
                    await _cdnClient.DownloadAsync(file, destination, progress);
                    installedCount++;
                }
                catch (Exception exception)
                {
                    if (destination is not null)
                    {
                        ShellRunner.TryDelete(destination);
                    }

                    errors.Add($"{file.FileName}: {exception.Message}");
                }
            }

            Log($"Installed {installedCount} fixture definition(s).");

            var message =
                $"Fixture installation complete.\n\n" +
                $"Installed: {installedCount}\n" +
                $"Failed: {errors.Count}\n\n" +
                $"Destination: {FixtureStore.FixturesPath}";

            if (errors.Count > 0)
            {
                message += $"\n\nErrors:\n{string.Join("\n", errors.Take(10))}";
                if (errors.Count > 10)
                {
                    message += $"\n... and {errors.Count - 10} more";
                }
            }

            MessageBox.Show(
                this,
                message,
                "Fixture installation",
                MessageBoxButton.OK,
                errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            await RefreshLocalStatusAsync();
        });
    }

    private async void FixtureUninstall_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("Scanning installed fixture definitions...", async _ =>
        {
            var fixtures = await Task.Run(FixtureStore.FindInstalled);

            if (fixtures.Count == 0)
            {
                MessageBox.Show(
                    this,
                    $"No fixture definitions (.qxf files) were found in:\n{FixtureStore.FixturesPath}",
                    "No fixtures found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var options = fixtures
                .Select(fixture => new SelectionOption(
                    fixture,
                    "QLC+ fixture definition",
                    fixture))
                .ToList();

            var dialog = new SelectionDialog(
                "Uninstall fixture definitions",
                "Select fixture definitions to remove",
                "The selected files are deleted from the current user's QLC+ fixture folder.",
                options,
                multiSelect: true,
                confirmText: "Uninstall selected",
                note: $"Location: {FixtureStore.FixturesPath}")
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var confirmation = MessageBox.Show(
                this,
                $"Remove {dialog.SelectedValues.Count} fixture definition(s)?\n\n" +
                "This cannot be undone.",
                "Confirm uninstall",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            var result = await Task.Run(() => FixtureStore.Uninstall(dialog.SelectedValues));
            Log($"Removed {result.SuccessCount} fixture definition(s).");

            var message =
                $"Fixture uninstallation complete.\n\n" +
                $"Removed: {result.SuccessCount}\n" +
                $"Failed: {result.FailureCount}";

            if (result.Errors.Count > 0)
            {
                message += $"\n\nErrors:\n{string.Join("\n", result.Errors.Take(10))}";
            }

            MessageBox.Show(
                this,
                message,
                "Fixture uninstallation",
                MessageBoxButton.OK,
                result.FailureCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            await RefreshLocalStatusAsync();
        });
    }

    private async void DriverInstall_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("Loading DMX drivers from cdn.by-cra.net...", async progress =>
        {
            var files = await _cdnClient.GetFilesAsync("technikag-drivers", package: null, platform: "windows");
            var groups = files
                .Where(file => file.Available &&
                               file.FileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                .GroupBy(file => file.Package)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (groups.Count == 0)
            {
                ShowCdnEmpty("DMX driver installers");
                return;
            }

            var options = groups
                .Select(group => new SelectionOption(
                    GetDriverLabel(group.Key),
                    $"{group.First().FileName} · Windows x64",
                    group.Key))
                .ToList();

            var dialog = new SelectionDialog(
                "Install DMX drivers",
                "Select a driver package",
                "Driver installers are downloaded from cdn.by-cra.net and run with administrator rights.",
                options,
                multiSelect: false,
                confirmText: "Install selected driver")
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var selectedPackage = dialog.SelectedValues[0];
            var file = groups.First(group => group.Key == selectedPackage).First();
            var destination = Path.Combine(
                Path.GetTempPath(),
                $"driver_install_{file.Package}.exe");

            SetStatus($"Downloading {GetDriverLabel(file.Package)}...");
            await _cdnClient.DownloadAsync(file, destination, progress);
            Log($"Downloaded {GetDriverLabel(file.Package)} to {destination}.");

            var confirmation = MessageBox.Show(
                this,
                $"{GetDriverLabel(file.Package)} has been downloaded.\n\n" +
                "The installer will be started in interactive mode (/ask).\n" +
                "Administrator permissions may be required.\n\n" +
                "Run the installer now?",
                "Download complete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
            {
                MessageBox.Show(
                    this,
                    $"The installer has been saved to:\n{destination}\n\n" +
                    "You can run it manually later with the /ask parameter.",
                    "Installer saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var process = ShellRunner.StartElevated(destination, "/ask");
            Log($"Driver installer started: {GetDriverLabel(file.Package)}.");
            MessageBox.Show(
                this,
                "The driver installer has been launched.\n\n" +
                "Follow the wizard prompts to finish the installation.",
                "Installer started",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (process is not null)
            {
                await ShellRunner.WaitForExitAsync(process);
                ShellRunner.TryDelete(destination);
                MessageBox.Show(
                    this,
                    "Driver installation has finished.",
                    "Installation complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            await RefreshLocalStatusAsync();
        });
    }

    private async void DriverUninstall_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("Scanning installed FTDI drivers...", async _ =>
        {
            var drivers = await Task.Run(FtdiDriverScanner.FindInstalledDrivers);

            if (drivers.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "No FTDI drivers were found in the system.\n\n" +
                    "The drivers may not be installed, or they may be registered under a different name.",
                    "No drivers found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var options = drivers
                .Select(driver => new SelectionOption(
                    driver.DisplayName,
                    $"v{driver.Version} · {driver.Publisher}",
                    driver.UninstallString))
                .ToList();

            var dialog = new SelectionDialog(
                "Uninstall FTDI drivers",
                "Select driver packages to remove",
                "Uninstalling drivers can affect connected USB-DMX hardware.",
                options,
                multiSelect: true,
                confirmText: "Uninstall selected",
                note: "Tip: hold Ctrl to select multiple packages.")
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var selectedDrivers = drivers
                .Where(driver => dialog.SelectedValues.Contains(driver.UninstallString))
                .ToList();

            var confirmation = MessageBox.Show(
                this,
                $"Uninstall {selectedDrivers.Count} FTDI driver package(s)?\n\n" +
                "This may affect connected hardware devices.\n" +
                "The uninstallers require administrator permissions.",
                "Confirm driver uninstall",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            var successCount = 0;
            var errors = new List<string>();

            foreach (var driver in selectedDrivers)
            {
                try
                {
                    var (fileName, arguments) = ParseUninstallCommand(driver.UninstallString);
                    var process = ShellRunner.StartElevated(fileName, arguments);

                    if (process is not null)
                    {
                        await ShellRunner.WaitForExitAsync(process, timeoutMilliseconds: 30000);
                    }

                    successCount++;
                }
                catch (Exception exception)
                {
                    errors.Add($"{driver.DisplayName}: {exception.Message}");
                }
            }

            Log($"Launched {successCount} FTDI driver uninstaller(s).");

            var message =
                $"Driver uninstallation started.\n\n" +
                $"Uninstallers launched: {successCount}\n" +
                $"Failed: {errors.Count}\n\n" +
                "Some uninstallers may still be running. Follow any remaining prompts.";

            if (errors.Count > 0)
            {
                message += $"\n\nErrors:\n{string.Join("\n", errors.Take(5))}";
            }

            MessageBox.Show(
                this,
                message,
                "Driver uninstallation",
                MessageBoxButton.OK,
                errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            await RefreshLocalStatusAsync();
        });
    }

    private static (string FileName, string Arguments) ParseUninstallCommand(string uninstallString)
    {
        var (fileName, arguments) = ShellRunner.ParseCommandLine(uninstallString);

        if (fileName.Contains("msiexec", StringComparison.OrdinalIgnoreCase) &&
            !arguments.Contains("/q", StringComparison.OrdinalIgnoreCase))
        {
            arguments = $"{arguments} /qb".Trim();
        }

        return (fileName, arguments);
    }
}
