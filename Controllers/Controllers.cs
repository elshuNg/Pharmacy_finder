using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyFinder.API.DTOs;
using PharmacyFinder.API.Helpers;
using PharmacyFinder.API.Models;
using PharmacyFinder.API.Services;

namespace PharmacyFinder.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService service) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto) => Ok(await service.RegisterAsync(dto));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto) => Ok(await service.LoginAsync(dto));

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeDto>> Me() => Ok(await service.GetMeAsync(User));
}

[ApiController]
[Route("api/pharmacies")]
public class PharmacyController(IPharmacyService service) : ControllerBase
{
    [Authorize(Roles = "PharmacyOwner")]
    [HttpPost]
    public async Task<ActionResult<PharmacyDto>> Create([FromBody] CreatePharmacyDto dto) =>
        Ok(await service.CreateAsync(User.GetUserId(), dto));

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<List<PharmacyDto>>> GetAll([FromQuery] PharmacyStatus? status) =>
        Ok(await service.GetAllAsync(status));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PharmacyDto>> GetById(Guid id) => Ok(await service.GetByIdAsync(id));

    [Authorize(Roles = "PharmacyOwner")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PharmacyDto>> Update(Guid id, [FromBody] CreatePharmacyDto dto) =>
        Ok(await service.UpdateAsync(id, User.GetUserId(), dto));

    [Authorize(Roles = "PharmacyOwner")]
    [HttpGet("my")]
    public async Task<ActionResult<List<PharmacyDto>>> Mine() => Ok(await service.GetMineAsync(User.GetUserId()));
}

[ApiController]
[Route("api/pharmacies/{pharmacyId:guid}/stock")]
public class StockController(IStockService service) : ControllerBase
{
    [Authorize(Roles = "PharmacyOwner")]
    [HttpPost]
    public async Task<ActionResult<PharmacyMedicineDto>> Add(Guid pharmacyId, [FromBody] UpdateStockDto dto) =>
        Ok(await service.AddAsync(pharmacyId, User.GetUserId(), dto));

    [Authorize(Roles = "PharmacyOwner")]
    [HttpPut("{medicineId:guid}")]
    public async Task<ActionResult<PharmacyMedicineDto>> Update(Guid pharmacyId, Guid medicineId, [FromBody] UpdateStockDto dto) =>
        Ok(await service.UpdateAsync(pharmacyId, medicineId, User.GetUserId(), dto));

    [Authorize(Roles = "PharmacyOwner")]
    [HttpDelete("{medicineId:guid}")]
    public async Task<IActionResult> Delete(Guid pharmacyId, Guid medicineId)
    {
        await service.RemoveAsync(pharmacyId, medicineId, User.GetUserId());
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<List<PharmacyMedicineDto>>> List(Guid pharmacyId) =>
        Ok(await service.GetByPharmacyAsync(pharmacyId));
}

[ApiController]
[Route("api/medicines")]
public class MedicineController(IMedicineService service) : ControllerBase
{
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<MedicineDto>> Create([FromBody] CreateMedicineDto dto) => Ok(await service.CreateAsync(dto));

    [HttpGet]
    public async Task<ActionResult<List<MedicineDto>>> Search([FromQuery] string? name, [FromQuery] string? category) =>
        Ok(await service.SearchAsync(name, category));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MedicineDto>> Get(Guid id) => Ok(await service.GetAsync(id));

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MedicineDto>> Update(Guid id, [FromBody] CreateMedicineDto dto) =>
        Ok(await service.UpdateAsync(id, dto));
}

[ApiController]
[Route("api/search")]
public class SearchController(ISearchService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SearchResultDto>>> Search([FromQuery] SearchQueryDto query) =>
        Ok(await service.SearchAsync(query.MedicineName, query.Lat, query.Lng));
}

[ApiController]
[Route("api/prescriptions")]
public class PrescriptionController(IPrescriptionService service) : ControllerBase
{
    [Authorize(Roles = "Customer")]
    [HttpPost]
    public async Task<ActionResult<PrescriptionDto>> Create([FromBody] CreatePrescriptionDto dto) =>
        Ok(await service.CreateAsync(User.GetUserId(), dto));

    /// <summary>Upload a prescription image; runs OCR and returns detected medicines for user confirmation.</summary>
    [Authorize(Roles = "Customer")]
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PrescriptionUploadResultDto>> Upload(
        [FromForm] PrescriptionUploadFormDto dto,
        CancellationToken cancellationToken) =>
        Ok(await service.UploadForReviewAsync(User.GetUserId(), dto.File!, cancellationToken));

    /// <summary>Confirm detected medicines and search nearby pharmacies in stock.</summary>
    [Authorize(Roles = "Customer")]
    [HttpPost("{id:guid}/confirm-search")]
    public async Task<ActionResult<List<SearchResultDto>>> ConfirmAndSearch(
        Guid id,
        [FromBody] ConfirmPrescriptionSearchDto dto,
        CancellationToken cancellationToken) =>
        Ok(await service.ConfirmAndSearchAsync(id, User.GetUserId(), dto, cancellationToken));

    [Authorize(Roles = "Customer")]
    [HttpGet]
    public async Task<ActionResult<List<PrescriptionDto>>> Mine() => Ok(await service.GetMineAsync(User.GetUserId()));

    [Authorize(Roles = "Customer")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PrescriptionDto>> Get(Guid id) => Ok(await service.GetByIdAsync(id, User.GetUserId()));

    [Authorize(Roles = "Customer")]
    [HttpPut("{id:guid}/items")]
    public async Task<ActionResult<PrescriptionDto>> UpsertItems(Guid id, [FromBody] UpsertPrescriptionItemsDto dto) =>
        Ok(await service.UpsertItemsAsync(id, User.GetUserId(), dto));
}

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(IAdminService service) : ControllerBase
{
    [HttpGet("pharmacies")]
    public async Task<ActionResult<List<PharmacyDto>>> PendingPharmacies() => Ok(await service.GetPendingPharmaciesAsync());

    [HttpPost("pharmacies/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApprovalDto dto)
    {
        await service.ApproveAsync(id, User.GetUserId(), dto.Notes);
        return Ok();
    }

    [HttpPost("pharmacies/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ApprovalDto dto)
    {
        await service.RejectAsync(id, User.GetUserId(), dto.Notes);
        return Ok();
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<MeDto>>> Users() => Ok(await service.GetUsersAsync());

    [HttpPut("users/{userId:guid}/role")]
    public async Task<ActionResult<MeDto>> UpdateUserRole(Guid userId, [FromBody] UpdateUserRoleDto dto) =>
        Ok(await service.UpdateUserRoleAsync(userId, dto.Role, User.GetUserId()));
}
