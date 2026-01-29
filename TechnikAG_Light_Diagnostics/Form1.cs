using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace TechnikAG_Light_Diagnostics
{
    public partial class Form1 : Form
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public Form1()
        {
            InitializeComponent();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "TechnikAG-Light-Diagnostics");
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            installQLCplus();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            uninstallQLCplus();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            installFixtureDefinitions();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            uninstallFixtureDefinitions();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            installDrivers();
        }

        private void button7_Click(object sender, EventArgs e)
        {
            uninstallDrivers();
        }

        private async void installQLCplus()
        {
            try
            {
                using (var loadingForm = new Form())
                {
                    loadingForm.Text = "QLC+ Installer";
                    loadingForm.Size = new Size(400, 150);
                    loadingForm.StartPosition = FormStartPosition.CenterParent;
                    loadingForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    loadingForm.MaximizeBox = false;
                    loadingForm.MinimizeBox = false;
                    loadingForm.BackColor = Color.Black;

                    var loadingLabel = new Label
                    {
                        Text = "Fetching available QLC+ versions from GitHub...",
                        AutoSize = false,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Segoe UI", 10F),
                        ForeColor = Color.White
                    };
                    loadingForm.Controls.Add(loadingLabel);

                    loadingForm.Show(this);
                    loadingForm.Refresh();

                    var releases = await FetchGitHubReleases();
                    loadingForm.Close();

                    if (releases == null || releases.Count == 0)
                    {
                        MessageBox.Show("Failed to fetch QLC+ releases from GitHub. Please check your internet connection and try again.",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var selectedVersion = ShowVersionSelector(releases);
                    if (string.IsNullOrEmpty(selectedVersion))
                    {
                        return;
                    }

                    await DownloadAndInstall(selectedVersion);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<List<string>> FetchGitHubReleases()
        {
            try
            {
                var response = await httpClient.GetAsync("https://api.github.com/repos/mcallegari/qlcplus/releases");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var releases = new List<string>();

                foreach (var release in document.RootElement.EnumerateArray())
                {
                    if (release.TryGetProperty("tag_name", out var tagName))
                    {
                        var version = tagName.GetString();
                        if (!string.IsNullOrEmpty(version))
                        {
                            version = version.TrimStart('v', 'V');

                            if (version.StartsWith("QLC+_", StringComparison.OrdinalIgnoreCase))
                            {
                                version = version.Substring(5);
                            }

                            releases.Add(version);
                        }
                    }
                }

                return releases;
            }
            catch
            {
                return null;
            }
        }

        private string ShowVersionSelector(List<string> versions)
        {
            using (var selectorForm = new Form())
            {
                selectorForm.Text = "Select QLC+ Version";
                selectorForm.Size = new Size(450, 400);
                selectorForm.StartPosition = FormStartPosition.CenterParent;
                selectorForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                selectorForm.MaximizeBox = false;
                selectorForm.MinimizeBox = false;
                selectorForm.BackColor = Color.Black;
                selectorForm.ForeColor = Color.White;

                var instructionLabel = new Label
                {
                    Text = "Please select the QLC+ version you want to install:",
                    Location = new Point(20, 20),
                    Size = new Size(400, 30),
                    Font = new Font("Segoe UI", 10F),
                    ForeColor = Color.White
                };
                selectorForm.Controls.Add(instructionLabel);

                var listBox = new ListBox
                {
                    Location = new Point(20, 60),
                    Size = new Size(400, 240),
                    Font = new Font("Segoe UI", 10F),
                    BackColor = Color.FromArgb(32, 32, 32),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                foreach (var version in versions)
                {
                    listBox.Items.Add(version);
                }

                if (listBox.Items.Count > 0)
                {
                    listBox.SelectedIndex = 0;
                }

                selectorForm.Controls.Add(listBox);

                var installButton = new Button
                {
                    Text = "Install Selected Version",
                    Location = new Point(20, 315),
                    Size = new Size(180, 35),
                    DialogResult = DialogResult.OK,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Lime,
                    BackColor = Color.Black
                };
                installButton.FlatAppearance.BorderColor = Color.Lime;
                installButton.FlatAppearance.MouseOverBackColor = Color.DarkGreen;
                installButton.FlatAppearance.MouseDownBackColor = Color.Green;
                selectorForm.Controls.Add(installButton);

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    Location = new Point(220, 315),
                    Size = new Size(100, 35),
                    DialogResult = DialogResult.Cancel,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Red,
                    BackColor = Color.Black
                };
                cancelButton.FlatAppearance.BorderColor = Color.Red;
                cancelButton.FlatAppearance.MouseOverBackColor = Color.DarkRed;
                cancelButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(192, 0, 0);
                selectorForm.Controls.Add(cancelButton);

                selectorForm.AcceptButton = installButton;
                selectorForm.CancelButton = cancelButton;

                listBox.DoubleClick += (s, e) => { selectorForm.DialogResult = DialogResult.OK; selectorForm.Close(); };

                if (selectorForm.ShowDialog(this) == DialogResult.OK && listBox.SelectedItem != null)
                {
                    return listBox.SelectedItem.ToString();
                }

                return null;
            }
        }

        private async Task DownloadAndInstall(string version)
        {
            var downloadUrl = $"https://www.qlcplus.org/downloads/{version}/QLC+_{version}.exe";
            var tempPath = Path.Combine(Path.GetTempPath(), $"QLC+_{version}.exe");

            using (var progressForm = new Form())
            {
                progressForm.Text = "Downloading QLC+";
                progressForm.Size = new Size(500, 200);
                progressForm.StartPosition = FormStartPosition.CenterParent;
                progressForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                progressForm.MaximizeBox = false;
                progressForm.MinimizeBox = false;
                progressForm.ControlBox = false;
                progressForm.BackColor = Color.Black;
                progressForm.ForeColor = Color.White;

                var statusLabel = new Label
                {
                    Text = $"Downloading QLC+ version {version}...",
                    Location = new Point(20, 20),
                    Size = new Size(450, 30),
                    Font = new Font("Segoe UI", 10F),
                    ForeColor = Color.White
                };
                progressForm.Controls.Add(statusLabel);

                var progressBar = new ProgressBar
                {
                    Location = new Point(20, 60),
                    Size = new Size(450, 30),
                    Style = ProgressBarStyle.Continuous,
                    ForeColor = Color.Lime
                };
                progressForm.Controls.Add(progressBar);

                var detailsLabel = new Label
                {
                    Text = "Preparing download...",
                    Location = new Point(20, 100),
                    Size = new Size(450, 30),
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.Gray
                };
                progressForm.Controls.Add(detailsLabel);

                progressForm.Show(this);
                progressForm.Refresh();

                try
                {
                    var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                    if (!response.IsSuccessStatusCode)
                    {
                        progressForm.Close();
                        MessageBox.Show($"Failed to download QLC+ {version}. The version might not be available at:\n{downloadUrl}\n\nPlease try a different version.",
                            "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                var percentage = (int)((totalRead * 100) / totalBytes);
                                progressBar.Value = Math.Min(percentage, 100);
                                var mbRead = totalRead / (1024.0 * 1024.0);
                                var mbTotal = totalBytes / (1024.0 * 1024.0);
                                detailsLabel.Text = $"Downloaded {mbRead:F2} MB of {mbTotal:F2} MB ({percentage}%)";
                            }
                            else
                            {
                                detailsLabel.Text = $"Downloaded {totalRead / (1024.0 * 1024.0):F2} MB";
                            }

                            progressForm.Refresh();
                        }
                    }

                    progressForm.Close();

                    var result = MessageBox.Show($"QLC+ {version} has been downloaded successfully!\n\nDo you want to run the installer now?",
                        "Download Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                    {
                        var processInfo = new ProcessStartInfo
                        {
                            FileName = tempPath,
                            UseShellExecute = true
                        };
                        Process.Start(processInfo);

                        MessageBox.Show("The QLC+ installer has been launched.\n\nPlease follow the installation wizard to complete the setup.",
                            "Installer Started", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show($"The installer has been saved to:\n{tempPath}\n\nYou can run it manually later.",
                            "Installation Skipped", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    progressForm.Close();
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                    throw new Exception($"Download failed: {ex.Message}", ex);
                }
            }
        }

        private class QlcInstallation
        {
            public string InstallPath { get; set; }
            public string Version { get; set; }
            public string UninstallerPath { get; set; }
            public long SizeMB { get; set; }
            public DateTime? InstallDate { get; set; }

            public override string ToString()
            {
                return $"QLC+ {Version} - {InstallPath}";
            }
        }

        private async void uninstallQLCplus()
        {
            try
            {
                using (var loadingForm = new Form())
                {
                    loadingForm.Text = "QLC+ Uninstaller";
                    loadingForm.Size = new Size(400, 150);
                    loadingForm.StartPosition = FormStartPosition.CenterParent;
                    loadingForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    loadingForm.MaximizeBox = false;
                    loadingForm.MinimizeBox = false;
                    loadingForm.BackColor = Color.Black;

                    var loadingLabel = new Label
                    {
                        Text = "Searching for QLC+ installations...",
                        AutoSize = false,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Segoe UI", 10F),
                        ForeColor = Color.White
                    };
                    loadingForm.Controls.Add(loadingLabel);

                    loadingForm.Show(this);
                    loadingForm.Refresh();

                    var installations = await Task.Run(() => FindQlcInstallations());
                    loadingForm.Close();

                    if (installations == null || installations.Count == 0)
                    {
                        MessageBox.Show("No QLC+ installations found on this system.\n\nQLC+ might not be installed, or it may be in a non-standard location.",
                            "No Installations Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var selectedInstallation = ShowInstallationSelector(installations);
                    if (selectedInstallation == null)
                    {
                        return;
                    }

                    await UninstallQlc(selectedInstallation);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private List<QlcInstallation> FindQlcInstallations()
        {
            var installations = new List<QlcInstallation>();
            var searchPaths = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "QLC+"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "QLC+"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QLC+"),
                @"C:\QLC+",
                @"C:\QLC",
                @"C:\qlcplus"
            };

            foreach (var searchPath in searchPaths)
            {
                try
                {
                    if (Directory.Exists(searchPath))
                    {
                        CheckAndAddInstallation(searchPath, installations);
                    }
                }
                catch
                {
                    continue;
                }
            }

            try
            {
                var cDrive = new DirectoryInfo(@"C:\");
                foreach (var dir in cDrive.GetDirectories())
                {
                    try
                    {
                        if (dir.Name.Contains("qlc", StringComparison.OrdinalIgnoreCase) ||
                            dir.Name.Contains("qplus", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!searchPaths.Any(p => p.Equals(dir.FullName, StringComparison.OrdinalIgnoreCase)))
                            {
                                CheckAndAddInstallation(dir.FullName, installations);
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch
            {
            }

            try
            {
                foreach (var programFilesPath in new[] {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                })
                {
                    if (Directory.Exists(programFilesPath))
                    {
                        var programFilesDir = new DirectoryInfo(programFilesPath);
                        foreach (var dir in programFilesDir.GetDirectories())
                        {
                            try
                            {
                                if (dir.Name.Contains("qlc", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!searchPaths.Any(p => p.Equals(dir.FullName, StringComparison.OrdinalIgnoreCase)) &&
                                        !installations.Any(i => i.InstallPath.Equals(dir.FullName, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        CheckAndAddInstallation(dir.FullName, installations);
                                    }
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                var displayName = subKey?.GetValue("DisplayName")?.ToString();
                                if (displayName != null && displayName.Contains("QLC+", StringComparison.OrdinalIgnoreCase))
                                {
                                    var installLocation = subKey.GetValue("InstallLocation")?.ToString();
                                    var uninstallString = subKey.GetValue("UninstallString")?.ToString();

                                    if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                                    {
                                        var qlcExePath = Path.Combine(installLocation, "qlcplus.exe");
                                        var qlcQmlExePath = Path.Combine(installLocation, "qlcplus-qml.exe");

                                        string actualExePath = null;
                                        if (File.Exists(qlcExePath))
                                        {
                                            actualExePath = qlcExePath;
                                        }
                                        else if (File.Exists(qlcQmlExePath))
                                        {
                                            actualExePath = qlcQmlExePath;
                                        }

                                        if (actualExePath != null && !installations.Any(i => i.InstallPath.Equals(installLocation, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            var fileInfo = new FileInfo(actualExePath);
                                            var versionInfo = FileVersionInfo.GetVersionInfo(actualExePath);

                                            var dirInfo = new DirectoryInfo(installLocation);
                                            var totalSize = GetDirectorySize(dirInfo) / (1024 * 1024);

                                            string uninstallerPath = null;
                                            if (!string.IsNullOrEmpty(uninstallString))
                                            {
                                                uninstallerPath = uninstallString.Trim('"').Split(' ')[0];
                                            }
                                            else
                                            {
                                                uninstallerPath = Path.Combine(installLocation, "uninstall.exe");
                                            }

                                            if (File.Exists(uninstallerPath))
                                            {
                                                installations.Add(new QlcInstallation
                                                {
                                                    InstallPath = installLocation,
                                                    Version = !string.IsNullOrEmpty(versionInfo.FileVersion)
                                                        ? versionInfo.FileVersion
                                                        : subKey.GetValue("DisplayVersion")?.ToString() ?? "Unknown",
                                                    UninstallerPath = uninstallerPath,
                                                    SizeMB = totalSize,
                                                    InstallDate = fileInfo.CreationTime
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return installations;
        }

        private void CheckAndAddInstallation(string searchPath, List<QlcInstallation> installations)
        {
            var qlcExePath = Path.Combine(searchPath, "qlcplus.exe");
            var qlcQmlExePath = Path.Combine(searchPath, "qlcplus-qml.exe");
            var uninstallerPath = Path.Combine(searchPath, "uninstall.exe");

            string actualExePath = null;
            if (File.Exists(qlcExePath))
            {
                actualExePath = qlcExePath;
            }
            else if (File.Exists(qlcQmlExePath))
            {
                actualExePath = qlcQmlExePath;
            }

            if (actualExePath != null && File.Exists(uninstallerPath))
            {
                if (!installations.Any(i => i.InstallPath.Equals(searchPath, StringComparison.OrdinalIgnoreCase)))
                {
                    var fileInfo = new FileInfo(actualExePath);
                    var versionInfo = FileVersionInfo.GetVersionInfo(actualExePath);

                    var dirInfo = new DirectoryInfo(searchPath);
                    var totalSize = GetDirectorySize(dirInfo) / (1024 * 1024);

                    installations.Add(new QlcInstallation
                    {
                        InstallPath = searchPath,
                        Version = !string.IsNullOrEmpty(versionInfo.FileVersion)
                            ? versionInfo.FileVersion
                            : "Unknown",
                        UninstallerPath = uninstallerPath,
                        SizeMB = totalSize,
                        InstallDate = fileInfo.CreationTime
                    });
                }
            }
        }

        private long GetDirectorySize(DirectoryInfo directory)
        {
            try
            {
                long size = 0;

                foreach (var file in directory.GetFiles())
                {
                    size += file.Length;
                }

                foreach (var subDir in directory.GetDirectories())
                {
                    size += GetDirectorySize(subDir);
                }

                return size;
            }
            catch
            {
                return 0;
            }
        }

        private QlcInstallation ShowInstallationSelector(List<QlcInstallation> installations)
        {
            using (var selectorForm = new Form())
            {
                selectorForm.Text = "Select QLC+ Installation to Uninstall";
                selectorForm.Size = new Size(700, 500);
                selectorForm.StartPosition = FormStartPosition.CenterParent;
                selectorForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                selectorForm.MaximizeBox = false;
                selectorForm.MinimizeBox = false;
                selectorForm.BackColor = Color.Black;
                selectorForm.ForeColor = Color.White;

                var instructionLabel = new Label
                {
                    Text = $"Found {installations.Count} QLC+ installation(s). Select one to uninstall:",
                    Location = new Point(20, 20),
                    Size = new Size(650, 30),
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = Color.White
                };
                selectorForm.Controls.Add(instructionLabel);

                var listBox = new ListBox
                {
                    Location = new Point(20, 60),
                    Size = new Size(650, 150),
                    Font = new Font("Segoe UI", 10F),
                    BackColor = Color.FromArgb(32, 32, 32),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                foreach (var installation in installations)
                {
                    listBox.Items.Add(installation);
                }

                if (listBox.Items.Count > 0)
                {
                    listBox.SelectedIndex = 0;
                }

                selectorForm.Controls.Add(listBox);

                var detailsPanel = new Panel
                {
                    Location = new Point(20, 220),
                    Size = new Size(650, 180),
                    BackColor = Color.FromArgb(20, 20, 20),
                    BorderStyle = BorderStyle.FixedSingle
                };
                selectorForm.Controls.Add(detailsPanel);

                var detailsTitle = new Label
                {
                    Text = "Installation Details:",
                    Location = new Point(10, 10),
                    Size = new Size(630, 25),
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    ForeColor = Color.Lime
                };
                detailsPanel.Controls.Add(detailsTitle);

                var detailsLabel = new Label
                {
                    Location = new Point(10, 40),
                    Size = new Size(630, 130),
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.White
                };
                detailsPanel.Controls.Add(detailsLabel);

                listBox.SelectedIndexChanged += (s, e) =>
                {
                    if (listBox.SelectedItem is QlcInstallation selected)
                    {
                        detailsLabel.Text = $"Version: {selected.Version}\n\n" +
                                          $"Installation Path:\n{selected.InstallPath}\n\n" +
                                          $"Size: {selected.SizeMB} MB\n\n" +
                                          $"Installed: {(selected.InstallDate.HasValue ? selected.InstallDate.Value.ToString("yyyy-MM-dd HH:mm") : "Unknown")}\n\n" +
                                          $"Uninstaller: {Path.GetFileName(selected.UninstallerPath)}";
                    }
                };

                if (listBox.Items.Count > 0)
                {
                    listBox.SelectedIndex = 0;
                }

                var uninstallButton = new Button
                {
                    Text = "Uninstall Selected",
                    Location = new Point(20, 415),
                    Size = new Size(180, 35),
                    DialogResult = DialogResult.OK,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Red,
                    BackColor = Color.Black
                };
                uninstallButton.FlatAppearance.BorderColor = Color.Red;
                uninstallButton.FlatAppearance.MouseOverBackColor = Color.DarkRed;
                uninstallButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(192, 0, 0);
                selectorForm.Controls.Add(uninstallButton);

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    Location = new Point(220, 415),
                    Size = new Size(100, 35),
                    DialogResult = DialogResult.Cancel,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Gray,
                    BackColor = Color.Black
                };
                cancelButton.FlatAppearance.BorderColor = Color.Gray;
                cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 64, 64);
                cancelButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(48, 48, 48);
                selectorForm.Controls.Add(cancelButton);

                selectorForm.AcceptButton = uninstallButton;
                selectorForm.CancelButton = cancelButton;

                listBox.DoubleClick += (s, e) => { selectorForm.DialogResult = DialogResult.OK; selectorForm.Close(); };

                if (selectorForm.ShowDialog(this) == DialogResult.OK && listBox.SelectedItem != null)
                {
                    return listBox.SelectedItem as QlcInstallation;
                }

                return null;
            }
        }

        private async Task UninstallQlc(QlcInstallation installation)
        {
            var confirmResult = MessageBox.Show(
                $"Are you sure you want to uninstall QLC+ {installation.Version}?\n\n" +
                $"Installation Path: {installation.InstallPath}\n\n" +
                $"This will launch the uninstaller. You may need to follow additional prompts.",
                "Confirm Uninstall",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmResult != DialogResult.Yes)
            {
                return;
            }

            try
            {
                if (!File.Exists(installation.UninstallerPath))
                {
                    MessageBox.Show($"Uninstaller not found at:\n{installation.UninstallerPath}\n\nThe installation may have been moved or corrupted.",
                        "Uninstaller Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = installation.UninstallerPath,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                var process = Process.Start(processInfo);

                MessageBox.Show(
                    $"The QLC+ {installation.Version} uninstaller has been launched.\n\n" +
                    "Please follow the uninstallation wizard to complete the removal.\n\n" +
                    "Note: You may need to provide administrator permissions.",
                    "Uninstaller Started",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                if (process != null)
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            process.WaitForExit();
                        }
                        catch { }
                    });

                    if (!Directory.Exists(installation.InstallPath))
                    {
                        MessageBox.Show("QLC+ has been successfully uninstalled!", "Uninstall Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch uninstaller: {ex.Message}\n\nYou may need to run this application as administrator.",
                    "Uninstall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void installFixtureDefinitions()
        {
            try
            {
                using (var loadingForm = new Form())
                {
                    loadingForm.Text = "Fixture Definitions Installer";
                    loadingForm.Size = new Size(400, 150);
                    loadingForm.StartPosition = FormStartPosition.CenterParent;
                    loadingForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    loadingForm.MaximizeBox = false;
                    loadingForm.MinimizeBox = false;
                    loadingForm.BackColor = Color.Black;

                    var loadingLabel = new Label
                    {
                        Text = "Fetching available fixture definition releases from GitHub...",
                        AutoSize = false,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Segoe UI", 10F),
                        ForeColor = Color.White
                    };
                    loadingForm.Controls.Add(loadingLabel);

                    loadingForm.Show(this);
                    loadingForm.Refresh();

                    var releases = await FetchFixtureReleases();
                    loadingForm.Close();

                    if (releases == null || releases.Count == 0)
                    {
                        MessageBox.Show("Failed to fetch fixture definition releases from GitHub. Please check your internet connection and try again.",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var selectedReleases = ShowFixtureReleaseSelector(releases);
                    if (selectedReleases == null || selectedReleases.Count == 0)
                    {
                        return;
                    }

                    await DownloadAndInstallFixtures(selectedReleases);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<List<string>> FetchFixtureReleases()
        {
            try
            {
                var response = await httpClient.GetAsync("https://api.github.com/repos/CSG-TechnikAG/Fixtures/releases");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var releases = new List<string>();

                foreach (var release in document.RootElement.EnumerateArray())
                {
                    if (release.TryGetProperty("tag_name", out var tagName))
                    {
                        var version = tagName.GetString();
                        if (!string.IsNullOrEmpty(version))
                        {
                            releases.Add(version);
                        }
                    }
                }

                return releases;
            }
            catch
            {
                return null;
            }
        }

        private List<string> ShowFixtureReleaseSelector(List<string> releases)
        {
            using (var selectorForm = new Form())
            {
                selectorForm.Text = "Select Fixture Definition Releases";
                selectorForm.Size = new Size(550, 500);
                selectorForm.StartPosition = FormStartPosition.CenterParent;
                selectorForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                selectorForm.MaximizeBox = false;
                selectorForm.MinimizeBox = false;
                selectorForm.BackColor = Color.Black;
                selectorForm.ForeColor = Color.White;

                var instructionLabel = new Label
                {
                    Text = "Select fixture definition releases to install (multi-select supported):",
                    Location = new Point(20, 20),
                    Size = new Size(500, 30),
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = Color.White
                };
                selectorForm.Controls.Add(instructionLabel);

                var listBox = new ListBox
                {
                    Location = new Point(20, 60),
                    Size = new Size(500, 340),
                    Font = new Font("Segoe UI", 10F),
                    BackColor = Color.FromArgb(32, 32, 32),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    SelectionMode = SelectionMode.MultiExtended
                };

                foreach (var release in releases)
                {
                    listBox.Items.Add(release);
                }

                selectorForm.Controls.Add(listBox);

                var infoLabel = new Label
                {
                    Text = "Tip: Hold Ctrl to select multiple, Shift for range selection",
                    Location = new Point(20, 410),
                    Size = new Size(500, 20),
                    Font = new Font("Segoe UI", 8F),
                    ForeColor = Color.Gray
                };
                selectorForm.Controls.Add(infoLabel);

                var installButton = new Button
                {
                    Text = "Install Selected Releases",
                    Location = new Point(20, 435),
                    Size = new Size(180, 35),
                    DialogResult = DialogResult.OK,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Lime,
                    BackColor = Color.Black
                };
                installButton.FlatAppearance.BorderColor = Color.Lime;
                installButton.FlatAppearance.MouseOverBackColor = Color.DarkGreen;
                installButton.FlatAppearance.MouseDownBackColor = Color.Green;
                selectorForm.Controls.Add(installButton);

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    Location = new Point(220, 435),
                    Size = new Size(100, 35),
                    DialogResult = DialogResult.Cancel,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Red,
                    BackColor = Color.Black
                };
                cancelButton.FlatAppearance.BorderColor = Color.Red;
                cancelButton.FlatAppearance.MouseOverBackColor = Color.DarkRed;
                cancelButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(192, 0, 0);
                selectorForm.Controls.Add(cancelButton);

                selectorForm.AcceptButton = installButton;
                selectorForm.CancelButton = cancelButton;

                if (selectorForm.ShowDialog(this) == DialogResult.OK && listBox.SelectedItems.Count > 0)
                {
                    return listBox.SelectedItems.Cast<string>().ToList();
                }

                return null;
            }
        }

        private async Task DownloadAndInstallFixtures(List<string> releases)
        {
            var fixturesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "QLC+", "Fixtures");

            try
            {
                if (!Directory.Exists(fixturesPath))
                {
                    Directory.CreateDirectory(fixturesPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create fixtures directory:\n{fixturesPath}\n\nError: {ex.Message}",
                    "Directory Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int successCount = 0;
            int failCount = 0;
            var errors = new List<string>();

            foreach (var release in releases)
            {
                var downloadUrl = $"https://github.com/CSG-TechnikAG/Fixtures/releases/download/{release}/Fixtures.zip";
                var tempZipPath = Path.Combine(Path.GetTempPath(), $"Fixtures_{release}.zip");

                using (var progressForm = new Form())
                {
                    progressForm.Text = "Downloading Fixture Definitions";
                    progressForm.Size = new Size(500, 200);
                    progressForm.StartPosition = FormStartPosition.CenterParent;
                    progressForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    progressForm.MaximizeBox = false;
                    progressForm.MinimizeBox = false;
                    progressForm.ControlBox = false;
                    progressForm.BackColor = Color.Black;
                    progressForm.ForeColor = Color.White;

                    var statusLabel = new Label
                    {
                        Text = $"Downloading fixtures from release {release}...",
                        Location = new Point(20, 20),
                        Size = new Size(450, 30),
                        Font = new Font("Segoe UI", 10F),
                        ForeColor = Color.White
                    };
                    progressForm.Controls.Add(statusLabel);

                    var progressBar = new ProgressBar
                    {
                        Location = new Point(20, 60),
                        Size = new Size(450, 30),
                        Style = ProgressBarStyle.Continuous,
                        ForeColor = Color.Lime
                    };
                    progressForm.Controls.Add(progressBar);

                    var detailsLabel = new Label
                    {
                        Text = "Preparing download...",
                        Location = new Point(20, 100),
                        Size = new Size(450, 30),
                        Font = new Font("Segoe UI", 9F),
                        ForeColor = Color.Gray
                    };
                    progressForm.Controls.Add(detailsLabel);

                    progressForm.Show(this);
                    progressForm.Refresh();

                    try
                    {
                        var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                        if (!response.IsSuccessStatusCode)
                        {
                            progressForm.Close();
                            errors.Add($"{release}: Download failed (HTTP {response.StatusCode})");
                            failCount++;
                            continue;
                        }

                        var totalBytes = response.Content.Headers.ContentLength ?? 0;
                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            long totalRead = 0;
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalRead += bytesRead;

                                if (totalBytes > 0)
                                {
                                    var percentage = (int)((totalRead * 100) / totalBytes);
                                    progressBar.Value = Math.Min(percentage, 100);
                                    var mbRead = totalRead / (1024.0 * 1024.0);
                                    var mbTotal = totalBytes / (1024.0 * 1024.0);
                                    detailsLabel.Text = $"Downloaded {mbRead:F2} MB of {mbTotal:F2} MB ({percentage}%)";
                                }
                                else
                                {
                                    detailsLabel.Text = $"Downloaded {totalRead / (1024.0 * 1024.0):F2} MB";
                                }

                                progressForm.Refresh();
                            }
                        }

                        statusLabel.Text = $"Extracting fixtures from {release}...";
                        detailsLabel.Text = "Extracting .qxf files...";
                        progressBar.Style = ProgressBarStyle.Marquee;
                        progressForm.Refresh();

                        using (var archive = ZipFile.OpenRead(tempZipPath))
                        {
                            int extractedCount = 0;
                            foreach (var entry in archive.Entries)
                            {
                                if (entry.FullName.EndsWith(".qxf", StringComparison.OrdinalIgnoreCase))
                                {
                                    var destinationPath = Path.Combine(fixturesPath, Path.GetFileName(entry.FullName));
                                    entry.ExtractToFile(destinationPath, true);
                                    extractedCount++;
                                }
                            }

                            if (extractedCount > 0)
                            {
                                successCount++;
                            }
                            else
                            {
                                errors.Add($"{release}: No .qxf files found in archive");
                                failCount++;
                            }
                        }

                        try
                        {
                            File.Delete(tempZipPath);
                        }
                        catch { }

                        progressForm.Close();
                    }
                    catch (Exception ex)
                    {
                        progressForm.Close();
                        errors.Add($"{release}: {ex.Message}");
                        failCount++;

                        if (File.Exists(tempZipPath))
                        {
                            try { File.Delete(tempZipPath); } catch { }
                        }
                    }
                }
            }

            var resultMessage = $"Fixture Definition Installation Complete!\n\n" +
                               $"Successfully installed: {successCount}\n" +
                               $"Failed: {failCount}\n\n" +
                               $"Fixtures installed to:\n{fixturesPath}";

            if (errors.Count > 0)
            {
                resultMessage += $"\n\nErrors:\n{string.Join("\n", errors)}";
            }

            MessageBox.Show(resultMessage, "Installation Complete",
                MessageBoxButtons.OK,
                failCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private async void uninstallFixtureDefinitions()
        {
            try
            {
                var fixturesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "QLC+", "Fixtures");

                if (!Directory.Exists(fixturesPath))
                {
                    MessageBox.Show($"Fixtures directory not found:\n{fixturesPath}\n\nNo fixtures are installed for the current user.",
                        "No Fixtures Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var loadingForm = new Form())
                {
                    loadingForm.Text = "Fixture Definitions Uninstaller";
                    loadingForm.Size = new Size(400, 150);
                    loadingForm.StartPosition = FormStartPosition.CenterParent;
                    loadingForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    loadingForm.MaximizeBox = false;
                    loadingForm.MinimizeBox = false;
                    loadingForm.BackColor = Color.Black;

                    var loadingLabel = new Label
                    {
                        Text = "Scanning for installed fixture definitions...",
                        AutoSize = false,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Segoe UI", 10F),
                        ForeColor = Color.White
                    };
                    loadingForm.Controls.Add(loadingLabel);

                    loadingForm.Show(this);
                    loadingForm.Refresh();

                    var fixtures = await Task.Run(() => FindInstalledFixtures(fixturesPath));
                    loadingForm.Close();

                    if (fixtures == null || fixtures.Count == 0)
                    {
                        MessageBox.Show($"No fixture definitions (.qxf files) found in:\n{fixturesPath}",
                            "No Fixtures Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var selectedFixtures = ShowFixtureUninstallSelector(fixtures, fixturesPath);
                    if (selectedFixtures == null || selectedFixtures.Count == 0)
                    {
                        return;
                    }

                    await UninstallFixtures(selectedFixtures, fixturesPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private List<string> FindInstalledFixtures(string fixturesPath)
        {
            try
            {
                var fixtures = new List<string>();
                var directory = new DirectoryInfo(fixturesPath);

                foreach (var file in directory.GetFiles("*.qxf"))
                {
                    fixtures.Add(file.Name);
                }

                return fixtures.OrderBy(f => f).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private List<string> ShowFixtureUninstallSelector(List<string> fixtures, string fixturesPath)
        {
            using (var selectorForm = new Form())
            {
                selectorForm.Text = "Select Fixture Definitions to Uninstall";
                selectorForm.Size = new Size(700, 550);
                selectorForm.StartPosition = FormStartPosition.CenterParent;
                selectorForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                selectorForm.MaximizeBox = false;
                selectorForm.MinimizeBox = false;
                selectorForm.BackColor = Color.Black;
                selectorForm.ForeColor = Color.White;

                var instructionLabel = new Label
                {
                    Text = $"Found {fixtures.Count} fixture definition(s). Select fixtures to uninstall:",
                    Location = new Point(20, 20),
                    Size = new Size(650, 30),
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = Color.White
                };
                selectorForm.Controls.Add(instructionLabel);

                var pathLabel = new Label
                {
                    Text = $"Location: {fixturesPath}",
                    Location = new Point(20, 50),
                    Size = new Size(650, 20),
                    Font = new Font("Segoe UI", 8F),
                    ForeColor = Color.Gray
                };
                selectorForm.Controls.Add(pathLabel);

                var listBox = new ListBox
                {
                    Location = new Point(20, 80),
                    Size = new Size(650, 360),
                    Font = new Font("Segoe UI", 9F),
                    BackColor = Color.FromArgb(32, 32, 32),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    SelectionMode = SelectionMode.MultiExtended
                };

                foreach (var fixture in fixtures)
                {
                    listBox.Items.Add(fixture);
                }

                selectorForm.Controls.Add(listBox);

                var infoLabel = new Label
                {
                    Text = "Tip: Hold Ctrl to select multiple, Shift for range, Ctrl+A to select all",
                    Location = new Point(20, 450),
                    Size = new Size(650, 20),
                    Font = new Font("Segoe UI", 8F),
                    ForeColor = Color.Gray
                };
                selectorForm.Controls.Add(infoLabel);

                var selectAllButton = new Button
                {
                    Text = "Select All",
                    Location = new Point(340, 475),
                    Size = new Size(100, 35),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Yellow,
                    BackColor = Color.Black
                };
                selectAllButton.FlatAppearance.BorderColor = Color.Yellow;
                selectAllButton.FlatAppearance.MouseOverBackColor = Color.Olive;
                selectAllButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(192, 192, 0);
                selectAllButton.Click += (s, e) =>
                {
                    for (int i = 0; i < listBox.Items.Count; i++)
                    {
                        listBox.SetSelected(i, true);
                    }
                };
                selectorForm.Controls.Add(selectAllButton);

                var uninstallButton = new Button
                {
                    Text = "Uninstall Selected",
                    Location = new Point(20, 475),
                    Size = new Size(180, 35),
                    DialogResult = DialogResult.OK,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Red,
                    BackColor = Color.Black
                };
                uninstallButton.FlatAppearance.BorderColor = Color.Red;
                uninstallButton.FlatAppearance.MouseOverBackColor = Color.DarkRed;
                uninstallButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(192, 0, 0);
                selectorForm.Controls.Add(uninstallButton);

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    Location = new Point(220, 475),
                    Size = new Size(100, 35),
                    DialogResult = DialogResult.Cancel,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Gray,
                    BackColor = Color.Black
                };
                cancelButton.FlatAppearance.BorderColor = Color.Gray;
                cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 64, 64);
                cancelButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(48, 48, 48);
                selectorForm.Controls.Add(cancelButton);

                selectorForm.AcceptButton = uninstallButton;
                selectorForm.CancelButton = cancelButton;

                if (selectorForm.ShowDialog(this) == DialogResult.OK && listBox.SelectedItems.Count > 0)
                {
                    return listBox.SelectedItems.Cast<string>().ToList();
                }

                return null;
            }
        }

        private async Task UninstallFixtures(List<string> fixtures, string fixturesPath)
        {
            var confirmResult = MessageBox.Show(
                $"Are you sure you want to uninstall {fixtures.Count} fixture definition(s)?\n\n" +
                $"This action cannot be undone.\n\n" +
                $"Selected fixtures:\n{string.Join("\n", fixtures.Take(10))}" +
                (fixtures.Count > 10 ? $"\n... and {fixtures.Count - 10} more" : ""),
                "Confirm Uninstall",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmResult != DialogResult.Yes)
            {
                return;
            }

            int successCount = 0;
            int failCount = 0;
            var errors = new List<string>();

            await Task.Run(() =>
            {
                foreach (var fixture in fixtures)
                {
                    try
                    {
                        var filePath = Path.Combine(fixturesPath, fixture);
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            successCount++;
                        }
                        else
                        {
                            errors.Add($"{fixture}: File not found");
                            failCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{fixture}: {ex.Message}");
                        failCount++;
                    }
                }
            });

            var resultMessage = $"Fixture Definition Uninstallation Complete!\n\n" +
                               $"Successfully uninstalled: {successCount}\n" +
                               $"Failed: {failCount}";

            if (errors.Count > 0)
            {
                resultMessage += $"\n\nErrors:\n{string.Join("\n", errors.Take(10))}";
                if (errors.Count > 10)
                {
                    resultMessage += $"\n... and {errors.Count - 10} more errors";
                }
            }

            MessageBox.Show(resultMessage, "Uninstallation Complete",
                MessageBoxButtons.OK,
                failCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private async void installDrivers()
        {
            try
            {
                using (var loadingForm = new Form())
                {
                    loadingForm.Text = "Driver Installer";
                    loadingForm.Size = new Size(400, 150);
                    loadingForm.StartPosition = FormStartPosition.CenterParent;
                    loadingForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    loadingForm.MaximizeBox = false;
                    loadingForm.MinimizeBox = false;
                    loadingForm.BackColor = Color.Black;

                    var loadingLabel = new Label
                    {
                        Text = "Fetching available driver releases from GitHub...",
                        AutoSize = false,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Segoe UI", 10F),
                        ForeColor = Color.White
                    };
                    loadingForm.Controls.Add(loadingLabel);

                    loadingForm.Show(this);
                    loadingForm.Refresh();

                    var releases = await FetchDriverReleases();
                    loadingForm.Close();

                    if (releases == null || releases.Count == 0)
                    {
                        MessageBox.Show("Failed to fetch driver releases from GitHub. Please check your internet connection and try again.",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var selectedRelease = ShowDriverReleaseSelector(releases);
                    if (string.IsNullOrEmpty(selectedRelease))
                    {
                        return;
                    }

                    await DownloadAndInstallDriver(selectedRelease);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<List<string>> FetchDriverReleases()
        {
            try
            {
                var response = await httpClient.GetAsync("https://api.github.com/repos/CSG-TechnikAG/Archiv/releases");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var releases = new List<string>();

                foreach (var release in document.RootElement.EnumerateArray())
                {
                    if (release.TryGetProperty("tag_name", out var tagName))
                    {
                        var version = tagName.GetString();
                        if (!string.IsNullOrEmpty(version))
                        {
                            releases.Add(version);
                        }
                    }
                }

                return releases;
            }
            catch
            {
                return null;
            }
        }

        private string ShowDriverReleaseSelector(List<string> releases)
        {
            using (var selectorForm = new Form())
            {
                selectorForm.Text = "Select Driver to Install";
                selectorForm.Size = new Size(550, 450);
                selectorForm.StartPosition = FormStartPosition.CenterParent;
                selectorForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                selectorForm.MaximizeBox = false;
                selectorForm.MinimizeBox = false;
                selectorForm.BackColor = Color.Black;
                selectorForm.ForeColor = Color.White;

                var instructionLabel = new Label
                {
                    Text = "Select a driver release to install:",
                    Location = new Point(20, 20),
                    Size = new Size(500, 30),
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = Color.White
                };
                selectorForm.Controls.Add(instructionLabel);

                var warningLabel = new Label
                {
                    Text = "⚠️ Installing drivers may require administrator permissions",
                    Location = new Point(20, 50),
                    Size = new Size(500, 25),
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.Yellow
                };
                selectorForm.Controls.Add(warningLabel);

                var listBox = new ListBox
                {
                    Location = new Point(20, 85),
                    Size = new Size(500, 290),
                    Font = new Font("Segoe UI", 10F),
                    BackColor = Color.FromArgb(32, 32, 32),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                foreach (var release in releases)
                {
                    listBox.Items.Add(release);
                }

                if (listBox.Items.Count > 0)
                {
                    listBox.SelectedIndex = 0;
                }

                selectorForm.Controls.Add(listBox);

                var installButton = new Button
                {
                    Text = "Install Selected Driver",
                    Location = new Point(20, 390),
                    Size = new Size(180, 35),
                    DialogResult = DialogResult.OK,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Lime,
                    BackColor = Color.Black
                };
                installButton.FlatAppearance.BorderColor = Color.Lime;
                installButton.FlatAppearance.MouseOverBackColor = Color.DarkGreen;
                installButton.FlatAppearance.MouseDownBackColor = Color.Green;
                selectorForm.Controls.Add(installButton);

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    Location = new Point(220, 390),
                    Size = new Size(100, 35),
                    DialogResult = DialogResult.Cancel,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Red,
                    BackColor = Color.Black
                };
                cancelButton.FlatAppearance.BorderColor = Color.Red;
                cancelButton.FlatAppearance.MouseOverBackColor = Color.DarkRed;
                cancelButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(192, 0, 0);
                selectorForm.Controls.Add(cancelButton);

                selectorForm.AcceptButton = installButton;
                selectorForm.CancelButton = cancelButton;

                listBox.DoubleClick += (s, e) => { selectorForm.DialogResult = DialogResult.OK; selectorForm.Close(); };

                if (selectorForm.ShowDialog(this) == DialogResult.OK && listBox.SelectedItem != null)
                {
                    return listBox.SelectedItem.ToString();
                }

                return null;
            }
        }

        private async Task DownloadAndInstallDriver(string release)
        {
            var downloadUrl = $"https://github.com/CSG-TechnikAG/Archiv/releases/download/{release}/install.exe";
            var tempPath = Path.Combine(Path.GetTempPath(), $"driver_install_{release}.exe");

            using (var progressForm = new Form())
            {
                progressForm.Text = "Downloading Driver";
                progressForm.Size = new Size(500, 200);
                progressForm.StartPosition = FormStartPosition.CenterParent;
                progressForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                progressForm.MaximizeBox = false;
                progressForm.MinimizeBox = false;
                progressForm.ControlBox = false;
                progressForm.BackColor = Color.Black;
                progressForm.ForeColor = Color.White;

                var statusLabel = new Label
                {
                    Text = $"Downloading driver installer from release {release}...",
                    Location = new Point(20, 20),
                    Size = new Size(450, 30),
                    Font = new Font("Segoe UI", 10F),
                    ForeColor = Color.White
                };
                progressForm.Controls.Add(statusLabel);

                var progressBar = new ProgressBar
                {
                    Location = new Point(20, 60),
                    Size = new Size(450, 30),
                    Style = ProgressBarStyle.Continuous,
                    ForeColor = Color.Lime
                };
                progressForm.Controls.Add(progressBar);

                var detailsLabel = new Label
                {
                    Text = "Preparing download...",
                    Location = new Point(20, 100),
                    Size = new Size(450, 30),
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.Gray
                };
                progressForm.Controls.Add(detailsLabel);

                progressForm.Show(this);
                progressForm.Refresh();

                try
                {
                    var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                    if (!response.IsSuccessStatusCode)
                    {
                        progressForm.Close();
                        MessageBox.Show($"Failed to download driver installer from release {release}.\n\nThe install.exe file might not be available at:\n{downloadUrl}\n\nPlease try a different release.",
                            "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                var percentage = (int)((totalRead * 100) / totalBytes);
                                progressBar.Value = Math.Min(percentage, 100);
                                var mbRead = totalRead / (1024.0 * 1024.0);
                                var mbTotal = totalBytes / (1024.0 * 1024.0);
                                detailsLabel.Text = $"Downloaded {mbRead:F2} MB of {mbTotal:F2} MB ({percentage}%)";
                            }
                            else
                            {
                                detailsLabel.Text = $"Downloaded {totalRead / (1024.0 * 1024.0):F2} MB";
                            }

                            progressForm.Refresh();
                        }
                    }

                    progressForm.Close();

                    var confirmResult = MessageBox.Show(
                        $"Driver installer for {release} has been downloaded successfully!\n\n" +
                        "The installer will be launched with interactive mode (/ask parameter).\n\n" +
                        "You may need to provide administrator permissions.\n\n" +
                        "Do you want to run the installer now?",
                        "Download Complete",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (confirmResult == DialogResult.Yes)
                    {
                        try
                        {
                            var processInfo = new ProcessStartInfo
                            {
                                FileName = tempPath,
                                Arguments = "/ask",
                                UseShellExecute = true,
                                Verb = "runas"
                            };

                            var process = Process.Start(processInfo);

                            MessageBox.Show(
                                $"The driver installer for {release} has been launched with /ask parameter.\n\n" +
                                "Please follow the installation wizard prompts.\n\n" +
                                "Note: You may need to provide administrator permissions.",
                                "Installer Started",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            if (process != null)
                            {
                                await Task.Run(() =>
                                {
                                    try
                                    {
                                        process.WaitForExit();
                                    }
                                    catch { }
                                });

                                try
                                {
                                    File.Delete(tempPath);
                                }
                                catch { }

                                MessageBox.Show("Driver installation completed!", "Installation Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to launch driver installer: {ex.Message}\n\nYou may need to run this application as administrator.",
                                "Installer Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"The installer has been saved to:\n{tempPath}\n\nYou can run it manually later with the /ask parameter.",
                            "Installation Skipped", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    progressForm.Close();
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                    throw new Exception($"Download failed: {ex.Message}", ex);
                }
            }
        }

        private async void uninstallDrivers()
        {
            try
            {
                using (var loadingForm = new Form())
                {
                    loadingForm.Text = "Driver Uninstaller";
                    loadingForm.Size = new Size(400, 150);
                    loadingForm.StartPosition = FormStartPosition.CenterParent;
                    loadingForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    loadingForm.MaximizeBox = false;
                    loadingForm.MinimizeBox = false;
                    loadingForm.BackColor = Color.Black;

                    var loadingLabel = new Label
                    {
                        Text = "Scanning for installed FTDI drivers...",
                        AutoSize = false,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Segoe UI", 10F),
                        ForeColor = Color.White
                    };
                    loadingForm.Controls.Add(loadingLabel);

                    loadingForm.Show(this);
                    loadingForm.Refresh();

                    var drivers = await Task.Run(() => FindInstalledFTDIDrivers());
                    loadingForm.Close();

                    if (drivers == null || drivers.Count == 0)
                    {
                        MessageBox.Show("No FTDI drivers found in the system.\n\nFTDI drivers might not be installed or are registered under a different name.",
                            "No Drivers Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var selectedDrivers = ShowDriverUninstallSelector(drivers);
                    if (selectedDrivers == null || selectedDrivers.Count == 0)
                    {
                        return;
                    }

                    await UninstallFTDIDrivers(selectedDrivers);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private class FTDIDriver
        {
            public string DisplayName { get; set; }
            public string Version { get; set; }
            public string Publisher { get; set; }
            public string UninstallString { get; set; }
            public string RegistryKey { get; set; }

            public override string ToString()
            {
                return $"{DisplayName} (v{Version})";
            }
        }

        private List<FTDIDriver> FindInstalledFTDIDrivers()
        {
            var drivers = new List<FTDIDriver>();
            var registryPaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var registryPath in registryPaths)
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryPath))
                    {
                        if (key != null)
                        {
                            foreach (var subKeyName in key.GetSubKeyNames())
                            {
                                using (var subKey = key.OpenSubKey(subKeyName))
                                {
                                    var displayName = subKey?.GetValue("DisplayName")?.ToString();
                                    if (displayName != null && displayName.Contains("FTDI", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var uninstallString = subKey.GetValue("UninstallString")?.ToString();
                                        if (!string.IsNullOrEmpty(uninstallString))
                                        {
                                            if (!drivers.Any(d => d.DisplayName == displayName))
                                            {
                                                drivers.Add(new FTDIDriver
                                                {
                                                    DisplayName = displayName,
                                                    Version = subKey.GetValue("DisplayVersion")?.ToString() ?? "Unknown",
                                                    Publisher = subKey.GetValue("Publisher")?.ToString() ?? "Unknown",
                                                    UninstallString = uninstallString,
                                                    RegistryKey = $"{registryPath}\\{subKeyName}"
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            return drivers.OrderBy(d => d.DisplayName).ToList();
        }

        private List<FTDIDriver> ShowDriverUninstallSelector(List<FTDIDriver> drivers)
        {
            using (var selectorForm = new Form())
            {
                selectorForm.Text = "Select FTDI Drivers to Uninstall";
                selectorForm.Size = new Size(750, 550);
                selectorForm.StartPosition = FormStartPosition.CenterParent;
                selectorForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                selectorForm.MaximizeBox = false;
                selectorForm.MinimizeBox = false;
                selectorForm.BackColor = Color.Black;
                selectorForm.ForeColor = Color.White;

                var instructionLabel = new Label
                {
                    Text = $"Found {drivers.Count} FTDI driver(s). Select drivers to uninstall:",
                    Location = new Point(20, 20),
                    Size = new Size(700, 30),
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = Color.White
                };
                selectorForm.Controls.Add(instructionLabel);

                var warningLabel = new Label
                {
                    Text = "⚠️ Warning: Uninstalling drivers may affect connected hardware devices",
                    Location = new Point(20, 50),
                    Size = new Size(700, 25),
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.Red
                };
                selectorForm.Controls.Add(warningLabel);

                var listBox = new ListBox
                {
                    Location = new Point(20, 85),
                    Size = new Size(700, 200),
                    Font = new Font("Segoe UI", 10F),
                    BackColor = Color.FromArgb(32, 32, 32),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    SelectionMode = SelectionMode.MultiExtended
                };

                foreach (var driver in drivers)
                {
                    listBox.Items.Add(driver);
                }

                selectorForm.Controls.Add(listBox);

                var detailsPanel = new Panel
                {
                    Location = new Point(20, 295),
                    Size = new Size(700, 180),
                    BackColor = Color.FromArgb(20, 20, 20),
                    BorderStyle = BorderStyle.FixedSingle
                };
                selectorForm.Controls.Add(detailsPanel);

                var detailsTitle = new Label
                {
                    Text = "Driver Details:",
                    Location = new Point(10, 10),
                    Size = new Size(680, 25),
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    ForeColor = Color.Lime
                };
                detailsPanel.Controls.Add(detailsTitle);

                var detailsLabel = new Label
                {
                    Location = new Point(10, 40),
                    Size = new Size(680, 130),
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.White
                };
                detailsPanel.Controls.Add(detailsLabel);

                listBox.SelectedIndexChanged += (s, e) =>
                {
                    if (listBox.SelectedItem is FTDIDriver selected)
                    {
                        detailsLabel.Text = $"Driver Name: {selected.DisplayName}\n\n" +
                                          $"Version: {selected.Version}\n\n" +
                                          $"Publisher: {selected.Publisher}\n\n" +
                                          $"Uninstall Command:\n{selected.UninstallString}";
                    }
                    else if (listBox.SelectedItems.Count > 1)
                    {
                        detailsLabel.Text = $"Multiple drivers selected: {listBox.SelectedItems.Count} driver(s)\n\n" +
                                          "All selected drivers will be uninstalled.";
                    }
                };

                var infoLabel = new Label
                {
                    Text = "Tip: Hold Ctrl to select multiple, Shift for range selection",
                    Location = new Point(20, 485),
                    Size = new Size(700, 20),
                    Font = new Font("Segoe UI", 8F),
                    ForeColor = Color.Gray
                };
                selectorForm.Controls.Add(infoLabel);

                var uninstallButton = new Button
                {
                    Text = "Uninstall Selected",
                    Location = new Point(20, 510),
                    Size = new Size(180, 35),
                    DialogResult = DialogResult.OK,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Red,
                    BackColor = Color.Black
                };
                uninstallButton.FlatAppearance.BorderColor = Color.Red;
                uninstallButton.FlatAppearance.MouseOverBackColor = Color.DarkRed;
                uninstallButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(192, 0, 0);
                selectorForm.Controls.Add(uninstallButton);

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    Location = new Point(220, 510),
                    Size = new Size(100, 35),
                    DialogResult = DialogResult.Cancel,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Gray,
                    BackColor = Color.Black
                };
                cancelButton.FlatAppearance.BorderColor = Color.Gray;
                cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 64, 64);
                cancelButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(48, 48, 48);
                selectorForm.Controls.Add(cancelButton);

                selectorForm.AcceptButton = uninstallButton;
                selectorForm.CancelButton = cancelButton;

                if (selectorForm.ShowDialog(this) == DialogResult.OK && listBox.SelectedItems.Count > 0)
                {
                    return listBox.SelectedItems.Cast<FTDIDriver>().ToList();
                }

                return null;
            }
        }

        private async Task UninstallFTDIDrivers(List<FTDIDriver> drivers)
        {
            var driverNames = string.Join("\n", drivers.Select(d => d.DisplayName).Take(5));
            if (drivers.Count > 5)
            {
                driverNames += $"\n... and {drivers.Count - 5} more";
            }

            var confirmResult = MessageBox.Show(
                $"Are you sure you want to uninstall {drivers.Count} FTDI driver(s)?\n\n" +
                "⚠️ WARNING: This may affect connected hardware devices!\n\n" +
                $"Selected drivers:\n{driverNames}\n\n" +
                "This action will launch the uninstaller(s) and may require administrator permissions.",
                "Confirm Driver Uninstall",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmResult != DialogResult.Yes)
            {
                return;
            }

            int successCount = 0;
            int failCount = 0;
            var errors = new List<string>();

            foreach (var driver in drivers)
            {
                try
                {
                    if (string.IsNullOrEmpty(driver.UninstallString))
                    {
                        errors.Add($"{driver.DisplayName}: No uninstall command found");
                        failCount++;
                        continue;
                    }

                    var uninstallCmd = driver.UninstallString.Trim('"');
                    var fileName = uninstallCmd;
                    var arguments = "";

                    var spaceIndex = uninstallCmd.IndexOf(".exe ", StringComparison.OrdinalIgnoreCase);
                    if (spaceIndex > 0)
                    {
                        fileName = uninstallCmd.Substring(0, spaceIndex + 4);
                        arguments = uninstallCmd.Substring(spaceIndex + 5);
                    }
                    else if (uninstallCmd.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName = "msiexec.exe";
                        arguments = uninstallCmd.Replace("msiexec.exe", "").Replace("msiexec", "").Trim();
                        if (!arguments.Contains("/qb") && !arguments.Contains("/qn") && !arguments.Contains("/q"))
                        {
                            arguments += " /qb";
                        }
                    }

                    var processInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    var process = Process.Start(processInfo);

                    if (process != null)
                    {
                        await Task.Run(() =>
                        {
                            try
                            {
                                process.WaitForExit(30000);
                            }
                            catch { }
                        });
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{driver.DisplayName}: {ex.Message}");
                    failCount++;
                }
            }

            var resultMessage = $"Driver Uninstallation Process Complete!\n\n" +
                               $"Uninstallers launched: {successCount}\n" +
                               $"Failed: {failCount}\n\n" +
                               "Note: Some uninstallers may still be running in the background.\n" +
                               "Please follow any remaining prompts to complete the uninstallation.";

            if (errors.Count > 0)
            {
                resultMessage += $"\n\nErrors:\n{string.Join("\n", errors.Take(5))}";
                if (errors.Count > 5)
                {
                    resultMessage += $"\n... and {errors.Count - 5} more errors";
                }
            }

            MessageBox.Show(resultMessage, "Uninstallation Process Complete",
                MessageBoxButtons.OK,
                failCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
    }
}
