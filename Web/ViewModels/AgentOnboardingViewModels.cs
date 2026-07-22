using NhaTot.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace NhaTot.ViewModels;

public sealed class AgentApplicationInput
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

    [StringLength(160, ErrorMessage = "Tên công ty không được vượt quá 160 ký tự.")]
    [Display(Name = "Công ty / đơn vị công tác")]
    public string? CompanyName { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập khu vực hoạt động.")]
    [StringLength(120, ErrorMessage = "Khu vực hoạt động không được vượt quá 120 ký tự.")]
    [Display(Name = "Khu vực hoạt động")]
    public string OperatingArea { get; set; } = string.Empty;

    [Range(0, 80, ErrorMessage = "Số năm kinh nghiệm phải từ 0 đến 80.")]
    [Display(Name = "Số năm kinh nghiệm")]
    public int? YearsOfExperience { get; set; }

    [StringLength(2000, ErrorMessage = "Phần giới thiệu không được vượt quá 2.000 ký tự.")]
    [Display(Name = "Giới thiệu ngắn")]
    public string? Introduction { get; set; }
}

public sealed class AgentApplicationStatusViewModel
{
    public AgentApplicationStatus? Status { get; init; }
    public DateTime? SubmittedAtUtc { get; init; }
    public bool HasAgentProfile { get; init; }
    public string? DisplayName { get; init; }
    public string? CompanyName { get; init; }
    public string? OperatingArea { get; init; }
}
