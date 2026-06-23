using System.ComponentModel.DataAnnotations;
using PharmacyFinder.API.Models;

namespace PharmacyFinder.API.DTOs;

public class RegisterDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email format is invalid.")]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters.")]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Phone number format is invalid.")]
    [MaxLength(30)]
    public string? Phone { get; set; }

    public UserRole Role { get; set; } = UserRole.Customer;
}

public class LoginDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email format is invalid.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}

public class CreatePharmacyDto
{
    [Required(ErrorMessage = "Pharmacy name is required.")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Address is required.")]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double Longitude { get; set; }

    [Required(ErrorMessage = "License number is required.")]
    [MaxLength(100)]
    public string LicenseNumber { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Contact phone format is invalid.")]
    [MaxLength(30)]
    public string? ContactPhone { get; set; }

    public string? OperatingHours { get; set; }
}

public class PharmacyDto
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? OperatingHours { get; set; }
    public PharmacyStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateStockDto
{
    public Guid MedicineId { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative.")]
    public int Quantity { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Price cannot be negative.")]
    public decimal Price { get; set; }
}

public class PharmacyMedicineDto
{
    public Guid PharmacyId { get; set; }
    public Guid MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class CreateMedicineDto
{
    [Required(ErrorMessage = "Medicine name is required.")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? GenericName { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsPrescriptionRequired { get; set; }
}

public class MedicineDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool IsPrescriptionRequired { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum MedicineMatchType
{
    None,
    Exact,
    FuzzyHigh,
    Fuzzy
}

public class CreatePrescriptionDto
{
    [Required(ErrorMessage = "Image URL is required.")]
    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;
}

public class PrescriptionItemReviewDto
{
    public Guid? Id { get; set; }
    public Guid? MedicineId { get; set; }
    public string OcrText { get; set; } = string.Empty;
    public string? SuggestedMedicineName { get; set; }
    public int? Quantity { get; set; }
    public double MatchConfidence { get; set; }
    public MedicineMatchType MatchType { get; set; }
    public bool RequiresConfirmation { get; set; }
}

public class PrescriptionUploadResultDto
{
    public Guid PrescriptionId { get; set; }
    public List<PrescriptionItemReviewDto> Items { get; set; } = new();
}

public class ConfirmPrescriptionSearchDto
{
    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double Longitude { get; set; }

    [Required(ErrorMessage = "Items list is required.")]
    [MinLength(1, ErrorMessage = "At least one prescription item is required.")]
    public List<PrescriptionItemInputDto> Items { get; set; } = new();
}

public class UpsertPrescriptionItemsDto
{
    [Required(ErrorMessage = "Items list is required.")]
    [MinLength(1, ErrorMessage = "At least one prescription item is required.")]
    public List<PrescriptionItemInputDto> Items { get; set; } = new();
}

public class PrescriptionItemInputDto
{
    public Guid? MedicineId { get; set; }

    [Required(ErrorMessage = "Medicine name is required.")]
    [MaxLength(300)]
    public string MedicineNameRaw { get; set; } = string.Empty;

    [Range(1, 999, ErrorMessage = "Quantity must be between 1 and 999.")]
    public int? Quantity { get; set; }
}

public class PrescriptionItemDto
{
    public Guid Id { get; set; }
    public Guid? MedicineId { get; set; }
    public string MedicineNameRaw { get; set; } = string.Empty;
    public int? Quantity { get; set; }
}

public class PrescriptionDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? ExtractedText { get; set; }
    public PrescriptionStatus Status { get; set; }
    public DateTime UploadedAt { get; set; }
    public List<PrescriptionItemDto> Items { get; set; } = new();
}

public class SearchQueryDto
{
    [Required(ErrorMessage = "Medicine name is required.")]
    [MinLength(2, ErrorMessage = "Medicine name must be at least 2 characters.")]
    [MaxLength(200)]
    public string MedicineName { get; set; } = string.Empty;

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double Lat { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double Lng { get; set; }
}

public class PrescriptionUploadFormDto
{
    [Required(ErrorMessage = "Prescription image file is required.")]
    public IFormFile? File { get; set; }

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double Longitude { get; set; }
}

public class SearchResultDto
{
    public Guid PharmacyId { get; set; }
    public string PharmacyName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public Guid MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public double Distance { get; set; }
}

public class ApprovalDto
{
    public ApprovalDecision Decision { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateUserRoleDto
{
    [Required(ErrorMessage = "Role is required.")]
    public UserRole Role { get; set; }
}

public class MeDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
}
