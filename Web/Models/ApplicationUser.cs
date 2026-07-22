using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace NhaTot.Models;

public class ApplicationUser : IdentityUser
{
    [StringLength(120)]
    public string? FullName { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public AgentProfile? AgentProfile { get; set; }

    public ICollection<AgentApplication> AgentApplications { get; set; } = new List<AgentApplication>();
    public ICollection<Property> Properties { get; set; } = new List<Property>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<ContactRequest> ContactRequests { get; set; } = new List<ContactRequest>();
}
