using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhaTot.Data;
using NhaTot.Models;
using NhaTot.Models.Enums;
using NhaTot.ViewModels;
using System.Security.Claims;

namespace NhaTot.Controllers;

public class PropertiesController : Controller
{
    private const int PageSize = 12;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PropertiesController> _logger;

    public PropertiesController(ApplicationDbContext context, ILogger<PropertiesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PropertySearchQuery query)
    {
        Normalize(query);
        try
        {
            var model = await BuildSearchViewModelAsync(query);
            return View(model);
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception))
        {
            _logger.LogWarning(exception, "Không thể kết nối cơ sở dữ liệu khi tải tìm kiếm bất động sản.");
            return View(new PropertySearchViewModel { Query = query, IsDatabaseAvailable = false });
        }
    }

    [HttpGet("Properties/Details/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        try
        {
            var model = await BuildDetailViewModelAsync(slug, GetCurrentUserId());
            return model is null ? NotFound() : View(model);
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception))
        {
            _logger.LogWarning(exception, "Không thể kết nối cơ sở dữ liệu khi tải chi tiết tin đăng.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("DatabaseUnavailable");
        }
    }

    [Authorize]
    [HttpPost("Properties/Details/{slug}/favorite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(string slug)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();

        try
        {
            var propertyId = await FindPublicPropertyIdAsync(slug);
            if (!propertyId.HasValue) return NotFound();

            var favorite = await _context.Favorites
                .SingleOrDefaultAsync(item => item.UserId == userId && item.PropertyId == propertyId.Value);
            if (favorite is null)
            {
                _context.Favorites.Add(new Favorite { UserId = userId, PropertyId = propertyId.Value });
                TempData["SuccessMessage"] = "Đã lưu tin đăng.";
            }
            else
            {
                _context.Favorites.Remove(favorite);
                TempData["SuccessMessage"] = "Đã bỏ lưu tin đăng.";
            }

            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Không thể cập nhật tin đã lưu cho người dùng {UserId}.", userId);
            TempData["ErrorMessage"] = "Không thể cập nhật tin đã lưu lúc này. Vui lòng thử lại.";
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception))
        {
            _logger.LogWarning(exception, "Không thể kết nối cơ sở dữ liệu khi cập nhật tin đã lưu.");
            TempData["ErrorMessage"] = "Dữ liệu đang tạm thời chưa sẵn sàng. Vui lòng thử lại sau.";
        }

        return RedirectToAction(nameof(Details), new { slug });
    }

    [Authorize]
    [HttpGet("Properties/Saved")]
    public async Task<IActionResult> Saved()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();

        try
        {
            var now = DateTime.UtcNow;
            var properties = await ProjectCards(_context.Favorites.AsNoTracking()
                    .Where(favorite => favorite.UserId == userId
                        && favorite.Property.Status == PropertyStatus.Approved
                        && (favorite.Property.ExpiresAtUtc == null || favorite.Property.ExpiresAtUtc > now))
                    .OrderByDescending(favorite => favorite.CreatedAtUtc)
                    .Select(favorite => favorite.Property))
                .ToListAsync();
            return View(new SavedPropertiesViewModel { Properties = properties });
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception))
        {
            _logger.LogWarning(exception, "Không thể kết nối cơ sở dữ liệu khi tải tin đã lưu.");
            return View(new SavedPropertiesViewModel { IsDatabaseAvailable = false });
        }
    }

    [Authorize]
    [HttpPost("Properties/Details/{slug}/contact")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(string slug, [Bind(Prefix = "ContactRequest")] ContactRequestInput input)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Challenge();

        try
        {
            var propertyId = await FindPublicPropertyIdAsync(slug);
            if (!propertyId.HasValue) return NotFound();

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildDetailViewModelAsync(slug, userId, input);
                return invalidModel is null ? NotFound() : View("Details", invalidModel);
            }

            _context.ContactRequests.Add(new ContactRequest
            {
                PropertyId = propertyId.Value,
                RequesterUserId = userId,
                FullName = input.FullName.Trim(),
                PhoneNumber = input.PhoneNumber.Trim(),
                Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim(),
                Message = input.Message.Trim()
            });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Yêu cầu liên hệ đã được gửi. Chúng tôi sẽ chuyển thông tin đến người đăng tin.";
            return RedirectToAction(nameof(Details), new { slug });
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Không thể lưu yêu cầu liên hệ cho người dùng {UserId}.", userId);
            ModelState.AddModelError(string.Empty, "Không thể gửi yêu cầu lúc này. Vui lòng thử lại.");
        }
        catch (Exception exception) when (IsDatabaseUnavailable(exception))
        {
            _logger.LogWarning(exception, "Không thể kết nối cơ sở dữ liệu khi gửi yêu cầu liên hệ.");
            ModelState.AddModelError(string.Empty, "Dữ liệu đang tạm thời chưa sẵn sàng. Vui lòng thử lại sau.");
        }

        var model = await BuildDetailViewModelAsync(slug, userId, input);
        return model is null ? NotFound() : View("Details", model);
    }

    internal async Task<PropertySearchViewModel> BuildSearchViewModelAsync(PropertySearchQuery query)
    {
        var now = DateTime.UtcNow;
        IQueryable<Property> properties = _context.Properties.AsNoTracking()
            .Where(property => property.Status == PropertyStatus.Approved
                && (property.ExpiresAtUtc == null || property.ExpiresAtUtc > now));

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            properties = properties.Where(property => property.Title.Contains(keyword)
                || property.Description.Contains(keyword)
                || property.ApproximateLocation!.Contains(keyword));
        }
        if (query.Purpose.HasValue) properties = properties.Where(property => property.Purpose == query.Purpose);
        if (query.ProvinceId.HasValue) properties = properties.Where(property => property.ProvinceId == query.ProvinceId);
        if (query.DistrictId.HasValue) properties = properties.Where(property => property.DistrictId == query.DistrictId);
        if (query.PropertyTypeId.HasValue) properties = properties.Where(property => property.PropertyTypeId == query.PropertyTypeId);
        if (query.MinPrice.HasValue) properties = properties.Where(property => property.Price >= query.MinPrice);
        if (query.MaxPrice.HasValue) properties = properties.Where(property => property.Price <= query.MaxPrice);
        if (query.MinArea.HasValue) properties = properties.Where(property => property.AreaSquareMeters >= query.MinArea);
        if (query.MaxArea.HasValue) properties = properties.Where(property => property.AreaSquareMeters <= query.MaxArea);
        if (query.Bedrooms.HasValue) properties = properties.Where(property => property.Bedrooms >= query.Bedrooms);

        properties = query.Sort switch
        {
            "price-asc" => properties.OrderBy(property => property.Price).ThenByDescending(property => property.PublishedAtUtc),
            "price-desc" => properties.OrderByDescending(property => property.Price).ThenByDescending(property => property.PublishedAtUtc),
            "area-desc" => properties.OrderByDescending(property => property.AreaSquareMeters).ThenByDescending(property => property.PublishedAtUtc),
            _ => properties.OrderByDescending(property => property.PublishedAtUtc).ThenByDescending(property => property.Id)
        };

        var totalCount = await properties.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        if (query.Page > totalPages) query.Page = totalPages;

        var cards = await ProjectCards(properties)
            .Skip((query.Page - 1) * PageSize).Take(PageSize).ToListAsync();
        var provinces = await _context.Provinces.AsNoTracking().OrderBy(province => province.Name)
            .Select(province => new LookupOption(province.Id, province.Name)).ToListAsync();
        var districtsQuery = _context.Districts.AsNoTracking();
        if (query.ProvinceId.HasValue) districtsQuery = districtsQuery.Where(district => district.ProvinceId == query.ProvinceId);
        var districts = await districtsQuery.OrderBy(district => district.Name)
            .Select(district => new LookupOption(district.Id, district.Name)).ToListAsync();
        var propertyTypes = await _context.PropertyTypes.AsNoTracking().Where(type => type.IsActive).OrderBy(type => type.Name)
            .Select(type => new LookupOption(type.Id, type.Name)).ToListAsync();

        return new PropertySearchViewModel { Query = query, Properties = cards, Provinces = provinces, Districts = districts, PropertyTypes = propertyTypes, TotalCount = totalCount, TotalPages = totalPages };
    }

    internal static IQueryable<PropertyCardViewModel> ProjectCards(IQueryable<Property> properties) =>
        properties.Select(property => new PropertyCardViewModel
        {
            Id = property.Id, Title = property.Title, Slug = property.Slug, Purpose = property.Purpose,
            Price = property.Price, AreaSquareMeters = property.AreaSquareMeters, Bedrooms = property.Bedrooms,
            Bathrooms = property.Bathrooms, PropertyTypeName = property.PropertyType.Name,
            ProvinceName = property.Province.Name, DistrictName = property.District.Name,
            ApproximateLocation = property.ApproximateLocation,
            CoverImagePath = property.Images.Where(image => image.IsCover).OrderBy(image => image.DisplayOrder).Select(image => image.FilePath).FirstOrDefault()
                ?? property.Images.OrderBy(image => image.DisplayOrder).Select(image => image.FilePath).FirstOrDefault(),
            PublishedAtUtc = property.PublishedAtUtc ?? property.CreatedAtUtc
        });

    private async Task<PropertyDetailViewModel?> BuildDetailViewModelAsync(string? slug, string? userId, ContactRequestInput? contactRequest = null)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 220) return null;

        var now = DateTime.UtcNow;
        var model = await _context.Properties.AsNoTracking()
            .Where(property => property.Slug == slug
                && property.Status == PropertyStatus.Approved
                && (property.ExpiresAtUtc == null || property.ExpiresAtUtc > now))
            .Select(property => new PropertyDetailViewModel
            {
                Title = property.Title,
                Slug = property.Slug,
                PropertyTypeName = property.PropertyType.Name,
                ProvinceName = property.Province.Name,
                DistrictName = property.District.Name,
                ApproximateLocation = property.ApproximateLocation,
                Purpose = property.Purpose,
                Price = property.Price,
                AreaSquareMeters = property.AreaSquareMeters,
                Bedrooms = property.Bedrooms,
                Bathrooms = property.Bathrooms,
                Description = property.Description,
                Images = property.Images.OrderBy(image => image.DisplayOrder)
                    .Select(image => new PropertyImageViewModel(image.FilePath, image.AltText)).ToList(),
                Amenities = property.PropertyAmenities.OrderBy(item => item.Amenity.Name)
                    .Select(item => item.Amenity.Name).ToList(),
                ContactRequest = contactRequest ?? new ContactRequestInput()
            })
            .SingleOrDefaultAsync();

        if (model is not null && userId is not null)
        {
            model = new PropertyDetailViewModel
            {
                Title = model.Title, Slug = model.Slug, PropertyTypeName = model.PropertyTypeName,
                ProvinceName = model.ProvinceName, DistrictName = model.DistrictName, ApproximateLocation = model.ApproximateLocation,
                Purpose = model.Purpose, Price = model.Price, AreaSquareMeters = model.AreaSquareMeters,
                Bedrooms = model.Bedrooms, Bathrooms = model.Bathrooms, Description = model.Description,
                Images = model.Images, Amenities = model.Amenities, ContactRequest = model.ContactRequest,
                IsFavorite = await _context.Favorites.AsNoTracking().AnyAsync(favorite => favorite.UserId == userId && favorite.Property.Slug == model.Slug)
            };
        }

        return model;
    }

    private async Task<int?> FindPublicPropertyIdAsync(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 220) return null;
        var now = DateTime.UtcNow;
        return await _context.Properties.AsNoTracking()
            .Where(property => property.Slug == slug && property.Status == PropertyStatus.Approved
                && (property.ExpiresAtUtc == null || property.ExpiresAtUtc > now))
            .Select(property => (int?)property.Id)
            .SingleOrDefaultAsync();
    }

    private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    internal static void Normalize(PropertySearchQuery query)
    {
        query.Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim()[..Math.Min(120, query.Keyword.Trim().Length)];
        query.Page = Math.Max(1, query.Page);
        query.Sort = query.Sort is "price-asc" or "price-desc" or "area-desc" ? query.Sort : "newest";
        if (query.MinPrice is < 0) query.MinPrice = null;
        if (query.MaxPrice is < 0) query.MaxPrice = null;
        if (query.MinArea is < 0) query.MinArea = null;
        if (query.MaxArea is < 0) query.MaxArea = null;
        if (query.Bedrooms is < 0) query.Bedrooms = null;
        if (query.MinPrice > query.MaxPrice) (query.MinPrice, query.MaxPrice) = (query.MaxPrice, query.MinPrice);
        if (query.MinArea > query.MaxArea) (query.MinArea, query.MaxArea) = (query.MaxArea, query.MinArea);
    }

    private static bool IsDatabaseUnavailable(Exception exception) => exception is Microsoft.Data.SqlClient.SqlException
        || exception.InnerException is Microsoft.Data.SqlClient.SqlException;
}
