using Locatic.Data.Context;
using Locatic.Interfaces;
using Locatic.Models;
using Microsoft.EntityFrameworkCore;

namespace Locatic.Data;

public class CarModelRepository : ICarModelRepository
{
    private readonly AppDbContext _context;

    public CarModelRepository(AppDbContext context)
    {
        _context = context;
    }

    public IEnumerable<CarModel> GetAllWithBrand()
        => _context.CarModels
            .Include(m => m.Brand)
            .OrderBy(m => m.Brand.Name).ThenBy(m => m.Name)
            .ToList();

    public CarModel? GetById(int id)
        => _context.CarModels.Include(m => m.Brand).FirstOrDefault(m => m.Id == id);

    public void Add(CarModel carModel)
        => _context.CarModels.Add(carModel);

    public void SaveChanges()
        => _context.SaveChanges();
}
