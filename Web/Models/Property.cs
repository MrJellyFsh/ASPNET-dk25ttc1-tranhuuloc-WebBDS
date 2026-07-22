using NhaTot.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace NhaTot.Models;

public class Property
{
    public int Id { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(220)] public string Slug { get; set; } = string.Empty;
    public ListingPurpose Purpose { get; set; }
    public PropertyStatus Status { get; set; } = PropertyStatus.Draft;
    public int PropertyTypeId { get; set; }
    public PropertyType PropertyType { get; set; } = null!;
    [Range(0.01, 999999999999999999.99)] public decimal Price { get; set; }
    [Range(0.01, 9999999.99)] public decimal AreaSquareMeters { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public int ProvinceId { get; set; }
    public Province Province { get; set; } = null!;
    public int DistrictId { get; set; }
    public District District { get; set; } = null!;
    [Required, StringLength(300)] public string Address { get; set; } = string.Empty;
    [Required, StringLength(8000)] public string Description { get; set; } = string.Empty;
    [StringLength(300)] public string? ApproximateLocation { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewedByUserId { get; set; }
    [StringLength(1000)] public string? RejectionReason { get; set; }
    public int ViewCount { get; set; }
    [Required] public string AgentUserId { get; set; } = string.Empty;
    public ApplicationUser AgentUser { get; set; } = null!;
    public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    public ICollection<PropertyAmenity> PropertyAmenities { get; set; } = new List<PropertyAmenity>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<ContactRequest> ContactRequests { get; set; } = new List<ContactRequest>();
    public ICollection<ModerationRecord> ModerationRecords { get; set; } = new List<ModerationRecord>();
}
