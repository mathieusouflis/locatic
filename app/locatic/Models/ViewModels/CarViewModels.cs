using System.ComponentModel.DataAnnotations;
using Locatic.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Locatic.Models.ViewModels;

public class CarListViewModel
{
    public int Id { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal DailyRate { get; set; }
    public FuelType FuelType { get; set; }
    public int SeatsCount { get; set; }
}

public class CarDetailViewModel : CarListViewModel { }

public class CarCreateViewModel
{
    [Required(ErrorMessage = "License plate is required.")]
    [StringLength(20)]
    [Display(Name = "License Plate")]
    public string LicensePlate { get; set; } = string.Empty;

    [Required(ErrorMessage = "Year is required.")]
    [Range(1990, 2030)]
    [Display(Name = "Year")]
    public int Year { get; set; } = DateTime.Now.Year;

    [Required(ErrorMessage = "Daily rate is required.")]
    [Range(0.01, 9999.99)]
    [Display(Name = "Daily Rate (€)")]
    public decimal DailyRate { get; set; }

    [Required]
    [Range(1, 9)]
    [Display(Name = "Seats")]
    public int SeatsCount { get; set; } = 5;

    [Required(ErrorMessage = "Fuel type is required.")]
    [Display(Name = "Fuel Type")]
    public FuelType FuelType { get; set; }

    [Required(ErrorMessage = "Model is required.")]
    [Display(Name = "Model")]
    public int CarModelId { get; set; }

    public IEnumerable<SelectListItem> CarModels { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class CarEditViewModel : CarCreateViewModel
{
    public int Id { get; set; }
}
