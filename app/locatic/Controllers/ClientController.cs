using Locatic.Interfaces;
using Locatic.Models;
using Locatic.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Locatic.Controllers;

public class ClientController : Controller
{
    private readonly IClientRepository _clientRepository;

    public ClientController(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    public IActionResult Index()
    {
        var vms = _clientRepository.GetAll().Select(c => new ClientListViewModel
        {
            Id = c.Id,
            FullName = $"{c.FirstName} {c.LastName}",
            Email = c.Email,
            Phone = c.Phone
        });
        return View(vms);
    }

    public IActionResult Create() => View(new ClientCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ClientCreateViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var client = new Client(vm.FirstName, vm.LastName, vm.Email, vm.Phone);
        _clientRepository.Add(client);
        _clientRepository.SaveChanges();
        return RedirectToAction(nameof(Index));
    }
}
