using Microsoft.Extensions.Options;
using PharmacyFinder.API.Helpers;

namespace PharmacyFinder.API.Services;

internal static class PrescriptionImageValidation
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
    };

    public static void Validate(IFormFile file, StorageSettings settings)
    {
        if (file.Length == 0)
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionImageInvalid, "Image file is empty.");

        var maxBytes = settings.MaxFileSizeMb * 1024L * 1024L;
        if (file.Length > maxBytes)
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionImageInvalid,
                $"Image exceeds maximum size of {settings.MaxFileSizeMb} MB.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionImageInvalid,
                "Only JPEG, PNG, and WebP images are allowed.");
    }

    public static string ResolveExtension(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!string.IsNullOrWhiteSpace(extension))
            return extension.ToLowerInvariant();

        return file.ContentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
    }
}
