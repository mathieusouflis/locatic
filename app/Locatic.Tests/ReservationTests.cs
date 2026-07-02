using Locatic.Models;

namespace Locatic.Tests;

public class ReservationTests
{
    private static readonly DateTime Start = new(2026, 7, 1);
    private static readonly DateTime End = new(2026, 7, 5);

    [Fact]
    public void Constructor_WithValidData_CreatesReservation()
    {
        var reservation = new Reservation(clientId: 1, carId: 2, Start, End);

        Assert.Equal(1, reservation.ClientId);
        Assert.Equal(2, reservation.CarId);
        Assert.Equal(Start, reservation.StartDate);
        Assert.Equal(End, reservation.EndDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidClientId_Throws(int clientId)
    {
        Assert.Throws<ArgumentException>(() => new Reservation(clientId, 1, Start, End));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_WithInvalidCarId_Throws(int carId)
    {
        Assert.Throws<ArgumentException>(() => new Reservation(1, carId, Start, End));
    }

    [Fact]
    public void Constructor_WithEndDateBeforeStartDate_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Reservation(1, 1, End, Start));
    }

    [Fact]
    public void Constructor_WithEndDateEqualToStartDate_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Reservation(1, 1, Start, Start));
    }
}
