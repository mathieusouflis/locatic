using Locatic.Enums;

namespace Locatic.Models;

public class Car
{
    private string _licensePlate = string.Empty;
    private decimal _dailyRate;
    private int _seatsCount;
    private int _year;

    private Car() { }

    public Car(string licensePlate, int year, decimal dailyRate, int seatsCount, FuelType fuelType, int carModelId)
    {
        LicensePlate = licensePlate;
        Year = year;
        DailyRate = dailyRate;
        SeatsCount = seatsCount;
        FuelType = fuelType;
        CarModelId = carModelId > 0
            ? carModelId
            : throw new ArgumentException("CarModelId must be a valid identifier.", nameof(carModelId));
    }

    public int Id { get; set; }

    public string LicensePlate
    {
        get => _licensePlate;
        set => _licensePlate = !string.IsNullOrWhiteSpace(value)
            ? value.Trim().ToUpperInvariant()
            : throw new ArgumentException("License plate cannot be empty.", nameof(value));
    }

    public int Year
    {
        get => _year;
        set => _year = value is >= 1990 and <= 2030
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Year must be between 1990 and 2030.");
    }

    public decimal DailyRate
    {
        get => _dailyRate;
        set => _dailyRate = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Daily rate must be greater than 0.");
    }

    public int SeatsCount
    {
        get => _seatsCount;
        set => _seatsCount = value is >= 1 and <= 9
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Seats count must be between 1 and 9.");
    }

    public FuelType FuelType { get; set; }

    public int CarModelId { get; set; }
    public CarModel CarModel { get; set; } = null!;

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
