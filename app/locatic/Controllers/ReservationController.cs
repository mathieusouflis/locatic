using Locatic.Interfaces;
using Locatic.Models;
using Locatic.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Locatic.Controllers;

public class ReservationController : Controller
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IClientRepository _clientRepository;
    private readonly ICarRepository _carRepository;

    public ReservationController(
        IReservationRepository reservationRepository,
        IClientRepository clientRepository,
        ICarRepository carRepository)
    {
        _reservationRepository = reservationRepository;
        _clientRepository = clientRepository;
        _carRepository = carRepository;
    }

    public IActionResult Index()
    {
        var vms = _reservationRepository.GetAllWithDetails().Select(r => new ReservationListViewModel
        {
            Id = r.Id,
            ClientFullName = $"{r.Client.FirstName} {r.Client.LastName}",
            CarLabel = $"{r.Car.CarModel.Brand.Name} {r.Car.CarModel.Name} – {r.Car.LicensePlate}",
            StartDate = r.StartDate,
            EndDate = r.EndDate
        });
        return View(vms);
    }

    public IActionResult Create() => View(PopulateDropdowns(new ReservationCreateViewModel()));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ReservationCreateViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(PopulateDropdowns(vm));

        if (_reservationRepository.HasOverlap(vm.CarId, vm.StartDate, vm.EndDate))
        {
            ModelState.AddModelError(string.Empty, "This car is already booked for the requested period.");
            return View(PopulateDropdowns(vm));
        }

        var reservation = new Reservation(vm.ClientId, vm.CarId, vm.StartDate, vm.EndDate);
        _reservationRepository.Add(reservation);
        _reservationRepository.SaveChanges();
        return RedirectToAction(nameof(Index));
    }

    private ReservationCreateViewModel PopulateDropdowns(ReservationCreateViewModel vm)
    {
        vm.Clients = _clientRepository.GetAll()
            .Select(c => new SelectListItem($"{c.FirstName} {c.LastName}", c.Id.ToString()));
        vm.Cars = _carRepository.GetAllWithDetails()
            .Select(c => new SelectListItem($"{c.CarModel.Brand.Name} {c.CarModel.Name} – {c.LicensePlate}", c.Id.ToString()));
        return vm;
    }
}
