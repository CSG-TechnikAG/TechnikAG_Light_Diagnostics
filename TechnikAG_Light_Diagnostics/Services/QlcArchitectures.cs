using TechnikAG_Light_Diagnostics.Models;

namespace TechnikAG_Light_Diagnostics.Services;

public static class QlcArchitectures
{
    public static QlcArchitecture QlcPlus4 { get; } = new(
        Id: "qlcplus4",
        DisplayName: "QLC+ 4",
        Description: "Official QLC+ 4 series with the Qt Widgets interface. Installs to C:\\QLC+.",
        CdnSource: "qlcplus",
        CdnPackage: "v4");

    public static QlcArchitecture QlcPlus5 { get; } = new(
        Id: "qlcplus5",
        DisplayName: "QLC+ 5",
        Description: "Official QLC+ 5 series with the QML interface. Installs to C:\\QLC+5 alongside QLC+ 4.",
        CdnSource: "qlcplus",
        CdnPackage: "v5");

    public static QlcArchitecture QlcPlusPlus { get; } = new(
        Id: "qlcpp",
        DisplayName: "QLC++",
        Description: "TechnikAG QLC++ build. It is hosted on the CDN and installs like QLC+ 4 into C:\\QLC+.",
        CdnSource: "technikag-apps",
        CdnPackage: "qlcplus");

    public static IReadOnlyList<QlcArchitecture> All { get; } = new[]
    {
        QlcPlus4,
        QlcPlus5,
        QlcPlusPlus
    };

    public static QlcArchitecture? Find(string architectureId) =>
        All.FirstOrDefault(architecture =>
            string.Equals(architecture.Id, architectureId, StringComparison.OrdinalIgnoreCase));
}
