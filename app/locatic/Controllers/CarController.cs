using Locatic.Interfaces;
using Locatic.Models;
using Locatic.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Locatic.Controllers;

public class CarController : Controller
{
    private readonly ICarRepository _carRepository;
    private readonly ICarModelRepository _carModelRepository;

    public CarController(ICarRepository carRepository, ICarModelRepository carModelRepository)
    {
        _carRepository = carRepository;
        _carModelRepository = carModelRepository;
    }

    public IActionResult Index()
    {
        var vms = _carRepository.GetAllWithDetails().Select(c => new CarListViewModel
        {
            Id = c.Id,
            LicensePlate = c.LicensePlate,
            BrandName = c.CarModel.Brand.Name,
            ModelName = c.CarModel.Name,
            Year = c.Year,
            DailyRate = c.DailyRate,
            FuelType = c.FuelType,
            SeatsCount = c.SeatsCount
        });
        return View(vms);
    }

    public IActionResult Detail(int id)
    {
        var car = _carRepository.GetByIdWithDetails(id);
        if (car == null) return NotFound();

        return View(new CarDetailViewModel
        {
            Id = car.Id,
            LicensePlate = car.LicensePlate,
            BrandName = car.CarModel.Brand.Name,
            ModelName = car.CarModel.Name,
            Year = car.Year,
            DailyRate = car.DailyRate,
            FuelType = car.FuelType,
            SeatsCount = car.SeatsCount
        });
    }

    public IActionResult Create()
        => View(new CarCreateViewModel { CarModels = GetCarModelSelectList() });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CarCreateViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.CarModels = GetCarModelSelectList();
            return View(vm);
        }

        var car = new Car(vm.LicensePlate, vm.Year, vm.DailyRate, vm.SeatsCount, vm.FuelType, vm.CarModelId);
        _carRepository.Add(car);
        _carRepository.SaveChanges();
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var car = _carRepository.GetByIdWithDetails(id);
        if (car == null) return NotFound();

        return View(new CarEditViewModel
        {
            Id = car.Id,
            LicensePlate = car.LicensePlate,
            Year = car.Year,
            DailyRate = car.DailyRate,
            SeatsCount = car.SeatsCount,
            FuelType = car.FuelType,
            CarModelId = car.CarModelId,
            CarModels = GetCarModelSelectList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(CarEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.CarModels = GetCarModelSelectList();
            return View(vm);
        }

        var car = _carRepository.GetByIdWithDetails(vm.Id);
        if (car == null) return NotFound();

        car.LicensePlate = vm.LicensePlate;
        car.Year = vm.Year;
        car.DailyRate = vm.DailyRate;
        car.SeatsCount = vm.SeatsCount;
        car.FuelType = vm.FuelType;
        car.CarModelId = vm.CarModelId;

        _carRepository.Update(car);
        _carRepository.SaveChanges();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var car = _carRepository.GetByIdWithDetails(id);
        if (car != null)
        {
            _carRepository.Delete(car);
            _carRepository.SaveChanges();
        }
        return RedirectToAction(nameof(Index));
    }

    private IEnumerable<SelectListItem> GetCarModelSelectList()
        => _carModelRepository.GetAllWithBrand()
            .Select(m => new SelectListItem($"{m.Brand.Name} – {m.Name}", m.Id.ToString()));
}
