# Technik-AG Light Diagnostics

Windows diagnostics and recovery utility for the Technik-AG lighting setup.

The app installs and removes QLC+, the Technik-AG fixture library and USB-DMX
drivers, and can update itself. The UI is built with WPF, the native .NET
desktop UI framework for Windows.

## Downloads and version checks

The application never talks to GitHub or qlcplus.org directly. Every version
check and every download goes through the CDN:

```text
https://cdn.by-cra.net
```

| Content | CDN package |
| --- | --- |
| QLC+ 4 installers | `qlcplus/v4` |
| QLC+ 5 installers | `qlcplus/v5` |
| QLC++ installers | `technikag-apps/qlcplus` |
| Fixture definitions | `cra/qlc-fixtures` |
| DMX drivers | `technikag-drivers/jmsusb2dmx`, `technikag-drivers/enttecdmxusbpro` |
| App updates | `technikag-apps/TechnikAG_Light_Diagnostics` |

The list endpoints used by the app are:

```text
https://cdn.by-cra.net/list?category=files&source=...&package=...
https://cdn.by-cra.net/package/<source>/<package>/<version>/<file>
https://cdn.by-cra.net/update-packages
```

The CDN is responsible for mirroring upstream sources. The client only sees
`cdn.by-cra.net`.

## QLC+ architectures

The tool treats the three QLC+ variants as separate architectures:

| Architecture | Source | Installation |
| --- | --- | --- |
| **QLC+ 4** | Official QLC+ 4 releases on qlcplus.org | Installs to `C:\QLC+` |
| **QLC+ 5** | Official QLC+ 5 releases on qlcplus.org | Installs to `C:\QLC+5`, side by side with QLC+ 4 |
| **QLC++** | TechnikAG QLC++ builds hosted on the CDN | Same installer flow and location as QLC+ 4 |

Each row shows the installed version, the latest CDN version and whether an
update is available. The installer opens a version picker for the selected
architecture and preselects the newest version.

The **Refresh versions** button asks the CDN to re-resolve the upstream QLC+
release tags, probes the available installers and rebuilds the package index
(the existing `https://cdn.by-cra.net/update-packages` endpoint). That makes
new QLC+ 4 / QLC+ 5 / QLC++ releases appear in the tool without redeploying the
CDN.

The CDN map limits the official QLC+ 4 history to 4.13.0+ and QLC+ 5 to
5.2.0+ so only recent releases are mirrored. QLC++ has no minimum version.

The CDN mirrors installers during its update run, which executes at server
start and can be triggered through `https://cdn.by-cra.net/update-packages`.
Newly discovered QLC+ versions are fetched by that update run; the diagnostics
tool reloads the index immediately after triggering it.

## In-app updater

On startup the app asks the CDN for the newest
`technikag-apps/TechnikAG_Light_Diagnostics` Windows x64 file and compares it
with its own assembly version. A banner appears when a newer version is
available. The update is downloaded to the temp folder, then a small PowerShell
helper waits for the running app to exit, replaces the executable and restarts
it. "Check for updates" in the header runs the same check manually.

## Build

Requirements: .NET 10 SDK on Windows.

```powershell
dotnet build TechnikAG_Light_Diagnostics/TechnikAG_Light_Diagnostics.csproj
```

Create the distributable single-file executable:

```powershell
dotnet publish TechnikAG_Light_Diagnostics/TechnikAG_Light_Diagnostics.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish
```

The result is `publish/TechnikAG_Light_Diagnostics.exe`. The file name must stay
exactly `TechnikAG_Light_Diagnostics.exe` because the CDN map references it.

## Automatic versioning and releases

`.github/workflows/build.yml` runs on every push to `master`/`main`, on version
tags (`v*`), on pull requests and on manual dispatch.

- A tag push uses the tag as the version (`v1.2.3` becomes `1.2.3`).
- A branch or manual build uses `1.0.<run number>` unless a version is supplied.
- The workflow builds the WPF app, publishes a self-contained single-file exe,
  uploads it as a workflow artifact and creates or updates the GitHub release
  with that version.
- The CDN discovers the release through its `dynamic_versions` mapping on its
  next refresh and mirrors `TechnikAG_Light_Diagnostics.exe` into
  `technikag-apps/TechnikAG_Light_Diagnostics`.

No version number is edited by hand in the project file.

## CDN configuration

The CDN repository (`cdn/`, one directory above this repository) contains the
package mapping in `app/static/map.json`. It must contain:

- `technikag-apps` with `qlcplus` (QLC++) and `TechnikAG_Light_Diagnostics`.
- `qlcplus` with `v4` and `v5` for the official QLC+ installers.
- `cra` with `qlc-fixtures`.
- `technikag-drivers` with the `jmsusb2dmx` and `enttecdmxusbpro` driver
  packages (`install.exe`).

After changing the CDN mapping, redeploy the CDN so the new package list and
cached files are available. To refresh already-cached files, call
`https://cdn.by-cra.net/update-packages?force=1`.

## Project layout

```text
TechnikAG_Light_Diagnostics/
  App.xaml                        WPF application
  MainWindow.xaml                 Main dashboard
  Themes/Styles.xaml              Dark WPF theme
  Dialogs/SelectionDialog.xaml    Version / package picker
  Models/                         Data records
  Services/
    CdnClient.cs                  CDN list and download client
    AppUpdateService.cs           Self-updater
    QlcArchitectures.cs           QLC+ 4 / QLC+ 5 / QLC++ definitions
    QlcPlusScanner.cs             Local QLC+ detection and removal
    FixtureStore.cs               Local fixture installation storage
    FtdiDriverScanner.cs          Local FTDI driver detection
    ShellRunner.cs                Process launching helpers
    VersionUtil.cs                Version comparison
  Resources/                      Icons and application icon
```
