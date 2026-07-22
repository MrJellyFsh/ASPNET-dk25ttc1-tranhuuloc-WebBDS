using System.ComponentModel.DataAnnotations;

namespace NhaTot.Models;

public class AgentProfile
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    [Required, StringLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(160)]
    public string? CompanyName { get; set; }

    [StringLength(120)]
    public string? OperatingArea { get; set; }

    [Range(0, 80)]
    public int? YearsOfExperience { get; set; }

    [StringLength(2000)]
    public string? Biography { get; set; }

    public DateTime ApprovedAtUtc { get; set; }
}
