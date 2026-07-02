using Microsoft.AspNetCore.Mvc.Testing;

namespace Locatic.Tests;

public class HealthEndpointTests : IClassFixture<LocaticWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(LocaticWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HomePage_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CarsPage_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/Car");

        response.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// Démarre l'application complète (migrations comprises) sur une base SQLite temporaire,
/// via la variable DB_PATH — le même mécanisme que le volume monté en Kubernetes.
/// </summary>
public class LocaticWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"locatic-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("DB_PATH", _dbPath);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
