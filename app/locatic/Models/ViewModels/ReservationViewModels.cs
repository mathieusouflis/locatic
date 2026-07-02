using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Locatic.Models.ViewModels;

public class ReservationListViewModel
{
    public int Id { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string CarLabel { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DurationDays => (EndDate - StartDate).Days;
}

public class ReservationCreateViewModel
{
    [Required(ErrorMessage = "Client is required.")]
    [Display(Name = "Client")]
    public int ClientId { get; set; }

    [Required(ErrorMessage = "Car is required.")]
    [Display(Name = "Car")]
    public int CarId { get; set; }

    [Required(ErrorMessage = "Start date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "End date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(1);

    public IEnumerable<SelectListItem> Clients { get; set; } = Enumerable.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Cars { get; set; } = Enumerable.Empty<SelectListItem>();
}
