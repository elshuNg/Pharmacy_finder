namespace PharmacyFinder.API.Helpers;

public class StorageSettings
{
    public string Provider { get; set; } = "Local";
    public string PrescriptionUploadPath { get; set; } = "uploads/prescriptions";
    public int MaxFileSizeMb { get; set; } = 10;
}

public class CloudinarySettings
{
    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string Folder { get; set; } = "prescriptions";
}

public class BootstrapAdminSettings
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? FullName { get; set; }
}

public class TesseractSettings
{
    public string DataPath { get; set; } = "tessdata";
    public string Language { get; set; } = "eng";
}
public class CorsSettings
{
    /// <summary>Comma-separated allowed origins, e.g. http://localhost:4200,http://127.0.0.1:4200</summary>
    public string AllowedOrigins { get; set; } = "http://localhost:4200";

    /// <summary>When true, allows any origin. Use for local/dev; set false in production.</summary>
    public bool AllowAll { get; set; }
}
