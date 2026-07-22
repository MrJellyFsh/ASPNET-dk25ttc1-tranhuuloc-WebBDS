using System.ComponentModel.DataAnnotations;

namespace NhaTot.Models;

public class Province
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Code { get; set; } = string.Empty;
    public ICollection<District> Districts { get; set; } = new List<District>();
    public ICollection<Property> Properties { get; set; } = new List<Property>();
}

public class District
{
    public int Id { get; set; }
    public int ProvinceId { get; set; }
    public Province Province { get; set; } = null!;
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Code { get; set; } = string.Empty;
    public ICollection<Property> Properties { get; set; } = new List<Property>();
}
