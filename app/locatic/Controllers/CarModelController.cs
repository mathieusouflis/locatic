using Locatic.Interfaces;
using Locatic.Models;
using Locatic.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Locatic.Controllers;

public class CarModelController : Controller
{
    private readonly ICarModelRepository _carModelRepository;
    private readonly IBrandRepository _brandRepository;

    public CarModelController(ICarModelRepository carModelRepository, IBrandRepository brandRepository)
    {
        _carModelRepository = carModelRepository;
        _brandRepository = brandRepository;
    }

    public IActionResult Index()
    {
        var vms = _carModelRepository.GetAllWithBrand().Select(m => new CarModelListViewModel
        {
            Id = m.Id,
            Name = m.Name,
            BrandName = m.Brand.Name
        });
        return View(vms);
    }

    public IActionResult Create()
    {
        var vm = new CarModelCreateViewModel
        {
            Brands = _brandRepository.GetAll().Select(b => new SelectListItem(b.Name, b.Id.ToString()))
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CarModelCreateViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Brands = _brandRepository.GetAll().Select(b => new SelectListItem(b.Name, b.Id.ToString()));
            return View(vm);
        }

        var carModel = new CarModel(vm.Name, vm.BrandId);
        _carModelRepository.Add(carModel);
        _carModelRepository.SaveChanges();
        return RedirectToAction(nameof(Index));
    }
}
