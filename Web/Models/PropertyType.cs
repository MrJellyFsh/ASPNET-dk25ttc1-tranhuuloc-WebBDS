using System.ComponentModel.DataAnnotations;

namespace NhaTot.Models;

public class PropertyType
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Property> Properties { get; set; } = new List<Property>();
}
