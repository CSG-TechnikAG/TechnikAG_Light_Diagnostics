using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using TechnikAG_Light_Diagnostics.Models;

namespace TechnikAG_Light_Diagnostics.Services;

public sealed class CdnClient : IDisposable
{
    public const string BaseUrl = "https://cdn.by-cra.net";

    private readonly HttpClient _httpClient;

    public CdnClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(15)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TechnikAG-Light-Diagnostics");
    }

    public async Task<IReadOnlyList<CdnFile>> GetFilesAsync(
        string source,
        string? package = null,
        string? platform = null,
        string? architecture = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            "category=files",
            $"source={Uri.EscapeDataString(source)}"
        };

        if (!string.IsNullOrWhiteSpace(package))
        {
            query.Add($"package={Uri.EscapeDataString(package)}");
        }

        if (!string.IsNullOrWhiteSpace(platform))
        {
            query.Add($"platform={Uri.EscapeDataString(platform)}");
        }

        if (!string.IsNullOrWhiteSpace(architecture))
        {
            query.Add($"architecture={Uri.EscapeDataString(architecture)}");
        }

        var requestUrl = $"{BaseUrl}/list?{string.Join("&", query)}";
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(30));
        var requestToken = timeoutSource.Token;

        using var response = await _httpClient
            .GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead, requestToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content
            .ReadAsStreamAsync(requestToken)
            .ConfigureAwait(false);

        using var document = await JsonDocument
            .ParseAsync(stream, cancellationToken: requestToken)
            .ConfigureAwait(false);

        var result = new List<CdnFile>();
        if (!document.RootElement.TryGetProperty("files", out var filesElement) ||
            filesElement.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in filesElement.EnumerateObject())
        {
            var file = property.Value;
            if (file.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var sourceName = GetString(file, "source");
            var packageName = GetString(file, "package");
            var version = GetString(file, "version");
            var fileName = GetString(file, "file");
            if (sourceName is null || packageName is null || version is null || fileName is null)
            {
                continue;
            }

            result.Add(new CdnFile(
                sourceName,
                packageName,
                version,
                fileName,
                GetString(file, "mime") ?? "application/octet-stream",
                GetString(file, "platform"),
                GetString(file, "architecture"),
                GetBool(file, "available", defaultValue: true)));
        }

        return result;
    }

    public async Task UpdatePackagesAsync(
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(90));

        using var response = await _httpClient
            .GetAsync(
                $"{BaseUrl}/update-packages?force={(force ? "1" : "0")}&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                "The CDN does not provide the package update endpoint.");
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task DownloadAsync(
        CdnFile file,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var downloadUrl = GetDownloadUrl(file);
        using var response = await _httpClient
            .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"cdn.by-cra.net could not provide {file.FileName} " +
                $"(HTTP {(int)response.StatusCode}). The file may not be available on the CDN yet. " +
                "Use \"Refresh versions\" and try again.");
        }

        var totalBytes = response.Content.Headers.ContentLength;
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var contentStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var fileStream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        var stopwatch = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;

        try
        {
            long bytesReceived = 0;
            int bytesRead;
            while ((bytesRead = await contentStream
                       .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                       .ConfigureAwait(false)) > 0)
            {
                await fileStream
                    .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                    .ConfigureAwait(false);
                bytesReceived += bytesRead;

                if (stopwatch.Elapsed - lastReport >= TimeSpan.FromMilliseconds(90))
                {
                    progress?.Report(new DownloadProgress(bytesReceived, totalBytes, file.FileName));
                    lastReport = stopwatch.Elapsed;
                }
            }

            progress?.Report(new DownloadProgress(bytesReceived, totalBytes, file.FileName));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static string GetDownloadUrl(CdnFile file)
    {
        var path = string.Join(
            '/',
            new[] { file.Source, file.Package, file.Version, file.FileName }
                .SelectMany(value => value.Split('/', StringSplitOptions.RemoveEmptyEntries))
                .Select(Uri.EscapeDataString));

        return $"{BaseUrl}/package/{path}";
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool GetBool(JsonElement element, string propertyName, bool defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
    }

    public void Dispose() => _httpClient.Dispose();
}
