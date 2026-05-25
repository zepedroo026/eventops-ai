using EventOps.API.Services;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EventOps.Tests;

/// <summary>
/// Utilitários partilhados por todos os testes.
/// </summary>
internal static class TestHelpers
{
    // ── DbContext em memória (nome único por teste) ──────────────────────────
    public static AppDbContext CreateDb(string name)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new AppDbContext(opts);
    }

    // ── IConfiguration com JWT para os testes ───────────────────────────────
    public static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"]              = "test-secret-key-must-be-at-least-32-chars!!",
                ["Jwt:Issuer"]           = "EventOps",
                ["Jwt:Audience"]         = "EventOps",
                ["Jwt:ExpiresInMinutes"] = "60"
            })
            .Build();

    // ── TokenService configurado para testes ────────────────────────────────
    public static TokenService CreateTokenService() =>
        new TokenService(CreateConfig());

    // ── Injeta um utilizador autenticado no ControllerContext ────────────────
    public static T WithUser<T>(this T controller, string role = "Organizador", int userId = 1)
        where T : ControllerBase
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, "test@eventops.com"),
            new Claim(ClaimTypes.Role,               role),
        };
        var identity  = new ClaimsIdentity(claims, authenticationType: "test");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }
}
