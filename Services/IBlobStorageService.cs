namespace RaceDay.Services
{
    public record BlobUploadResult(string BlobName, string Url);
    public interface IBlobStorageService
    {
        string? validate(IFormFile file);

        Task<BlobUploadResult> UploadFileAsync(IFormFile file,  CancellationToken ct = default);

        Task DeleteAsync(string? blobName, CancellationToken ct = default);
    }
}
