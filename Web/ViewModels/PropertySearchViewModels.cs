using NhaTot.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace NhaTot.ViewModels;

public class PropertySearchQuery
{
    public string? Keyword { get; set; }
    public ListingPurpose? Purpose { get; set; }
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    public int? PropertyTypeId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinArea { get; set; }
    public decimal? MaxArea { get; set; }
    public int? Bedrooms { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
}

public sealed class PropertyCardViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string PropertyTypeName { get; init; } = string.Empty;
    public string ProvinceName { get; init; } = string.Empty;
    public string DistrictName { get; init; } = string.Empty;
    public string? ApproximateLocation { get; init; }
    public ListingPurpose Purpose { get; init; }
    public decimal Price { get; init; }
    public decimal AreaSquareMeters { get; init; }
    public int? Bedrooms { get; init; }
    public int? Bathrooms { get; init; }
    public string? CoverImagePath { get; init; }
    public DateTime PublishedAtUtc { get; init; }
}

public sealed class PropertySearchViewModel
{
    public PropertySearchQuery Query { get; init; } = new();
    public IReadOnlyList<PropertyCardViewModel> Properties { get; init; } = [];
    public IReadOnlyList<LookupOption> Provinces { get; init; } = [];
    public IReadOnlyList<LookupOption> Districts { get; init; } = [];
    public IReadOnlyList<LookupOption> PropertyTypes { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool IsDatabaseAvailable { get; init; } = true;
}

public sealed record LookupOption(int Id, string Name);

public sealed class HomeViewModel
{
    public PropertySearchQuery Search { get; init; } = new();
    public IReadOnlyList<LookupOption> Provinces { get; init; } = [];
    public IReadOnlyList<LookupOption> PropertyTypes { get; init; } = [];
    public IReadOnlyList<PropertyCardViewModel> FeaturedProperties { get; init; } = [];
    public bool IsDatabaseAvailable { get; init; } = true;
}

public sealed class PropertyDetailViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string PropertyTypeName { get; init; } = string.Empty;
    public string ProvinceName { get; init; } = string.Empty;
    public string DistrictName { get; init; } = string.Empty;
    public string? ApproximateLocation { get; init; }
    public ListingPurpose Purpose { get; init; }
    public decimal Price { get; init; }
    public decimal AreaSquareMeters { get; init; }
    public int? Bedrooms { get; init; }
    public int? Bathrooms { get; init; }
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<PropertyImageViewModel> Images { get; init; } = [];
    public IReadOnlyList<string> Amenities { get; init; } = [];
    public bool IsFavorite { get; init; }
    public ContactRequestInput ContactRequest { get; init; } = new();
}

public sealed record PropertyImageViewModel(string FilePath, string? AltText);

public sealed class ContactRequestInput
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(120, ErrorMessage = "Họ và tên không được vượt quá 120 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [StringLength(30, ErrorMessage = "Số điện thoại không được vượt quá 30 ký tự.")]
    [Phone(ErrorMessage = "Số điện thoại chưa đúng định dạng.")]
    [Display(Name = "Số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email chưa đúng định dạng.")]
    [StringLength(256, ErrorMessage = "Email không được vượt quá 256 ký tự.")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nội dung yêu cầu.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Nội dung yêu cầu cần từ 10 đến 2.000 ký tự.")]
    [Display(Name = "Nội dung yêu cầu")]
    public string Message { get; set; } = string.Empty;
}

public sealed class SavedPropertiesViewModel
{
    public IReadOnlyList<PropertyCardViewModel> Properties { get; init; } = [];
    public bool IsDatabaseAvailable { get; init; } = true;
}
