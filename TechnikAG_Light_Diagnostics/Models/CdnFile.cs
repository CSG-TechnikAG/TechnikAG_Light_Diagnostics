namespace TechnikAG_Light_Diagnostics.Models;

public sealed record CdnFile(
    string Source,
    string Package,
    string Version,
    string FileName,
    string Mime,
    string? Platform,
    string? Architecture,
    bool Available)
{
    public string Identifier => $"{Source}/{Package}/{Version}/{FileName}";
}
