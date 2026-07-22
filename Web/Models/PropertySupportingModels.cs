using NhaTot.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace NhaTot.Models;

public class PropertyImage
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    [Required, StringLength(260)] public string FilePath { get; set; } = string.Empty;
    [StringLength(180)] public string? AltText { get; set; }
    public bool IsCover { get; set; }
    public int DisplayOrder { get; set; }
}

public class Amenity
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Slug { get; set; } = string.Empty;
    public ICollection<PropertyAmenity> PropertyAmenities { get; set; } = new List<PropertyAmenity>();
}

public class PropertyAmenity
{
    public int PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public int AmenityId { get; set; }
    public Amenity Amenity { get; set; } = null!;
}

public class Favorite
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public int PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ContactRequest
{
    public int Id { get; set; }
    [Required] public string RequesterUserId { get; set; } = string.Empty;
    public ApplicationUser RequesterUser { get; set; } = null!;
    public int PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    [Required, StringLength(120)] public string FullName { get; set; } = string.Empty;
    [Required, StringLength(30)] public string PhoneNumber { get; set; } = string.Empty;
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    [Required, StringLength(2000)] public string Message { get; set; } = string.Empty;
    public ContactRequestStatus Status { get; set; } = ContactRequestStatus.New;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ModerationRecord
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    [Required] public string ModeratorUserId { get; set; } = string.Empty;
    public PropertyStatus PreviousStatus { get; set; }
    public PropertyStatus NewStatus { get; set; }
    [StringLength(1000)] public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
