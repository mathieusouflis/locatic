namespace Locatic.Models;

public class Client
{
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _email = string.Empty;

    private Client() { }

    public Client(string firstName, string lastName, string email, string? phone = null)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Phone = phone;
    }

    public int Id { get; set; }

    public string FirstName
    {
        get => _firstName;
        set => _firstName = !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException("First name cannot be empty.", nameof(value));
    }

    public string LastName
    {
        get => _lastName;
        set => _lastName = !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException("Last name cannot be empty.", nameof(value));
    }

    public string Email
    {
        get => _email;
        set => _email = !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException("Email cannot be empty.", nameof(value));
    }

    public string? Phone { get; set; }

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
