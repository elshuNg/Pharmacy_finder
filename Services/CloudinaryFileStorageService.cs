using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using PharmacyFinder.API.Helpers;

namespace PharmacyFinder.API.Services;

public class CloudinaryFileStorageService(
    IOptions<StorageSettings> storageOptions,
    IOptions<CloudinarySettings> cloudinaryOptions,
    IHttpClientFactory httpClientFactory) : IFileStorageService
{
    private readonly Cloudinary _cloudinary = new(new Account(
        cloudinaryOptions.Value.CloudName,
        cloudinaryOptions.Value.ApiKey,
        cloudinaryOptions.Value.ApiSecret));

    public async Task<string> SavePrescriptionImageAsync(Guid prescriptionId, IFormFile file, CancellationToken cancellationToken = default)
    {
        PrescriptionImageValidation.Validate(file, storageOptions.Value);

        var folder = cloudinaryOptions.Value.Folder.Trim().Trim('/');
        var publicId = string.IsNullOrEmpty(folder)
            ? prescriptionId.ToString()
            : $"{folder}/{prescriptionId}";

        await using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            PublicId = publicId,
            Overwrite = true
        };

        var result = await _cloudinary.UploadAsync(uploadParams);
        if (result.Error is not null)
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionImageInvalid,
                $"Cloudinary upload failed: {result.Error.Message}");

        return result.SecureUrl.ToString();
    }

    public async Task<string> MaterializeForOcrAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        using var response = await client.GetAsync(imageUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionImageInvalid,
                "Could not download prescription image for OCR.");

        var extension = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".jpg";

        var tempPath = Path.Combine(Path.GetTempPath(), $"prescription-ocr-{Guid.NewGuid():N}{extension}");
        await using var fileStream = File.Create(tempPath);
        await response.Content.CopyToAsync(fileStream, cancellationToken);
        return tempPath;
    }

    public void CleanupOcrMaterialization(string materializedPath)
    {
        if (materializedPath.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) && File.Exists(materializedPath))
            File.Delete(materializedPath);
    }
}
