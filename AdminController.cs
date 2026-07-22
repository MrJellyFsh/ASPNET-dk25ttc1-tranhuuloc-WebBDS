using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhaTot.Data;
using NhaTot.Models;
using NhaTot.Models.Enums;
using NhaTot.ViewModels;
using System.Security.Claims;

namespace NhaTot.Controllers;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<AdminController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("Admin")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var model = new AdminDashboardViewModel
            {
                PendingAgentApplications = await PendingApplications().ToListAsync(),
                PendingProperties = await PendingProperties().ToListAsync()
            };
            return View(model);
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpGet("Admin/AgentApplications")]
    public async Task<IActionResult> AgentApplications()
    {
        try { return View(await PendingApplications().ToListAsync()); }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpGet("Admin/AgentApplications/{id:int}")]
    public async Task<IActionResult> AgentApplicationDetails(int id)
    {
        try
        {
            var model = await _context.AgentApplications.AsNoTracking()
                .Where(application => application.Id == id && application.Status == AgentApplicationStatus.PendingReview)
                .Select(application => new AdminAgentApplicationDetailsViewModel
                {
                    Id = application.Id, FullName = application.FullName, PhoneNumber = application.PhoneNumber,
                    Email = application.User.Email, CompanyName = application.CompanyName, OperatingArea = application.OperatingArea,
                    YearsOfExperience = application.YearsOfExperience, Introduction = application.Introduction,
                    VerificationDocumentPath = application.VerificationDocumentPath, CreatedAtUtc = application.CreatedAtUtc
                }).SingleOrDefaultAsync();
            return model is null ? NotFound() : View(model);
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpPost("Admin/AgentApplications/{id:int}/Approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveAgentApplication(int id)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null) return Challenge();
        try
        {
            var application = await _context.AgentApplications.Include(item => item.User)
                .SingleOrDefaultAsync(item => item.Id == id && item.Status == AgentApplicationStatus.PendingReview);
            if (application is null) return NotFound();
            if (application.UserId == reviewerId) return Forbid();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var profile = await _context.AgentProfiles.SingleOrDefaultAsync(item => item.UserId == application.UserId);
            if (profile is null)
            {
                profile = new AgentProfile { UserId = application.UserId };
                _context.AgentProfiles.Add(profile);
            }
            profile.DisplayName = application.FullName;
            profile.CompanyName = application.CompanyName;
            profile.OperatingArea = application.OperatingArea;
            profile.YearsOfExperience = application.YearsOfExperience;
            profile.Biography = application.Introduction;
            profile.ApprovedAtUtc = DateTime.UtcNow;

            if (!await _userManager.IsInRoleAsync(application.User, RoleNames.Agent))
            {
                var result = await _userManager.AddToRoleAsync(application.User, RoleNames.Agent);
                if (!result.Succeeded) throw new InvalidOperationException($"Không thể gán vai trò môi giới: {string.Join("; ", result.Errors.Select(error => error.Description))}");
            }

            application.Status = AgentApplicationStatus.Approved;
            application.RejectionReason = null;
            application.ReviewedAtUtc = DateTime.UtcNow;
            application.ReviewedByUserId = reviewerId;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["SuccessMessage"] = "Đã duyệt hồ sơ và cấp quyền môi giới.";
            return RedirectToAction(nameof(AgentApplications));
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không thể duyệt hồ sơ môi giới {ApplicationId}.", id);
            TempData["ErrorMessage"] = "Không thể duyệt hồ sơ lúc này. Vui lòng thử lại.";
            return RedirectToAction(nameof(AgentApplicationDetails), new { id });
        }
    }

    [HttpPost("Admin/AgentApplications/{id:int}/Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectAgentApplication(int id, RejectionInput input)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null) return Challenge();
        if (!ModelState.IsValid) { TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối hợp lệ."; return RedirectToAction(nameof(AgentApplicationDetails), new { id }); }
        try
        {
            var application = await _context.AgentApplications.SingleOrDefaultAsync(item => item.Id == id && item.Status == AgentApplicationStatus.PendingReview);
            if (application is null) return NotFound();
            if (application.UserId == reviewerId) return Forbid();
            application.Status = AgentApplicationStatus.Rejected;
            application.RejectionReason = input.Reason.Trim();
            application.ReviewedAtUtc = DateTime.UtcNow;
            application.ReviewedByUserId = reviewerId;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã từ chối hồ sơ môi giới.";
            return RedirectToAction(nameof(AgentApplications));
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpGet("Admin/Properties")]
    public async Task<IActionResult> Properties()
    {
        try { return View(await PendingProperties().ToListAsync()); }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpGet("Admin/Properties/{id:int}")]
    public async Task<IActionResult> PropertyDetails(int id)
    {
        try
        {
            var model = await _context.Properties.AsNoTracking().Where(property => property.Id == id && property.Status == PropertyStatus.PendingReview)
                .Select(property => new AdminPropertyModerationDetailsViewModel
                {
                    Id = property.Id, Title = property.Title, Purpose = property.Purpose, PropertyTypeName = property.PropertyType.Name,
                    Price = property.Price, AreaSquareMeters = property.AreaSquareMeters, Bedrooms = property.Bedrooms, Bathrooms = property.Bathrooms,
                    ProvinceName = property.Province.Name, DistrictName = property.District.Name, Address = property.Address,
                    ApproximateLocation = property.ApproximateLocation, Description = property.Description,
                    ImageUrls = property.Images.OrderBy(image => image.DisplayOrder).Select(image => image.FilePath).ToList(),
                    Amenities = property.PropertyAmenities.OrderBy(item => item.Amenity.Name).Select(item => item.Amenity.Name).ToList()
                }).SingleOrDefaultAsync();
            return model is null ? NotFound() : View(model);
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpPost("Admin/Properties/{id:int}/Approve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ApproveProperty(int id) => ModeratePropertyAsync(id, PropertyStatus.Approved, null);

    [HttpPost("Admin/Properties/{id:int}/Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectProperty(int id, RejectionInput input)
    {
        if (!ModelState.IsValid) { TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối hợp lệ."; return RedirectToAction(nameof(PropertyDetails), new { id }); }
        return await ModeratePropertyAsync(id, PropertyStatus.Rejected, input.Reason.Trim());
    }

    private async Task<IActionResult> ModeratePropertyAsync(int id, PropertyStatus targetStatus, string? note)
    {
        var reviewerId = GetCurrentUserId();
        if (reviewerId is null) return Challenge();
        try
        {
            var property = await _context.Properties.SingleOrDefaultAsync(item => item.Id == id && item.Status == PropertyStatus.PendingReview);
            if (property is null) return NotFound();
            var now = DateTime.UtcNow;
            property.Status = targetStatus;
            property.ReviewedAtUtc = now;
            property.ReviewedByUserId = reviewerId;
            property.RejectionReason = targetStatus == PropertyStatus.Rejected ? note : null;
            property.PublishedAtUtc = targetStatus == PropertyStatus.Approved ? now : property.PublishedAtUtc;
            property.UpdatedAtUtc = now;
            _context.ModerationRecords.Add(new ModerationRecord { PropertyId = property.Id, ModeratorUserId = reviewerId, PreviousStatus = PropertyStatus.PendingReview, NewStatus = targetStatus, Note = note, CreatedAtUtc = now });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = targetStatus == PropertyStatus.Approved ? "Đã duyệt và công khai tin đăng." : "Đã từ chối tin đăng.";
            return RedirectToAction(nameof(Properties));
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    private IQueryable<AdminAgentApplicationSummaryViewModel> PendingApplications() => _context.AgentApplications.AsNoTracking()
        .Where(application => application.Status == AgentApplicationStatus.PendingReview).OrderBy(application => application.CreatedAtUtc)
        .Select(application => new AdminAgentApplicationSummaryViewModel { Id = application.Id, FullName = application.FullName, OperatingArea = application.OperatingArea, CompanyName = application.CompanyName, CreatedAtUtc = application.CreatedAtUtc });

    private IQueryable<AdminPropertyModerationSummaryViewModel> PendingProperties() => _context.Properties.AsNoTracking()
        .Where(property => property.Status == PropertyStatus.PendingReview).OrderBy(property => property.UpdatedAtUtc)
        .Select(property => new AdminPropertyModerationSummaryViewModel { Id = property.Id, Title = property.Title, PropertyTypeName = property.PropertyType.Name, ApproximateLocation = property.ApproximateLocation ?? property.District.Name, Price = property.Price, UpdatedAtUtc = property.UpdatedAtUtc });

    private IActionResult DatabaseUnavailable(Exception exception)
    {
        _logger.LogWarning(exception, "Không thể kết nối cơ sở dữ liệu khi quản trị và kiểm duyệt.");
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View("DatabaseUnavailable");
    }

    private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private static bool IsDatabaseUnavailable(Exception exception) => exception is Microsoft.Data.SqlClient.SqlException || exception.InnerException is Microsoft.Data.SqlClient.SqlException;
}
