namespace Locatic.Models;

public class Brand
{
    private string _name = string.Empty;

    private Brand() { }

    public Brand(string name, string? countryOfOrigin = null)
    {
        Name = name;
        CountryOfOrigin = countryOfOrigin;
    }

    public int Id { get; set; }

    public string Name
    {
        get => _name;
        set => _name = !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException("Brand name cannot be empty.", nameof(value));
    }

    public string? CountryOfOrigin { get; set; }

    public ICollection<CarModel> Models { get; set; } = new List<CarModel>();
}
