using Locatic.Data;
using Locatic.Data.Context;
using Locatic.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Locatic.Tests;

public class ReservationRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly ReservationRepository _repository;

    public ReservationRepositoryTests()
    {
        // SQLite en mémoire : la connexion doit rester ouverte pendant toute la durée du test.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new ReservationRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void HasOverlap_WhenDatesOverlap_ReturnsTrue()
    {
        _repository.Add(new Reservation(1, 1, new DateTime(2026, 7, 1), new DateTime(2026, 7, 10)));
        _repository.SaveChanges();

        var overlap = _repository.HasOverlap(1, new DateTime(2026, 7, 5), new DateTime(2026, 7, 15));

        Assert.True(overlap);
    }

    [Fact]
    public void HasOverlap_WhenDatesDoNotOverlap_ReturnsFalse()
    {
        _repository.Add(new Reservation(1, 1, new DateTime(2026, 7, 1), new DateTime(2026, 7, 10)));
        _repository.SaveChanges();

        var overlap = _repository.HasOverlap(1, new DateTime(2026, 7, 10), new DateTime(2026, 7, 20));

        Assert.False(overlap);
    }

    [Fact]
    public void HasOverlap_ForDifferentCar_ReturnsFalse()
    {
        _repository.Add(new Reservation(1, 1, new DateTime(2026, 7, 1), new DateTime(2026, 7, 10)));
        _repository.SaveChanges();

        var overlap = _repository.HasOverlap(2, new DateTime(2026, 7, 5), new DateTime(2026, 7, 15));

        Assert.False(overlap);
    }

    [Fact]
    public void HasOverlap_WithExcludedReservation_IgnoresIt()
    {
        var reservation = new Reservation(1, 1, new DateTime(2026, 7, 1), new DateTime(2026, 7, 10));
        _repository.Add(reservation);
        _repository.SaveChanges();

        var overlap = _repository.HasOverlap(1, new DateTime(2026, 7, 5), new DateTime(2026, 7, 15), excludeId: reservation.Id);

        Assert.False(overlap);
    }

    [Fact]
    public void GetAllWithDetails_ReturnsSeededDataOrderedByStartDateDescending()
    {
        _repository.Add(new Reservation(1, 1, new DateTime(2026, 7, 1), new DateTime(2026, 7, 5)));
        _repository.Add(new Reservation(2, 2, new DateTime(2026, 8, 1), new DateTime(2026, 8, 5)));
        _repository.SaveChanges();

        var reservations = _repository.GetAllWithDetails().ToList();

        Assert.Equal(2, reservations.Count);
        Assert.True(reservations[0].StartDate > reservations[1].StartDate);
        Assert.NotNull(reservations[0].Car.CarModel.Brand);
    }
}
