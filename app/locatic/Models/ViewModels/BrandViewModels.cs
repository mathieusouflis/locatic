using System.ComponentModel.DataAnnotations;

namespace Locatic.Models.ViewModels;

public class BrandListViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CountryOfOrigin { get; set; }
    public int ModelsCount { get; set; }
}

public class BrandCreateViewModel
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? CountryOfOrigin { get; set; }
}
