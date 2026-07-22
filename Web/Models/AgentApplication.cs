using NhaTot.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace NhaTot.Models;

public class AgentApplication
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [StringLength(160)]
    public string? CompanyName { get; set; }

    [Required, StringLength(120)]
    public string OperatingArea { get; set; } = string.Empty;

    [Range(0, 80)]
    public int? YearsOfExperience { get; set; }

    [StringLength(2000)]
    public string? Introduction { get; set; }

    [StringLength(260)]
    public string? VerificationDocumentPath { get; set; }

    public AgentApplicationStatus Status { get; set; } = AgentApplicationStatus.PendingReview;

    [StringLength(1000)]
    public string? RejectionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewedByUserId { get; set; }
}
