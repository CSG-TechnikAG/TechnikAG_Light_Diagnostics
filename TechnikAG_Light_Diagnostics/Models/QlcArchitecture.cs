namespace TechnikAG_Light_Diagnostics.Models;

public sealed record QlcArchitecture(
    string Id,
    string DisplayName,
    string Description,
    string CdnSource,
    string CdnPackage,
    string InstallDirectory);
