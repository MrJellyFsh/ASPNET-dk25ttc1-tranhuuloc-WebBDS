using NhaTot.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace NhaTot.ViewModels;

public sealed class AdminDashboardViewModel
{
    public IReadOnlyList<AdminAgentApplicationSummaryViewModel> PendingAgentApplications { get; init; } = [];
    public IReadOnlyList<AdminPropertyModerationSummaryViewModel> PendingProperties { get; init; } = [];
}

public sealed class AdminAgentApplicationSummaryViewModel
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string OperatingArea { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class AdminAgentApplicationDetailsViewModel
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? CompanyName { get; init; }
    public string OperatingArea { get; init; } = string.Empty;
    public int? YearsOfExperience { get; init; }
    public string? Introduction { get; init; }
    public string? VerificationDocumentPath { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class AdminPropertyModerationSummaryViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string PropertyTypeName { get; init; } = string.Empty;
    public string ApproximateLocation { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class AdminPropertyModerationDetailsViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public ListingPurpose Purpose { get; init; }
    public string PropertyTypeName { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal AreaSquareMeters { get; init; }
    public int? Bedrooms { get; init; }
    public int? Bathrooms { get; init; }
    public string ProvinceName { get; init; } = string.Empty;
    public string DistrictName { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string? ApproximateLocation { get; init; }
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> ImageUrls { get; init; } = [];
    public IReadOnlyList<string> Amenities { get; init; } = [];
}

public sealed class RejectionInput
{
    [Required(ErrorMessage = "Vui lòng nhập lý do từ chối để môi giới có thể điều chỉnh.")]
    [StringLength(1000, ErrorMessage = "Lý do từ chối không được vượt quá 1.000 ký tự.")]
    [Display(Name = "Lý do từ chối")]
    public string Reason { get; set; } = string.Empty;
}
