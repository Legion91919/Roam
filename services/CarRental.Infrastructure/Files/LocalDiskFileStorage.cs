using System.Text.RegularExpressions;
using CarRental.Application.Abstractions.Files;
using CarRental.Application.Common.Exceptions;

namespace CarRental.Infrastructure.Files;

/// <summary>
/// ADAPTER for IFileStorage on the local disk (Development). The root is configurable
/// (Files:LocalRoot) and lives OUTSIDE any web root: the API never serves this folder statically.
/// Every download goes through the authorised endpoint.
/// </summary>
/// <remarks>
/// Keys are generated here ("2026/11/9f2c…e1.jpg"): a random GUID, never the user's file name, so an
/// uploaded name like "../../appsettings.json" can never become a path. Keys read back are checked
/// against the same pattern before touching the disk.
/// </remarks>
public sealed partial class LocalDiskFileStorage(string root) : IFileStorage
{
    private static readonly Dictionary<string, string> Extensions = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["application/pdf"] = ".pdf",
    };

    public string Root { get; } = Path.GetFullPath(root);

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken)
    {
        var extension = Extensions.TryGetValue(contentType, out var ext) ? ext : ".bin";
        var now = DateTime.UtcNow;
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";
        var path = PathFor(key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = PathFor(storageKey);
        if (!File.Exists(path))
            throw new NotFoundException("File", storageKey);
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = PathFor(storageKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string PathFor(string storageKey)
    {
        if (!KeyPattern().IsMatch(storageKey))
            throw new NotFoundException("File", "invalid key");
        var path = Path.GetFullPath(Path.Combine(Root, storageKey));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new NotFoundException("File", "invalid key");
        return path;
    }

    [GeneratedRegex(@"^\d{4}/\d{2}/[0-9a-f]{32}\.(jpg|png|pdf|bin)$")]
    private static partial Regex KeyPattern();
}

/// <summary>
/// PRODUCTION PLACEHOLDER (Files:Provider = AzureBlob). Deliberately not implemented in this PoC so
/// the Azure SDK is not a dependency. To implement:
///  * Add Azure.Storage.Blobs to Infrastructure only; authenticate with Managed Identity (no keys).
///  * SaveAsync: upload to a PRIVATE container under the same random key; set Content-Type.
///  * OpenReadAsync: BlobClient.OpenReadAsync (the API still streams it after its own access check),
///    or return a short-lived read-only SAS for large files.
///  * Turn on soft delete and immutability (legal hold) for completed inspections: they are evidence.
///  * Scan uploads for malware (Microsoft Defender for Storage) before they are served.
/// </summary>
public sealed class AzureBlobFileStorage : IFileStorage
{
    private const string Message = "Azure Blob storage is not implemented in this proof of concept. Use Files:Provider = Local.";

    public Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken) => throw new NotSupportedException(Message);
    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) => throw new NotSupportedException(Message);
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => throw new NotSupportedException(Message);
}
