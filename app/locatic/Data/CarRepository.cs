using Locatic.Data.Context;
using Locatic.Interfaces;
using Locatic.Models;
using Microsoft.EntityFrameworkCore;

namespace Locatic.Data;

public class CarRepository : ICarRepository
{
    private readonly AppDbContext _context;

    public CarRepository(AppDbContext context)
    {
        _context = context;
    }

    public IEnumerable<Car> GetAllWithDetails()
        => _context.Cars
            .Include(c => c.CarModel).ThenInclude(m => m.Brand)
            .OrderBy(c => c.CarModel.Brand.Name).ThenBy(c => c.CarModel.Name)
            .ToList();

    public Car? GetByIdWithDetails(int id)
        => _context.Cars
            .Include(c => c.CarModel).ThenInclude(m => m.Brand)
            .FirstOrDefault(c => c.Id == id);

    public void Add(Car car)
        => _context.Cars.Add(car);

    public void Update(Car car)
        => _context.Cars.Update(car);

    public void Delete(Car car)
        => _context.Cars.Remove(car);

    public void SaveChanges()
        => _context.SaveChanges();
}
