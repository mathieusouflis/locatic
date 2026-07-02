using Locatic.Data.Context;
using Locatic.Interfaces;
using Locatic.Models;
using Microsoft.EntityFrameworkCore;

namespace Locatic.Data;

public class BrandRepository : IBrandRepository
{
    private readonly AppDbContext _context;

    public BrandRepository(AppDbContext context)
    {
        _context = context;
    }

    public IEnumerable<Brand> GetAll()
        => _context.Brands.Include(b => b.Models).OrderBy(b => b.Name).ToList();

    public Brand? GetById(int id)
        => _context.Brands.Find(id);

    public void Add(Brand brand)
        => _context.Brands.Add(brand);

    public void SaveChanges()
        => _context.SaveChanges();
}
