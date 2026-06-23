using Microsoft.Extensions.Options;
using PharmacyFinder.API.Helpers;

namespace PharmacyFinder.API.Services;

public interface IFileStorageService
{
    Task<string> SavePrescriptionImageAsync(Guid prescriptionId, IFormFile file, CancellationToken cancellationToken = default);
    Task<string> MaterializeForOcrAsync(string imageUrl, CancellationToken cancellationToken = default);
    void CleanupOcrMaterialization(string materializedPath);
}

public class LocalFileStorageService(IOptions<StorageSettings> options, IWebHostEnvironment env) : IFileStorageService
{
    public async Task<string> SavePrescriptionImageAsync(Guid prescriptionId, IFormFile file, CancellationToken cancellationToken = default)
    {
        PrescriptionImageValidation.Validate(file, options.Value);

        var extension = PrescriptionImageValidation.ResolveExtension(file);
        var uploadDir = Path.Combine(env.ContentRootPath, options.Value.PrescriptionUploadPath);
        Directory.CreateDirectory(uploadDir);

        var fileName = $"{prescriptionId}{extension}";
        var fullPath = Path.Combine(uploadDir, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);

        return $"/{options.Value.PrescriptionUploadPath.Replace('\\', '/')}/{fileName}";
    }

    public Task<string> MaterializeForOcrAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var trimmed = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(env.ContentRootPath, trimmed);
        if (!File.Exists(absolutePath))
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionImageInvalid, "Prescription image file was not found.");
        return Task.FromResult(absolutePath);
    }

    public void CleanupOcrMaterialization(string materializedPath)
    {
        // Local files are persisted; nothing to clean up for OCR.
    }
}
