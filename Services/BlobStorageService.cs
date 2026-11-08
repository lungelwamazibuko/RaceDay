using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Services
{
    public class BlobStorageService : IBlobStorageService
    {
        private const long MaxBytes = 2 * 1024 * 1024; // 2 MB

        private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".gif"] = "image/gif"
        };

        private readonly BlobContainerClient _container;

        public BlobStorageService(IConfiguration config)
        {
            var connectionString = config.GetConnectionString("BlobStorage");
            var containerName = config["BlobContainerName"] ?? "event-banners";

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("AzureStorage:ConnectionString is not configured.");
            try
            {
                _container = new BlobContainerClient(connectionString.Trim(), containerName);
            }
            catch (FormatException)
            {
                var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=')[0].Trim());
                throw new InvalidOperationException(
                    $"Connection string is invalid. length={connectionString.Length}; " +
                    $"starts with '{connectionString[..Math.Min(25, connectionString.Length)]}'; " +
                    $"keys found: {string.Join(", ", parts)}. ");
            }
        }
        public string? Validate(IFormFile file)
        {
            if (file.Length == 0) return "The Selected file is empty.";
            if (file.Length > MaxBytes) return "The banner must be 2 MB or smaller.";

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedTypes.TryGetValue(ext, out var expectedType) || !string.Equals(file.ContentType, expectedType, StringComparison.OrdinalIgnoreCase))
            {
                return "The banner must be a valid image file (jpg, jpeg, png, gif).";
            }

            using var stream = file.OpenReadStream();
            Span<byte> h = stackalloc byte[12];

            var read = stream.Read(h);

            var valid = expectedType switch
            {
                "image/jpeg" => h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
                "image/png" => h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47,
                "image/gif" => h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38,
                _ => false
            };
            return valid ? null : "That file doesn't look like a valid image.";
        }
        public async Task<BlobUploadResult> UploadFileAsync(IFormFile file, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var blobName = $"banners/{Guid.NewGuid():N}{ext}";
            var blob = _container.GetBlobClient(blobName);
            await using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = AllowedTypes[ext],
                    CacheControl = "public, max-age=31536000" // 1 year
                }
            }, ct);
            return new BlobUploadResult(blobName, blob.Uri.ToString());
        }
        public async Task DeleteAsync(string? blobName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(blobName)) return;
            await _container.DeleteBlobIfExistsAsync(blobName, cancellationToken: ct);
        }
    }
}
