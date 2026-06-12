using EventOps.API.Services;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
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

    // ── IConfiguration com chaves adicionais/override ───────────────────────
    public static IConfiguration BuildConfig(Dictionary<string, string?> extra)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["Jwt:Key"]              = "test-secret-key-must-be-at-least-32-chars!!",
            ["Jwt:Issuer"]           = "EventOps",
            ["Jwt:Audience"]         = "EventOps",
            ["Jwt:ExpiresInMinutes"] = "60"
        };
        foreach (var kv in extra) defaults[kv.Key] = kv.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(defaults).Build();
    }

    // ── IHttpClientFactory que devolve um HttpClient pré-configurado ─────────
    public static IHttpClientFactory BuildHttpClientFactory(string name, HttpClient client)
        => new SingleClientFactory(name, client);

    // ── TokenService configurado para testes ────────────────────────────────
    public static TokenService CreateTokenService() =>
        new TokenService(CreateConfig());

    // ── Injeta um utilizador autenticado no ControllerContext ────────────────
    public static T WithUser<T>(this T controller, string role = "Organizador", int userId = 1, int? fornecedorId = null)
        where T : ControllerBase
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub,   userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, "test@eventops.com"),
            new Claim(ClaimTypes.Role,               role),
        };
        if (fornecedorId.HasValue)
            claims.Add(new Claim("fornecedorId", fornecedorId.Value.ToString()));
        var identity  = new ClaimsIdentity(claims.ToArray(), authenticationType: "test");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }
}

// ── Stub que nunca chama a API real ─────────────────────────────────────────

internal sealed class NullAnaliseIAService : IAnaliseIAService
{
    public Task<AnaliseIAResultado?> AnalisarCronogramaAsync(string contexto, CancellationToken ct = default)
        => Task.FromResult<AnaliseIAResultado?>(null);
}

file sealed class SingleClientFactory(string expectedName, HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        if (name != expectedName)
            throw new InvalidOperationException($"Factory não tem cliente '{name}'.");
        return client;
    }
}
