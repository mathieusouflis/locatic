using Locatic.Models;

namespace Locatic.Interfaces;

public interface IBrandRepository
{
    IEnumerable<Brand> GetAll();
    Brand? GetById(int id);
    void Add(Brand brand);
    void SaveChanges();
}
