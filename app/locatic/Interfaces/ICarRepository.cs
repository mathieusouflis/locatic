using Locatic.Models;

namespace Locatic.Interfaces;

public interface ICarRepository
{
    IEnumerable<Car> GetAllWithDetails();
    Car? GetByIdWithDetails(int id);
    void Add(Car car);
    void Update(Car car);
    void Delete(Car car);
    void SaveChanges();
}
