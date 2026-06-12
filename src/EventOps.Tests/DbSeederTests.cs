using EventOps.API.Services;
using EventOps.Core.Models;
using Microsoft.Extensions.Configuration;

namespace EventOps.Tests;

/// <summary>
/// Testes do DbSeeder: criação do admin inicial e idempotência.
/// </summary>
public class DbSeederTests
{
    private static IConfiguration ConfigWith(string? email, string? password) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Email"]    = email,
                ["Admin:Password"] = password,
            })
            .Build();

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().Build();

    // ── 1. BD vazia + dev com config → cria admin ────────────────────────────

    [Fact]
    public async Task Seed_BdVazia_Dev_CriaAdmin()
    {
        using var db = TestHelpers.CreateDb(nameof(Seed_BdVazia_Dev_CriaAdmin));

        await DbSeeder.SeedAdminAsync(db, ConfigWith("admin@test.com", "Segredo123"), isDevelopment: true);

        var admin = db.Utilizadores.SingleOrDefault(u => u.Perfil == Perfil.Administrador);
        Assert.NotNull(admin);
        Assert.Equal("admin@test.com", admin.Email);
        Assert.True(BCrypt.Net.BCrypt.Verify("Segredo123", admin.PasswordHash));
    }

    // ── 2. Idempotência: correr seed duas vezes não duplica o admin ───────────

    [Fact]
    public async Task Seed_AdminJaExiste_NaoDuplica()
    {
        using var db = TestHelpers.CreateDb(nameof(Seed_AdminJaExiste_NaoDuplica));
        var cfg      = ConfigWith("admin@test.com", "Segredo123");

        await DbSeeder.SeedAdminAsync(db, cfg, isDevelopment: true);
        await DbSeeder.SeedAdminAsync(db, cfg, isDevelopment: true); // segunda chamada

        Assert.Equal(1, db.Utilizadores.Count(u => u.Perfil == Perfil.Administrador));
    }

    // ── 3. Dev sem config → usa defaults ─────────────────────────────────────

    [Fact]
    public async Task Seed_DevSemConfig_UsaDefaults()
    {
        using var db = TestHelpers.CreateDb(nameof(Seed_DevSemConfig_UsaDefaults));

        await DbSeeder.SeedAdminAsync(db, EmptyConfig(), isDevelopment: true);

        var admin = db.Utilizadores.SingleOrDefault(u => u.Perfil == Perfil.Administrador);
        Assert.NotNull(admin);
        Assert.Equal("admin@eventops.com", admin.Email);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password123", admin.PasswordHash));
    }

    // ── 4. Produção sem config → não cria nada ───────────────────────────────

    [Fact]
    public async Task Seed_ProducaoSemConfig_NaoCriaNada()
    {
        using var db = TestHelpers.CreateDb(nameof(Seed_ProducaoSemConfig_NaoCriaNada));

        await DbSeeder.SeedAdminAsync(db, EmptyConfig(), isDevelopment: false);

        Assert.Empty(db.Utilizadores);
    }

    // ── 5. Produção com config → cria admin ──────────────────────────────────

    [Fact]
    public async Task Seed_ProducaoComConfig_CriaAdmin()
    {
        using var db = TestHelpers.CreateDb(nameof(Seed_ProducaoComConfig_CriaAdmin));

        await DbSeeder.SeedAdminAsync(db, ConfigWith("prod@app.com", "ProdPass!99"), isDevelopment: false);

        var admin = db.Utilizadores.SingleOrDefault(u => u.Perfil == Perfil.Administrador);
        Assert.NotNull(admin);
        Assert.Equal("prod@app.com", admin.Email);
    }
}
