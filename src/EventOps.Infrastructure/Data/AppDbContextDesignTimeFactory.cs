using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EventOps.Infrastructure.Data;

// Usado exclusivamente pelo dotnet-ef em design-time (migrations add/remove).
// Usa Npgsql para que as migrations geradas tenham tipos PostgreSQL nativos
// (integer, boolean, numeric, timestamp with time zone), compatíveis com SQLite
// em desenvolvimento (o SQLite aceita nomes de tipo arbitrários via type affinity).
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Deve espelhar o switch activo em Program.cs para que o snapshot gerado
        // pelo dotnet-ef use os mesmos mapeamentos de tipo que o runtime usa.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=eventops_design;Username=postgres",
                npgsql => npgsql.MigrationsAssembly("EventOps.Infrastructure"))
            .Options;
        return new AppDbContext(opts);
    }
}
