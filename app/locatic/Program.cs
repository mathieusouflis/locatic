using Locatic.Data;
using Locatic.Data.Context;
using Locatic.Interfaces;
using Microsoft.EntityFrameworkCore;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// DB_PATH permet de pointer la base SQLite vers un volume monté (Docker / Kubernetes).
var dbPath = builder.Configuration["DB_PATH"];
var connectionString = !string.IsNullOrWhiteSpace(dbPath)
    ? $"Data Source={dbPath}"
    : builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=locatic.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

builder.Services.AddScoped<IBrandRepository, BrandRepository>();
builder.Services.AddScoped<ICarModelRepository, CarModelRepository>();
builder.Services.AddScoped<ICarRepository, CarRepository>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Derrière Nginx (conteneur), le TLS est terminé par le reverse proxy :
// la redirection HTTPS casserait les probes Kubernetes.
if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseHttpMetrics();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapMetrics();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Rend la classe Program visible pour les tests d'intégration (WebApplicationFactory).
public partial class Program { }
