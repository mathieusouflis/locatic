namespace Locatic.Models;

public class CarModel
{
    private string _name = string.Empty;

    private CarModel() { }

    public CarModel(string name, int brandId)
    {
        Name = name;
        BrandId = brandId > 0
            ? brandId
            : throw new ArgumentException("BrandId must be a valid identifier.", nameof(brandId));
    }

    public int Id { get; set; }

    public string Name
    {
        get => _name;
        set => _name = !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException("Model name cannot be empty.", nameof(value));
    }

    public int BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    public ICollection<Car> Cars { get; set; } = new List<Car>();
}
