using Locatic.Enums;
using Locatic.Models;
using Microsoft.EntityFrameworkCore;

namespace Locatic.Data.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<CarModel> CarModels => Set<CarModel>();
    public DbSet<Car> Cars => Set<Car>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Car>()
            .Property(c => c.DailyRate)
            .HasColumnType("decimal(10,2)");

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Brand>().HasData(
            new Brand("Ferrari", "Italy") { Id = 1 },
            new Brand("Lamborghini", "Italy") { Id = 2 },
            new Brand("Porsche", "Germany") { Id = 3 },
            new Brand("McLaren", "United Kingdom") { Id = 4 },
            new Brand("Bugatti", "France") { Id = 5 },
            new Brand("Aston Martin", "United Kingdom") { Id = 6 },
            new Brand("Pagani", "Italy") { Id = 7 }
        );

        modelBuilder.Entity<CarModel>().HasData(
            new CarModel("488 GTB", 1) { Id = 1 },
            new CarModel("F8 Tributo", 1) { Id = 2 },
            new CarModel("Roma", 1) { Id = 3 },
            new CarModel("Huracán", 2) { Id = 4 },
            new CarModel("Aventador", 2) { Id = 5 },
            new CarModel("911 GT3", 3) { Id = 6 },
            new CarModel("Taycan Turbo S", 3) { Id = 7 },
            new CarModel("720S", 4) { Id = 8 },
            new CarModel("Artura", 4) { Id = 9 },
            new CarModel("Chiron", 5) { Id = 10 },
            new CarModel("DB11", 6) { Id = 11 },
            new CarModel("Vantage", 6) { Id = 12 },
            new CarModel("Huayra", 7) { Id = 13 }
        );

        modelBuilder.Entity<Car>().HasData(
            new Car("FE-001-AA", 2021, 800m, 2, FuelType.Gasoline, 1) { Id = 1 },
            new Car("FE-002-AA", 2022, 850m, 2, FuelType.Gasoline, 1) { Id = 2 },
            new Car("FE-003-BB", 2023, 950m, 2, FuelType.Gasoline, 2) { Id = 3 },
            new Car("FE-004-CC", 2023, 780m, 4, FuelType.Gasoline, 3) { Id = 4 },
            new Car("LA-001-AA", 2021, 980m, 2, FuelType.Gasoline, 4) { Id = 5 },
            new Car("LA-002-AA", 2023, 1050m, 2, FuelType.Gasoline, 4) { Id = 6 },
            new Car("LA-003-BB", 2022, 1250m, 2, FuelType.Gasoline, 5) { Id = 7 },
            new Car("LA-004-BB", 2023, 1350m, 2, FuelType.Gasoline, 5) { Id = 8 },
            new Car("PO-001-AA", 2022, 720m, 4, FuelType.Gasoline, 6) { Id = 9 },
            new Car("PO-002-AA", 2023, 760m, 4, FuelType.Gasoline, 6) { Id = 10 },
            new Car("PO-003-BB", 2023, 680m, 4, FuelType.Electric, 7) { Id = 11 },
            new Car("MC-001-AA", 2021, 1100m, 2, FuelType.Gasoline, 8) { Id = 12 },
            new Car("MC-002-AA", 2022, 1180m, 2, FuelType.Gasoline, 8) { Id = 13 },
            new Car("MC-003-BB", 2023, 920m, 2, FuelType.Hybrid, 9) { Id = 14 },
            new Car("BU-001-AA", 2022, 3200m, 2, FuelType.Gasoline, 10) { Id = 15 },
            new Car("BU-002-AA", 2023, 3600m, 2, FuelType.Gasoline, 10) { Id = 16 },
            new Car("AM-001-AA", 2022, 820m, 4, FuelType.Gasoline, 11) { Id = 17 },
            new Car("AM-002-BB", 2023, 870m, 2, FuelType.Gasoline, 12) { Id = 18 },
            new Car("PA-001-AA", 2022, 2600m, 2, FuelType.Gasoline, 13) { Id = 19 },
            new Car("PA-002-AA", 2023, 2900m, 2, FuelType.Gasoline, 13) { Id = 20 }
        );

        modelBuilder.Entity<Client>().HasData(
            new Client("Alice", "Martin", "alice.martin@email.fr", "0601020304") { Id = 1 },
            new Client("Bob", "Dupont", "bob.dupont@email.fr", "0605060708") { Id = 2 }
        );
    }
}
