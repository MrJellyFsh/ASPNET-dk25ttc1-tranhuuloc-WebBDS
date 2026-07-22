using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhaTot.Data;
using NhaTot.Models;
using NhaTot.Models.Enums;
using NhaTot.ViewModels;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace NhaTot.Controllers;

[Authorize]
public sealed class AgentListingsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AgentListingsController> _logger;

    public AgentListingsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<AgentListingsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("AgentDashboard")]
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();
        if (!await IsAgentAsync(userId)) return Forbid();

        try
        {
            var model = new AgentDashboardViewModel
            {
                Properties = await _context.Properties.AsNoTracking().Where(property => property.AgentUserId == userId)
                    .OrderByDescending(property => property.UpdatedAtUtc)
                    .Select(property => new AgentListingSummaryViewModel { Id = property.Id, Title = property.Title, Slug = property.Slug, Status = property.Status, CreatedAtUtc = property.CreatedAtUtc, UpdatedAtUtc = property.UpdatedAtUtc, PropertyTypeName = property.PropertyType.Name, Price = property.Price })
                    .ToListAsync()
            };
            return View(model);
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpGet("AgentListings/Create")]
    public async Task<IActionResult> Create()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();
        if (!await IsAgentAsync(userId)) return Forbid();
        try { return View("Edit", await BuildEditModelAsync(0, new AgentPropertyInput(), PropertyStatus.Draft)); }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpPost("AgentListings/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AgentPropertyInput input)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();
        if (!await IsAgentAsync(userId)) return Forbid();
        try
        {
            await ValidateInputAsync(input);
            if (!ModelState.IsValid) return View("Edit", await BuildEditModelAsync(0, input, PropertyStatus.Draft));

            var property = MapInput(new Property { AgentUserId = userId, Status = PropertyStatus.Draft, CreatedAtUtc = DateTime.UtcNow }, input);
            property.Slug = await CreateUniqueSlugAsync(property.Title);
            _context.Properties.Add(property);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã lưu tin đăng ở trạng thái nháp.";
            return RedirectToAction(nameof(Edit), new { id = property.Id });
        }
        catch (DbUpdateException exception) { return await HandleWriteFailureAsync(exception, 0, input, PropertyStatus.Draft); }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpGet("AgentListings/{id:int}/Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();
        if (!await IsAgentAsync(userId)) return Forbid();
        try
        {
            var property = await OwnedPropertyAsync(id, userId);
            if (property is null) return NotFound();
            if (!CanEdit(property.Status)) { TempData["InfoMessage"] = "Tin đăng đang chờ duyệt hoặc đã được duyệt nên chưa thể chỉnh sửa."; return RedirectToAction(nameof(Index)); }
            return View(await BuildEditModelAsync(property.Id, ToInput(property), property.Status, property.Images));
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpPost("AgentListings/{id:int}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AgentPropertyInput input)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();
        if (!await IsAgentAsync(userId)) return Forbid();
        try
        {
            var property = await OwnedPropertyAsync(id, userId);
            if (property is null) return NotFound();
            if (!CanEdit(property.Status)) return Forbid();
            await ValidateInputAsync(input);
            if (!ModelState.IsValid) return View(await BuildEditModelAsync(id, input, property.Status, property.Images));
            MapInput(property, input); property.UpdatedAtUtc = DateTime.UtcNow;
            if (!string.Equals(Slugify(property.Title), property.Slug, StringComparison.Ordinal)) property.Slug = await CreateUniqueSlugAsync(property.Title, property.Id);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã cập nhật bản nháp.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (DbUpdateException exception) { return await HandleWriteFailureAsync(exception, id, input, PropertyStatus.Draft); }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpGet("AgentListings/{id:int}/Preview")]
    public async Task<IActionResult> Preview(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();
        if (!await IsAgentAsync(userId)) return Forbid();
        try
        {
            var model = await _context.Properties.AsNoTracking().Where(property => property.Id == id && property.AgentUserId == userId)
                .Select(property => new AgentPropertyPreviewViewModel { Title = property.Title, Slug = property.Slug, PropertyTypeName = property.PropertyType.Name, ProvinceName = property.Province.Name, DistrictName = property.District.Name, Address = property.Address, ApproximateLocation = property.ApproximateLocation, Purpose = property.Purpose, Status = property.Status, Price = property.Price, AreaSquareMeters = property.AreaSquareMeters, Bedrooms = property.Bedrooms, Bathrooms = property.Bathrooms, Description = property.Description, Images = property.Images.OrderBy(image => image.DisplayOrder).Select(image => new AgentPropertyImageViewModel(image.Id, image.FilePath, image.AltText, image.IsCover)).ToList(), Amenities = property.PropertyAmenities.Select(item => item.Amenity.Name).ToList() }).SingleOrDefaultAsync();
            return model is null ? NotFound() : View(model);
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpPost("AgentListings/{id:int}/Submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();
        if (!await IsAgentAsync(userId)) return Forbid();
        try
        {
            var property = await OwnedPropertyAsync(id, userId);
            if (property is null) return NotFound();
            if (!CanEdit(property.Status)) { TempData["InfoMessage"] = "Tin đăng này không thể gửi duyệt ở trạng thái hiện tại."; return RedirectToAction(nameof(Index)); }
            if (!await IsListingStillValidForSubmissionAsync(property))
            {
                TempData["ErrorMessage"] = "Tin đăng có dữ liệu danh mục không còn hợp lệ. Vui lòng cập nhật lại bản nháp.";
                return RedirectToAction(nameof(Edit), new { id });
            }
            property.Status = PropertyStatus.PendingReview;
            property.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã gửi tin đăng để xét duyệt.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpPost("AgentListings/{id:int}/Images")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddImage(int id, AgentPropertyImageInput input)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();
        if (!await IsAgentAsync(userId)) return Forbid();
        try
        {
            var property = await OwnedPropertyAsync(id, userId);
            if (property is null) return NotFound();
            if (!CanEdit(property.Status)) return Forbid();
            if (!IsSafeImageUrl(input.FilePath)) ModelState.AddModelError(nameof(input.FilePath), "Liên kết ảnh phải là URL HTTPS hợp lệ.");
            if (!ModelState.IsValid) { TempData["ErrorMessage"] = "Không thể thêm ảnh. Vui lòng kiểm tra lại liên kết ảnh HTTPS."; return RedirectToAction(nameof(Edit), new { id }); }
            if (input.IsCover) foreach (var image in property.Images) image.IsCover = false;
            property.Images.Add(new PropertyImage { FilePath = input.FilePath.Trim(), AltText = TrimToNull(input.AltText), IsCover = input.IsCover || !property.Images.Any(), DisplayOrder = property.Images.Count == 0 ? 0 : property.Images.Max(image => image.DisplayOrder) + 1 });
            property.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã thêm ảnh cho tin đăng.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    [HttpPost("AgentListings/{id:int}/Images/{imageId:int}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int id, int imageId)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();
        if (!await IsAgentAsync(userId)) return Forbid();
        try
        {
            var property = await OwnedPropertyAsync(id, userId);
            if (property is null) return NotFound();
            if (!CanEdit(property.Status)) return Forbid();
            var image = property.Images.SingleOrDefault(item => item.Id == imageId);
            if (image is null) return NotFound();
            var wasCover = image.IsCover;
            _context.PropertyImages.Remove(image);
            if (wasCover) { var next = property.Images.Where(item => item.Id != imageId).OrderBy(item => item.DisplayOrder).FirstOrDefault(); if (next is not null) next.IsCover = true; }
            property.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa ảnh khỏi tin đăng.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception)) { return DatabaseUnavailable(exception); }
    }

    private async Task<bool> IsAgentAsync(string userId)
    {
        if (await _context.AgentProfiles.AsNoTracking().AnyAsync(profile => profile.UserId == userId)) return true;
        var user = await _userManager.FindByIdAsync(userId);
        return user is not null && await _userManager.IsInRoleAsync(user, RoleNames.Agent);
    }

    private Task<Property?> OwnedPropertyAsync(int id, string userId) => _context.Properties.Include(property => property.Images).Include(property => property.PropertyAmenities)
        .SingleOrDefaultAsync(property => property.Id == id && property.AgentUserId == userId);

    private async Task ValidateInputAsync(AgentPropertyInput input)
    {
        if (input.Purpose is null || !Enum.IsDefined(input.Purpose.Value)) ModelState.AddModelError(nameof(input.Purpose), "Vui lòng chọn nhu cầu hợp lệ.");
        if (!await _context.PropertyTypes.AsNoTracking().AnyAsync(type => type.Id == input.PropertyTypeId && type.IsActive)) ModelState.AddModelError(nameof(input.PropertyTypeId), "Loại bất động sản không còn khả dụng.");
        if (!await _context.Provinces.AsNoTracking().AnyAsync(province => province.Id == input.ProvinceId)) ModelState.AddModelError(nameof(input.ProvinceId), "Tỉnh/thành không hợp lệ.");
        if (!await _context.Districts.AsNoTracking().AnyAsync(district => district.Id == input.DistrictId && district.ProvinceId == input.ProvinceId)) ModelState.AddModelError(nameof(input.DistrictId), "Quận/huyện không thuộc tỉnh/thành đã chọn.");
        input.AmenityIds = input.AmenityIds.Distinct().ToList();
        var validAmenities = await _context.Amenities.AsNoTracking().Where(amenity => input.AmenityIds.Contains(amenity.Id)).Select(amenity => amenity.Id).ToListAsync();
        if (validAmenities.Count != input.AmenityIds.Count) ModelState.AddModelError(nameof(input.AmenityIds), "Có tiện ích không hợp lệ.");
    }

    private async Task<bool> IsListingStillValidForSubmissionAsync(Property property)
    {
        if (!await _context.PropertyTypes.AsNoTracking().AnyAsync(type => type.Id == property.PropertyTypeId && type.IsActive)) return false;
        if (!await _context.Districts.AsNoTracking().AnyAsync(district => district.Id == property.DistrictId && district.ProvinceId == property.ProvinceId)) return false;
        return await _context.PropertyAmenities.AsNoTracking().Where(item => item.PropertyId == property.Id)
            .AllAsync(item => _context.Amenities.Any(amenity => amenity.Id == item.AmenityId));
    }

    private async Task<AgentPropertyEditViewModel> BuildEditModelAsync(int id, AgentPropertyInput input, PropertyStatus status, IEnumerable<PropertyImage>? images = null)
    {
        var provinces = await _context.Provinces.AsNoTracking().OrderBy(item => item.Name).Select(item => new LookupOption(item.Id, item.Name)).ToListAsync();
        var districts = await _context.Districts.AsNoTracking().Where(item => item.ProvinceId == input.ProvinceId).OrderBy(item => item.Name).Select(item => new LookupOption(item.Id, item.Name)).ToListAsync();
        var types = await _context.PropertyTypes.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name).Select(item => new LookupOption(item.Id, item.Name)).ToListAsync();
        var amenities = await _context.Amenities.AsNoTracking().OrderBy(item => item.Name).Select(item => new LookupOption(item.Id, item.Name)).ToListAsync();
        return new AgentPropertyEditViewModel { Id = id, Status = status, Input = input, Provinces = provinces, Districts = districts, PropertyTypes = types, Amenities = amenities, Images = images?.OrderBy(item => item.DisplayOrder).Select(item => new AgentPropertyImageViewModel(item.Id, item.FilePath, item.AltText, item.IsCover)).ToList() ?? [] };
    }

    private static AgentPropertyInput ToInput(Property property) => new() { Title = property.Title, Purpose = property.Purpose, PropertyTypeId = property.PropertyTypeId, Price = property.Price, AreaSquareMeters = property.AreaSquareMeters, Bedrooms = property.Bedrooms, Bathrooms = property.Bathrooms, ProvinceId = property.ProvinceId, DistrictId = property.DistrictId, Address = property.Address, ApproximateLocation = property.ApproximateLocation, Description = property.Description, AmenityIds = property.PropertyAmenities.Select(item => item.AmenityId).ToList() };
    private static Property MapInput(Property property, AgentPropertyInput input) { property.Title = input.Title.Trim(); property.Purpose = input.Purpose!.Value; property.PropertyTypeId = input.PropertyTypeId; property.Price = input.Price; property.AreaSquareMeters = input.AreaSquareMeters; property.Bedrooms = input.Bedrooms; property.Bathrooms = input.Bathrooms; property.ProvinceId = input.ProvinceId; property.DistrictId = input.DistrictId; property.Address = input.Address.Trim(); property.ApproximateLocation = TrimToNull(input.ApproximateLocation); property.Description = input.Description.Trim(); property.PropertyAmenities.Clear(); foreach (var amenityId in input.AmenityIds) property.PropertyAmenities.Add(new PropertyAmenity { AmenityId = amenityId }); return property; }
    private async Task<string> CreateUniqueSlugAsync(string title, int? currentId = null) { var root = Slugify(title); var slug = root; for (var suffix = 2; await _context.Properties.AsNoTracking().AnyAsync(item => item.Slug == slug && item.Id != currentId); suffix++) slug = $"{root}-{suffix}"; return slug; }
    private static string Slugify(string value) { var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD); var builder = new StringBuilder(); foreach (var character in normalized) { var category = CharUnicodeInfo.GetUnicodeCategory(character); if (category != UnicodeCategory.NonSpacingMark) builder.Append(character); } var slug = Regex.Replace(builder.ToString().Replace('đ', 'd'), "[^a-z0-9]+", "-").Trim('-'); return string.IsNullOrWhiteSpace(slug) ? "tin-dang" : slug[..Math.Min(slug.Length, 200)]; }
    private async Task<IActionResult> HandleWriteFailureAsync(Exception exception, int id, AgentPropertyInput input, PropertyStatus status) { _logger.LogWarning(exception, "Không thể lưu tin đăng {PropertyId}.", id); ModelState.AddModelError(string.Empty, "Không thể lưu tin đăng lúc này. Vui lòng thử lại; tiêu đề có thể đang được sử dụng."); return View("Edit", await BuildEditModelAsync(id, input, status)); }
    private IActionResult DatabaseUnavailable(Exception exception) { _logger.LogWarning(exception, "Không thể kết nối cơ sở dữ liệu khi quản lý tin đăng."); Response.StatusCode = StatusCodes.Status503ServiceUnavailable; return View("DatabaseUnavailable"); }
    private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private static bool CanEdit(PropertyStatus status) => status is PropertyStatus.Draft or PropertyStatus.Rejected;
    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsSafeImageUrl(string value) => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    private static bool IsDatabaseUnavailable(Exception exception) => exception is Microsoft.Data.SqlClient.SqlException || exception.InnerException is Microsoft.Data.SqlClient.SqlException;
}
