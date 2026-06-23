using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PharmacyFinder.API.Data;
using PharmacyFinder.API.DTOs;
using PharmacyFinder.API.Helpers;
using PharmacyFinder.API.Models;

namespace PharmacyFinder.API.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<MeDto> GetMeAsync(ClaimsPrincipal principal);
}

public class AuthService(AppDbContext db, IPasswordHasher hasher, IJwtHelper jwtHelper) : IAuthService
{
    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        if (!EmailValidator.IsValid(dto.Email))
            throw ApiException.BadRequest(ApiErrorCodes.AuthInvalidEmail, "Email format is invalid.");

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail))
            throw ApiException.Conflict(ApiErrorCodes.AuthEmailExists, "Email already exists.");

        if (dto.Role == UserRole.Admin)
            throw ApiException.BadRequest(ApiErrorCodes.AuthCannotRegisterAdmin, "Cannot register as Admin.");

        var role = dto.Role == UserRole.PharmacyOwner ? UserRole.PharmacyOwner : UserRole.Customer;

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = hasher.HashPassword(dto.Password),
            FullName = dto.FullName,
            Phone = dto.Phone,
            Role = role,
            IsActive = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = jwtHelper.GenerateToken(user),
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        if (!EmailValidator.IsValid(dto.Email))
            throw ApiException.BadRequest(ApiErrorCodes.AuthInvalidEmail, "Email format is invalid.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant())
                   ?? throw ApiException.Unauthorized(ApiErrorCodes.AuthInvalidCredentials, "Invalid email or password.");

        if (!hasher.VerifyPassword(dto.Password, user.PasswordHash))
            throw ApiException.Unauthorized(ApiErrorCodes.AuthInvalidCredentials, "Invalid email or password.");

        return new AuthResponseDto
        {
            Token = jwtHelper.GenerateToken(user),
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role
        };
    }

    public async Task<MeDto> GetMeAsync(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue("userId");
        if (!Guid.TryParse(userId, out var id))
            throw ApiException.Unauthorized(ApiErrorCodes.AuthInvalidToken, "Invalid user token.");

        var user = await db.Users.FindAsync(id)
                   ?? throw ApiException.NotFound(ApiErrorCodes.UserNotFound, "User not found.");
        return new MeDto
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Role = user.Role,
            IsActive = user.IsActive
        };
    }
}

public interface IPharmacyService
{
    Task<PharmacyDto> CreateAsync(Guid ownerId, CreatePharmacyDto dto);
    Task<List<PharmacyDto>> GetAllAsync(PharmacyStatus? status);
    Task<PharmacyDto> GetByIdAsync(Guid id);
    Task<PharmacyDto> UpdateAsync(Guid pharmacyId, Guid ownerId, CreatePharmacyDto dto);
    Task<List<PharmacyDto>> GetMineAsync(Guid ownerId);
}

public class PharmacyService(AppDbContext db) : IPharmacyService
{
    public async Task<PharmacyDto> CreateAsync(Guid ownerId, CreatePharmacyDto dto)
    {
        var entity = new Pharmacy
        {
            OwnerId = ownerId,
            Name = dto.Name,
            Address = dto.Address,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            LicenseNumber = dto.LicenseNumber,
            ContactPhone = dto.ContactPhone,
            OperatingHours = ParseJson(dto.OperatingHours),
            Status = PharmacyStatus.Pending
        };
        db.Pharmacies.Add(entity);
        await db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<List<PharmacyDto>> GetAllAsync(PharmacyStatus? status)
    {
        var query = db.Pharmacies.AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var data = await query.ToListAsync();
        return data.Select(ToDto).ToList();
    }

    public async Task<PharmacyDto> GetByIdAsync(Guid id)
    {
        var p = await db.Pharmacies.FindAsync(id)
                ?? throw ApiException.NotFound(ApiErrorCodes.PharmacyNotFound, "Pharmacy not found.");
        return ToDto(p);
    }

    public async Task<PharmacyDto> UpdateAsync(Guid pharmacyId, Guid ownerId, CreatePharmacyDto dto)
    {
        var p = await db.Pharmacies.FindAsync(pharmacyId)
                ?? throw ApiException.NotFound(ApiErrorCodes.PharmacyNotFound, "Pharmacy not found.");
        if (p.OwnerId != ownerId)
            throw ApiException.Forbidden(ApiErrorCodes.PharmacyForbidden, "You can only update your own pharmacy.");

        p.Name = dto.Name;
        p.Address = dto.Address;
        p.Latitude = dto.Latitude;
        p.Longitude = dto.Longitude;
        p.LicenseNumber = dto.LicenseNumber;
        p.ContactPhone = dto.ContactPhone;
        p.OperatingHours = ParseJson(dto.OperatingHours);
        await db.SaveChangesAsync();
        return ToDto(p);
    }

    public async Task<List<PharmacyDto>> GetMineAsync(Guid ownerId)
    {
        var data = await db.Pharmacies.Where(p => p.OwnerId == ownerId).ToListAsync();
        return data.Select(ToDto).ToList();
    }

    private static JsonDocument? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw ApiException.BadRequest(ApiErrorCodes.ValidationFailed, "Operating hours must be valid JSON.");
        }
    }

    private static PharmacyDto ToDto(Pharmacy p) => new()
    {
        Id = p.Id,
        OwnerId = p.OwnerId,
        Name = p.Name,
        Address = p.Address,
        Latitude = p.Latitude,
        Longitude = p.Longitude,
        LicenseNumber = p.LicenseNumber,
        ContactPhone = p.ContactPhone,
        OperatingHours = p.OperatingHours?.RootElement.GetRawText(),
        Status = p.Status,
        CreatedAt = p.CreatedAt
    };
}

public interface IStockService
{
    Task<PharmacyMedicineDto> AddAsync(Guid pharmacyId, Guid ownerId, UpdateStockDto dto);
    Task<PharmacyMedicineDto> UpdateAsync(Guid pharmacyId, Guid medicineId, Guid ownerId, UpdateStockDto dto);
    Task RemoveAsync(Guid pharmacyId, Guid medicineId, Guid ownerId);
    Task<List<PharmacyMedicineDto>> GetByPharmacyAsync(Guid pharmacyId);
}

public class StockService(AppDbContext db) : IStockService
{
    public async Task<PharmacyMedicineDto> AddAsync(Guid pharmacyId, Guid ownerId, UpdateStockDto dto)
    {
        await EnsureOwnership(pharmacyId, ownerId);
        if (dto.MedicineId == Guid.Empty)
            throw ApiException.BadRequest(ApiErrorCodes.ValidationFailed, "Medicine ID is required.");

        if (!await db.Medicines.AnyAsync(m => m.Id == dto.MedicineId))
            throw ApiException.NotFound(ApiErrorCodes.MedicineNotFound, "Medicine not found.");

        var existing = await db.PharmacyMedicines.FirstOrDefaultAsync(x =>
            x.PharmacyId == pharmacyId && x.MedicineId == dto.MedicineId);
        if (existing is not null)
            throw ApiException.Conflict(ApiErrorCodes.StockAlreadyExists, "Medicine already exists in pharmacy stock. Use update endpoint.");

        var entity = new PharmacyMedicine
        {
            PharmacyId = pharmacyId,
            MedicineId = dto.MedicineId,
            Quantity = dto.Quantity,
            Price = dto.Price,
            UpdatedAt = DateTime.UtcNow
        };
        db.PharmacyMedicines.Add(entity);
        await db.SaveChangesAsync();
        return await ToDto(entity);
    }

    public async Task<PharmacyMedicineDto> UpdateAsync(Guid pharmacyId, Guid medicineId, Guid ownerId, UpdateStockDto dto)
    {
        await EnsureOwnership(pharmacyId, ownerId);
        var entity = await db.PharmacyMedicines.FirstOrDefaultAsync(x => x.PharmacyId == pharmacyId && x.MedicineId == medicineId)
                     ?? throw ApiException.NotFound(ApiErrorCodes.StockNotFound, "Stock item not found.");
        entity.Quantity = dto.Quantity;
        entity.Price = dto.Price;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await ToDto(entity);
    }

    public async Task RemoveAsync(Guid pharmacyId, Guid medicineId, Guid ownerId)
    {
        await EnsureOwnership(pharmacyId, ownerId);
        var entity = await db.PharmacyMedicines.FirstOrDefaultAsync(x => x.PharmacyId == pharmacyId && x.MedicineId == medicineId)
                     ?? throw ApiException.NotFound(ApiErrorCodes.StockNotFound, "Stock item not found.");
        db.PharmacyMedicines.Remove(entity);
        await db.SaveChangesAsync();
    }

    public Task<List<PharmacyMedicineDto>> GetByPharmacyAsync(Guid pharmacyId) =>
        db.PharmacyMedicines.Include(x => x.Medicine).Where(x => x.PharmacyId == pharmacyId)
            .Select(x => new PharmacyMedicineDto
            {
                PharmacyId = x.PharmacyId,
                MedicineId = x.MedicineId,
                MedicineName = x.Medicine!.Name,
                Quantity = x.Quantity,
                Price = x.Price
            }).ToListAsync();

    private async Task EnsureOwnership(Guid pharmacyId, Guid ownerId)
    {
        var pharmacy = await db.Pharmacies.FindAsync(pharmacyId)
                       ?? throw ApiException.NotFound(ApiErrorCodes.PharmacyNotFound, "Pharmacy not found.");
        if (pharmacy.OwnerId != ownerId)
            throw ApiException.Forbidden(ApiErrorCodes.PharmacyForbidden, "Not your pharmacy.");
    }

    private async Task<PharmacyMedicineDto> ToDto(PharmacyMedicine x)
    {
        var med = await db.Medicines.FindAsync(x.MedicineId)
                  ?? throw ApiException.NotFound(ApiErrorCodes.MedicineNotFound, "Medicine not found.");
        return new PharmacyMedicineDto
        {
            PharmacyId = x.PharmacyId,
            MedicineId = x.MedicineId,
            MedicineName = med.Name,
            Quantity = x.Quantity,
            Price = x.Price
        };
    }
}

public interface IMedicineService
{
    Task<MedicineDto> CreateAsync(CreateMedicineDto dto);
    Task<List<MedicineDto>> SearchAsync(string? name, string? category);
    Task<MedicineDto> GetAsync(Guid id);
    Task<MedicineDto> UpdateAsync(Guid id, CreateMedicineDto dto);
}

public class MedicineService(AppDbContext db) : IMedicineService
{
    public async Task<MedicineDto> CreateAsync(CreateMedicineDto dto)
    {
        var m = new Medicine
        {
            Name = dto.Name,
            GenericName = dto.GenericName,
            Category = dto.Category,
            Description = dto.Description,
            IsPrescriptionRequired = dto.IsPrescriptionRequired
        };
        db.Medicines.Add(m);
        await db.SaveChangesAsync();
        return ToDto(m);
    }

    public async Task<List<MedicineDto>> SearchAsync(string? name, string? category)
    {
        var query = db.Medicines.AsQueryable();
        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(x => EF.Functions.ILike(x.Name, PostgresSearchPatterns.ContainsPattern(name)));
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category != null && EF.Functions.ILike(x.Category, PostgresSearchPatterns.ContainsPattern(category)));
        var data = await query.ToListAsync();
        return data.Select(ToDto).ToList();
    }

    public async Task<MedicineDto> GetAsync(Guid id) =>
        ToDto(await db.Medicines.FindAsync(id)
              ?? throw ApiException.NotFound(ApiErrorCodes.MedicineNotFound, "Medicine not found."));

    public async Task<MedicineDto> UpdateAsync(Guid id, CreateMedicineDto dto)
    {
        var m = await db.Medicines.FindAsync(id)
                ?? throw ApiException.NotFound(ApiErrorCodes.MedicineNotFound, "Medicine not found.");
        m.Name = dto.Name;
        m.GenericName = dto.GenericName;
        m.Category = dto.Category;
        m.Description = dto.Description;
        m.IsPrescriptionRequired = dto.IsPrescriptionRequired;
        await db.SaveChangesAsync();
        return ToDto(m);
    }

    private static MedicineDto ToDto(Medicine x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        GenericName = x.GenericName,
        Category = x.Category,
        Description = x.Description,
        IsPrescriptionRequired = x.IsPrescriptionRequired,
        CreatedAt = x.CreatedAt
    };
}

public interface ISearchService
{
    Task<List<SearchResultDto>> SearchAsync(string medicineName, double lat, double lng);
    Task<List<SearchResultDto>> SearchForMedicinesAsync(
        IEnumerable<string> medicineNames,
        double lat,
        double lng,
        CancellationToken cancellationToken = default);
}

public class SearchService(AppDbContext db) : ISearchService
{
    public async Task<List<SearchResultDto>> SearchAsync(string medicineName, double lat, double lng)
    {
        if (string.IsNullOrWhiteSpace(medicineName))
            throw ApiException.BadRequest(ApiErrorCodes.SearchInvalidQuery, "Medicine name is required.");

        var rows = await QueryPharmacyMedicinesAsync([medicineName], CancellationToken.None);
        return MapAndSortByDistance(rows, lat, lng);
    }

    public async Task<List<SearchResultDto>> SearchForMedicinesAsync(
        IEnumerable<string> medicineNames,
        double lat,
        double lng,
        CancellationToken cancellationToken = default)
    {
        var names = medicineNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0)
            return [];

        var rows = await QueryPharmacyMedicinesAsync(names, cancellationToken);
        return MapAndSortByDistance(rows, lat, lng);
    }

    private async Task<List<PharmacyMedicine>> QueryPharmacyMedicinesAsync(
        IReadOnlyList<string> medicineNames,
        CancellationToken cancellationToken)
    {
        var patterns = medicineNames.Select(PostgresSearchPatterns.ContainsPattern).ToList();

        return await db.PharmacyMedicines
            .Include(pm => pm.Pharmacy)
            .Include(pm => pm.Medicine)
            .Where(pm => pm.Quantity > 0
                         && pm.Pharmacy!.Status == PharmacyStatus.Approved
                         && patterns.Any(pattern => EF.Functions.ILike(pm.Medicine!.Name, pattern)))
            .ToListAsync(cancellationToken);
    }

    private static List<SearchResultDto> MapAndSortByDistance(
        List<PharmacyMedicine> rows, double lat, double lng)
    {
        return rows.Select(x =>
            {
                var distance = HaversineHelper.DistanceKm(lat, lng, x.Pharmacy!.Latitude, x.Pharmacy.Longitude);
                return new SearchResultDto
                {
                    PharmacyId = x.PharmacyId,
                    PharmacyName = x.Pharmacy.Name,
                    Address = x.Pharmacy.Address,
                    Latitude = x.Pharmacy.Latitude,
                    Longitude = x.Pharmacy.Longitude,
                    MedicineId = x.MedicineId,
                    MedicineName = x.Medicine!.Name,
                    Quantity = x.Quantity,
                    Price = x.Price,
                    Distance = distance
                };
            })
            .OrderBy(x => x.Distance)
            .ToList();
    }
}

public interface IPrescriptionService
{
    Task<PrescriptionDto> CreateAsync(Guid customerId, CreatePrescriptionDto dto);
    Task<PrescriptionUploadResultDto> UploadForReviewAsync(
        Guid customerId,
        IFormFile file,
        CancellationToken cancellationToken = default);
    Task<List<SearchResultDto>> ConfirmAndSearchAsync(
        Guid prescriptionId,
        Guid customerId,
        ConfirmPrescriptionSearchDto dto,
        CancellationToken cancellationToken = default);
    Task<List<PrescriptionDto>> GetMineAsync(Guid customerId);
    Task<PrescriptionDto> GetByIdAsync(Guid prescriptionId, Guid? requesterId = null);
    Task<PrescriptionDto> UpsertItemsAsync(Guid prescriptionId, Guid customerId, UpsertPrescriptionItemsDto dto);
}

public class PrescriptionService(
    AppDbContext db,
    IFileStorageService fileStorage,
    IOcrService ocrService,
    IPrescriptionTextParser textParser,
    ISearchService searchService) : IPrescriptionService
{
    public async Task<PrescriptionDto> CreateAsync(Guid customerId, CreatePrescriptionDto dto)
    {
        var p = new Prescription
        {
            CustomerId = customerId,
            ImageUrl = dto.ImageUrl,
            Status = PrescriptionStatus.Uploaded
        };
        db.Prescriptions.Add(p);
        await db.SaveChangesAsync();
        return await GetByIdAsync(p.Id, customerId);
    }

    public async Task<PrescriptionUploadResultDto> UploadForReviewAsync(
        Guid customerId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var prescription = new Prescription
        {
            CustomerId = customerId,
            ImageUrl = string.Empty,
            Status = PrescriptionStatus.Processing
        };
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            prescription.ImageUrl = await fileStorage.SavePrescriptionImageAsync(prescription.Id, file, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            var ocrPath = await fileStorage.MaterializeForOcrAsync(prescription.ImageUrl, cancellationToken);
            try
            {
                prescription.ExtractedText = await ocrService.ExtractTextAsync(ocrPath, cancellationToken);
            }
            finally
            {
                fileStorage.CleanupOcrMaterialization(ocrPath);
            }

            var parsedItems = await textParser.ParseAsync(prescription.ExtractedText, cancellationToken);
            var reviewItems = parsedItems
                .Where(p => p.MedicineId.HasValue && p.MatchConfidence >= MedicineMatching.MinReviewConfidence)
                .ToList();

            var entities = reviewItems.Select(p => new PrescriptionItem
            {
                PrescriptionId = prescription.Id,
                MedicineId = p.MedicineId,
                MedicineNameRaw = p.OcrText,
                Quantity = p.Quantity
            }).ToList();
            db.PrescriptionItems.AddRange(entities);
            await db.SaveChangesAsync(cancellationToken);

            return new PrescriptionUploadResultDto
            {
                PrescriptionId = prescription.Id,
                Items = reviewItems.Zip(entities, (parsed, entity) => new PrescriptionItemReviewDto
                {
                    Id = entity.Id,
                    MedicineId = parsed.MedicineId,
                    OcrText = parsed.OcrText,
                    SuggestedMedicineName = parsed.SuggestedMedicineName,
                    Quantity = parsed.Quantity,
                    MatchConfidence = parsed.MatchConfidence,
                    MatchType = parsed.MatchType,
                    RequiresConfirmation = parsed.RequiresConfirmation
                }).ToList()
            };
        }
        catch (ApiException)
        {
            await MarkPrescriptionFailedAsync(prescription.Id);
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            await MarkPrescriptionFailedAsync(prescription.Id);
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionOcrFailed, ex.Message);
        }
        catch (Exception ex)
        {
            await MarkPrescriptionFailedAsync(prescription.Id);
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionOcrFailed,
                TesseractOcrService.GetRootCauseMessage(ex));
        }
    }

    public async Task<List<SearchResultDto>> ConfirmAndSearchAsync(
        Guid prescriptionId,
        Guid customerId,
        ConfirmPrescriptionSearchDto dto,
        CancellationToken cancellationToken = default)
    {
        var prescription = await db.Prescriptions
            .FirstOrDefaultAsync(x => x.Id == prescriptionId, cancellationToken)
            ?? throw ApiException.NotFound(ApiErrorCodes.PrescriptionNotFound, "Prescription not found.");

        if (prescription.CustomerId != customerId)
            throw ApiException.Forbidden(ApiErrorCodes.PrescriptionForbidden, "Cannot access this prescription.");

        if (prescription.Status == PrescriptionStatus.Failed)
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionOcrFailed, "This prescription upload failed. Please upload again.");

        var confirmedWithMedicine = dto.Items.Where(i => i.MedicineId.HasValue).ToList();
        if (confirmedWithMedicine.Count == 0)
            throw ApiException.BadRequest(ApiErrorCodes.PrescriptionNoMedicines,
                "At least one medicine with a catalogue match is required to search.");

        await db.PrescriptionItems
            .Where(pi => pi.PrescriptionId == prescriptionId)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var i in confirmedWithMedicine)
        {
            db.PrescriptionItems.Add(new PrescriptionItem
            {
                PrescriptionId = prescription.Id,
                MedicineId = i.MedicineId,
                MedicineNameRaw = i.MedicineNameRaw,
                Quantity = i.Quantity
            });
        }

        prescription.Status = PrescriptionStatus.Ready;
        await db.SaveChangesAsync(cancellationToken);

        return await SearchForConfirmedMedicinesAsync(confirmedWithMedicine, dto.Latitude, dto.Longitude, cancellationToken);
    }

    private async Task<List<SearchResultDto>> SearchForConfirmedMedicinesAsync(
        List<PrescriptionItemInputDto> confirmedWithMedicine,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var medicineIds = confirmedWithMedicine.Select(i => i.MedicineId!.Value).Distinct().ToList();
        var medicineLookup = await db.Medicines.AsNoTracking()
            .Where(m => medicineIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var searchTerms = confirmedWithMedicine
            .Where(i => medicineLookup.ContainsKey(i.MedicineId!.Value))
            .Select(i => medicineLookup[i.MedicineId!.Value].Name);

        return await searchService.SearchForMedicinesAsync(searchTerms, latitude, longitude, cancellationToken);
    }

    private async Task MarkPrescriptionFailedAsync(Guid prescriptionId)
    {
        try
        {
            await db.Prescriptions
                .Where(p => p.Id == prescriptionId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(p => p.Status, PrescriptionStatus.Failed),
                    CancellationToken.None);
        }
        catch
        {
            // Original upload error is more important than status update failure.
        }
    }

    public async Task<List<PrescriptionDto>> GetMineAsync(Guid customerId)
    {
        return await db.Prescriptions
            .Include(p => p.Items)
            .Where(p => p.CustomerId == customerId)
            .Select(p => ToDto(p))
            .ToListAsync();
    }

    public async Task<PrescriptionDto> GetByIdAsync(Guid prescriptionId, Guid? requesterId = null)
    {
        var p = await db.Prescriptions.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == prescriptionId)
                ?? throw ApiException.NotFound(ApiErrorCodes.PrescriptionNotFound, "Prescription not found.");
        if (requesterId.HasValue && p.CustomerId != requesterId.Value)
            throw ApiException.Forbidden(ApiErrorCodes.PrescriptionForbidden, "Cannot access this prescription.");
        return ToDto(p);
    }

    public async Task<PrescriptionDto> UpsertItemsAsync(Guid prescriptionId, Guid customerId, UpsertPrescriptionItemsDto dto)
    {
        var p = await db.Prescriptions.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == prescriptionId)
                ?? throw ApiException.NotFound(ApiErrorCodes.PrescriptionNotFound, "Prescription not found.");
        if (p.CustomerId != customerId)
            throw ApiException.Forbidden(ApiErrorCodes.PrescriptionForbidden, "Cannot modify this prescription.");

        db.PrescriptionItems.RemoveRange(p.Items);
        p.Items = dto.Items.Select(i => new PrescriptionItem
        {
            PrescriptionId = p.Id,
            MedicineId = i.MedicineId,
            MedicineNameRaw = i.MedicineNameRaw,
            Quantity = i.Quantity
        }).ToList();
        await db.SaveChangesAsync();
        return ToDto(p);
    }

    private static PrescriptionDto ToDto(Prescription p) => new()
    {
        Id = p.Id,
        CustomerId = p.CustomerId,
        ImageUrl = p.ImageUrl,
        ExtractedText = p.ExtractedText,
        Status = p.Status,
        UploadedAt = p.UploadedAt,
        Items = p.Items.Select(i => new PrescriptionItemDto
        {
            Id = i.Id,
            MedicineId = i.MedicineId,
            MedicineNameRaw = i.MedicineNameRaw,
            Quantity = i.Quantity
        }).ToList()
    };
}

public interface IAdminService
{
    Task<List<PharmacyDto>> GetPendingPharmaciesAsync();
    Task ApproveAsync(Guid pharmacyId, Guid adminId, string? notes);
    Task RejectAsync(Guid pharmacyId, Guid adminId, string? notes);
    Task<List<MeDto>> GetUsersAsync();
    Task<MeDto> UpdateUserRoleAsync(Guid userId, UserRole role, Guid adminId);
}

public class AdminService(AppDbContext db) : IAdminService
{
    public async Task<List<PharmacyDto>> GetPendingPharmaciesAsync()
    {
        var pharmacies = await db.Pharmacies
            .Where(x => x.Status == PharmacyStatus.Pending)
            .ToListAsync();

        return pharmacies.Select(p => new PharmacyDto
        {
            Id = p.Id,
            OwnerId = p.OwnerId,
            Name = p.Name,
            Address = p.Address,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            LicenseNumber = p.LicenseNumber,
            ContactPhone = p.ContactPhone,
            OperatingHours = p.OperatingHours?.RootElement.GetRawText(),
            Status = p.Status,
            CreatedAt = p.CreatedAt
        }).ToList();
    }

    public Task ApproveAsync(Guid pharmacyId, Guid adminId, string? notes) =>
        Decide(pharmacyId, adminId, ApprovalDecision.Approved, PharmacyStatus.Approved, notes);

    public Task RejectAsync(Guid pharmacyId, Guid adminId, string? notes) =>
        Decide(pharmacyId, adminId, ApprovalDecision.Rejected, PharmacyStatus.Rejected, notes);

    public Task<List<MeDto>> GetUsersAsync() =>
        db.Users.Select(u => new MeDto
        {
            UserId = u.Id,
            Email = u.Email,
            FullName = u.FullName,
            Phone = u.Phone,
            Role = u.Role,
            IsActive = u.IsActive
        }).ToListAsync();

    public async Task<MeDto> UpdateUserRoleAsync(Guid userId, UserRole role, Guid adminId)
    {
        if (role != UserRole.Admin && role != UserRole.Customer && role != UserRole.PharmacyOwner)
            throw ApiException.BadRequest(ApiErrorCodes.ValidationFailed, "Invalid role.");

        var user = await db.Users.FindAsync(userId)
                   ?? throw ApiException.NotFound(ApiErrorCodes.UserNotFound, "User not found.");

        if (user.Id == adminId && user.Role == UserRole.Admin && role != UserRole.Admin)
            throw ApiException.Forbidden(ApiErrorCodes.AdminCannotDemoteSelf, "You cannot demote your own admin account.");

        if (user.Role == UserRole.Admin && role != UserRole.Admin)
        {
            var adminCount = await db.Users.CountAsync(u => u.Role == UserRole.Admin);
            if (adminCount <= 1)
                throw ApiException.BadRequest(ApiErrorCodes.AdminLastAdmin, "Cannot demote the last admin account.");
        }

        user.Role = role;
        await db.SaveChangesAsync();

        return new MeDto
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Role = user.Role,
            IsActive = user.IsActive
        };
    }

    private async Task Decide(Guid pharmacyId, Guid adminId, ApprovalDecision decision, PharmacyStatus status, string? notes)
    {
        var p = await db.Pharmacies.FindAsync(pharmacyId)
                ?? throw ApiException.NotFound(ApiErrorCodes.PharmacyNotFound, "Pharmacy not found.");
        p.Status = status;
        db.PharmacyApprovals.Add(new PharmacyApproval
        {
            PharmacyId = pharmacyId,
            AdminId = adminId,
            Decision = decision,
            Notes = notes
        });
        await db.SaveChangesAsync();
    }
}
