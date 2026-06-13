using Locatic.Interfaces;
using Locatic.Models;
using Locatic.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Locatic.Controllers;

public class BrandController : Controller
{
    private readonly IBrandRepository _brandRepository;

    public BrandController(IBrandRepository brandRepository)
    {
        _brandRepository = brandRepository;
    }

    public IActionResult Index()
    {
        var vms = _brandRepository.GetAll().Select(b => new BrandListViewModel
        {
            Id = b.Id,
            Name = b.Name,
            CountryOfOrigin = b.CountryOfOrigin,
            ModelsCount = b.Models.Count
        });
        return View(vms);
    }

    public IActionResult Create() => View(new BrandCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(BrandCreateViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var brand = new Brand(vm.Name, vm.CountryOfOrigin);
        _brandRepository.Add(brand);
        _brandRepository.SaveChanges();
        return RedirectToAction(nameof(Index));
    }
}
