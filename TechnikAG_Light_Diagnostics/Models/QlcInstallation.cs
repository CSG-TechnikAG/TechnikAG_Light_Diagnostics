namespace TechnikAG_Light_Diagnostics.Models;

public sealed class QlcInstallation
{
    public required string InstallPath { get; init; }
    public required string ExecutablePath { get; init; }
    public required string ArchitectureId { get; init; }
    public required string Version { get; init; }
    public required string UninstallerPath { get; init; }
    public long SizeMB { get; init; }
    public DateTime? InstallDate { get; init; }
}
