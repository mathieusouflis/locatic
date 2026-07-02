using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Locatic.Models.ViewModels;

public class CarModelListViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
}

public class CarModelCreateViewModel
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Brand is required.")]
    public int BrandId { get; set; }

    public IEnumerable<SelectListItem> Brands { get; set; } = Enumerable.Empty<SelectListItem>();
}
