using Locatic.Models;

namespace Locatic.Interfaces;

public interface ICarModelRepository
{
    IEnumerable<CarModel> GetAllWithBrand();
    CarModel? GetById(int id);
    void Add(CarModel carModel);
    void SaveChanges();
}
