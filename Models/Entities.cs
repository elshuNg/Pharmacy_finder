using System.Text.Json;
using NpgsqlTypes;

namespace PharmacyFinder.API.Models;

public enum UserRole
{
    [PgName("admin")]
    Admin,
    [PgName("pharmacy_owner")]
    PharmacyOwner,
    [PgName("customer")]
    Customer
}

public enum PharmacyStatus
{
    [PgName("pending")]
    Pending,
    [PgName("approved")]
    Approved,
    [PgName("rejected")]
    Rejected
}

public enum PrescriptionStatus
{
    [PgName("uploaded")]
    Uploaded,
    [PgName("processing")]
    Processing,
    [PgName("ready")]
    Ready,
    [PgName("failed")]
    Failed
}

public enum ApprovalDecision
{
    [PgName("approved")]
    Approved,
    [PgName("rejected")]
    Rejected
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Pharmacy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public JsonDocument? OperatingHours { get; set; }
    public PharmacyStatus Status { get; set; } = PharmacyStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? Owner { get; set; }
    public List<PharmacyMedicine> Medicines { get; set; } = new();
}

public class Medicine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool IsPrescriptionRequired { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PharmacyMedicine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PharmacyId { get; set; }
    public Guid MedicineId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Pharmacy? Pharmacy { get; set; }
    public Medicine? Medicine { get; set; }
}

public class Prescription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? ExtractedText { get; set; }
    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Uploaded;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public User? Customer { get; set; }
    public List<PrescriptionItem> Items { get; set; } = new();
}

public class PrescriptionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; set; }
    public Guid? MedicineId { get; set; }
    public string MedicineNameRaw { get; set; } = string.Empty;
    public int? Quantity { get; set; }

    public Prescription? Prescription { get; set; }
    public Medicine? Medicine { get; set; }
}

public class PharmacyApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PharmacyId { get; set; }
    public Guid AdminId { get; set; }
    public ApprovalDecision Decision { get; set; }
    public string? Notes { get; set; }
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
}
