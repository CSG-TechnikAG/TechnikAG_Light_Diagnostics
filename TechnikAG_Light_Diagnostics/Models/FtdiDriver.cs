namespace TechnikAG_Light_Diagnostics.Models;

public sealed class FtdiDriver
{
    public required string DisplayName { get; init; }
    public required string Version { get; init; }
    public required string Publisher { get; init; }
    public required string UninstallString { get; init; }
    public required string RegistryKey { get; init; }
}
