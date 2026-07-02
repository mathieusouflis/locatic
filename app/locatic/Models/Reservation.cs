namespace Locatic.Models;

public class Reservation
{
    private DateTime _startDate;
    private DateTime _endDate;

    private Reservation() { }

    public Reservation(int clientId, int carId, DateTime startDate, DateTime endDate)
    {
        ClientId = clientId > 0
            ? clientId
            : throw new ArgumentException("ClientId must be a valid identifier.", nameof(clientId));
        CarId = carId > 0
            ? carId
            : throw new ArgumentException("CarId must be a valid identifier.", nameof(carId));
        StartDate = startDate;
        EndDate = endDate;
    }

    public int Id { get; set; }

    public DateTime StartDate
    {
        get => _startDate;
        set => _startDate = value;
    }

    public DateTime EndDate
    {
        get => _endDate;
        set => _endDate = _startDate == default || value > _startDate
            ? value
            : throw new ArgumentException("End date must be after start date.", nameof(value));
    }

    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public int CarId { get; set; }
    public Car Car { get; set; } = null!;
}
