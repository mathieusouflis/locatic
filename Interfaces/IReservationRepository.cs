using Locatic.Models;

namespace Locatic.Interfaces;

public interface IReservationRepository
{
    IEnumerable<Reservation> GetAllWithDetails();
    Reservation? GetByIdWithDetails(int id);
    bool HasOverlap(int carId, DateTime startDate, DateTime endDate, int? excludeId = null);
    void Add(Reservation reservation);
    void SaveChanges();
}
