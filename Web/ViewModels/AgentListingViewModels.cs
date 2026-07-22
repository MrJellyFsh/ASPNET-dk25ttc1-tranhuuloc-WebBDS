using NhaTot.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace NhaTot.ViewModels;

public sealed class AgentDashboardViewModel
{
    public IReadOnlyList<AgentListingSummaryViewModel> Properties { get; init; } = [];
}

public sealed class AgentListingSummaryViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public PropertyStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public string PropertyTypeName { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

public sealed class AgentPropertyInput
{
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề tin đăng.")]
    [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự.")]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn nhu cầu.")]
    [Display(Name = "Nhu cầu")]
    public ListingPurpose? Purpose { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại bất động sản hợp lệ.")]
    [Display(Name = "Loại bất động sản")]
    public int PropertyTypeId { get; set; }

    [Range(typeof(decimal), "0.01", "999999999999999999.99", ErrorMessage = "Giá phải lớn hơn 0.")]
    [Display(Name = "Giá (VNĐ)")]
    public decimal Price { get; set; }

    [Range(typeof(decimal), "0.01", "9999999.99", ErrorMessage = "Diện tích phải lớn hơn 0.")]
    [Display(Name = "Diện tích (m²)")]
    public decimal AreaSquareMeters { get; set; }

    [Range(0, 100, ErrorMessage = "Số phòng ngủ phải từ 0 đến 100.")]
    [Display(Name = "Phòng ngủ")]
    public int? Bedrooms { get; set; }

    [Range(0, 100, ErrorMessage = "Số phòng tắm phải từ 0 đến 100.")]
    [Display(Name = "Phòng tắm")]
    public int? Bathrooms { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tỉnh/thành hợp lệ.")]
    [Display(Name = "Tỉnh/thành")]
    public int ProvinceId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn quận/huyện hợp lệ.")]
    [Display(Name = "Quận/huyện")]
    public int DistrictId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ.")]
    [StringLength(300, ErrorMessage = "Địa chỉ không được vượt quá 300 ký tự.")]
    [Display(Name = "Địa chỉ đầy đủ")]
    public string Address { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "Khu vực hiển thị không được vượt quá 300 ký tự.")]
    [Display(Name = "Khu vực hiển thị công khai")]
    public string? ApproximateLocation { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mô tả tin đăng.")]
    [StringLength(8000, MinimumLength = 30, ErrorMessage = "Mô tả cần từ 30 đến 8.000 ký tự.")]
    [Display(Name = "Mô tả")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Tiện ích")]
    public List<int> AmenityIds { get; set; } = [];
}

public sealed class AgentPropertyEditViewModel
{
    public int Id { get; init; }
    public PropertyStatus Status { get; init; }
    public AgentPropertyInput Input { get; init; } = new();
    public IReadOnlyList<LookupOption> Provinces { get; init; } = [];
    public IReadOnlyList<LookupOption> Districts { get; init; } = [];
    public IReadOnlyList<LookupOption> PropertyTypes { get; init; } = [];
    public IReadOnlyList<LookupOption> Amenities { get; init; } = [];
    public IReadOnlyList<AgentPropertyImageViewModel> Images { get; init; } = [];
}

public sealed record AgentPropertyImageViewModel(int Id, string FilePath, string? AltText, bool IsCover);

public sealed class AgentPropertyImageInput
{
    [Required(ErrorMessage = "Vui lòng nhập liên kết ảnh HTTPS.")]
    [StringLength(260, ErrorMessage = "Liên kết ảnh không được vượt quá 260 ký tự.")]
    [Display(Name = "Liên kết ảnh HTTPS")]
    public string FilePath { get; set; } = string.Empty;

    [StringLength(180, ErrorMessage = "Mô tả ảnh không được vượt quá 180 ký tự.")]
    [Display(Name = "Mô tả ảnh")]
    public string? AltText { get; set; }

    [Display(Name = "Đặt làm ảnh bìa")]
    public bool IsCover { get; set; }
}

public sealed class AgentPropertyPreviewViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string PropertyTypeName { get; init; } = string.Empty;
    public string ProvinceName { get; init; } = string.Empty;
    public string DistrictName { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string? ApproximateLocation { get; init; }
    public ListingPurpose Purpose { get; init; }
    public PropertyStatus Status { get; init; }
    public decimal Price { get; init; }
    public decimal AreaSquareMeters { get; init; }
    public int? Bedrooms { get; init; }
    public int? Bathrooms { get; init; }
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<AgentPropertyImageViewModel> Images { get; init; } = [];
    public IReadOnlyList<string> Amenities { get; init; } = [];
}
