using Locatic.Data.Context;
using Locatic.Interfaces;
using Locatic.Models;
using Microsoft.EntityFrameworkCore;

namespace Locatic.Data;

public class ReservationRepository : IReservationRepository
{
    private readonly AppDbContext _context;

    public ReservationRepository(AppDbContext context)
    {
        _context = context;
    }

    public IEnumerable<Reservation> GetAllWithDetails()
        => _context.Reservations
            .Include(r => r.Client)
            .Include(r => r.Car).ThenInclude(c => c.CarModel).ThenInclude(m => m.Brand)
            .OrderByDescending(r => r.StartDate)
            .ToList();

    public Reservation? GetByIdWithDetails(int id)
        => _context.Reservations
            .Include(r => r.Client)
            .Include(r => r.Car).ThenInclude(c => c.CarModel).ThenInclude(m => m.Brand)
            .FirstOrDefault(r => r.Id == id);

    public bool HasOverlap(int carId, DateTime startDate, DateTime endDate, int? excludeId = null)
        => _context.Reservations.Any(r =>
            r.CarId == carId
            && r.StartDate < endDate
            && r.EndDate > startDate
            && (excludeId == null || r.Id != excludeId));

    public void Add(Reservation reservation)
        => _context.Reservations.Add(reservation);

    public void SaveChanges()
        => _context.SaveChanges();
}
