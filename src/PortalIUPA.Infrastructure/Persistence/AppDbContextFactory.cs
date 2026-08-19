using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PortalIUPA.Infrastructure.Persistence;

/// <summary>Factory de design-time para `dotnet ef migrations` (no requiere la API en ejecución).</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=portal_iupa;Username=postgres;Password=postgres")
            .Options;

        return new AppDbContext(opciones);
    }
}