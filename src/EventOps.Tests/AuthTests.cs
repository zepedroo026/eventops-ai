using EventOps.API.Controllers;
using EventOps.API.DTOs;
using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace EventOps.Tests;

/// <summary>
/// Testes de autenticação: registo, login, conta bloqueada.
/// </summary>
public class AuthTests
{
    // ── 1. Registo com e-mail duplicado deve devolver 409 ────────────────────

    [Fact]
    public async Task Registo_EmailDuplicado_DeveDevolver409()
    {
        using var db = TestHelpers.CreateDb(nameof(Registo_EmailDuplicado_DeveDevolver409));

        // Seed — utilizador já existe
        db.Utilizadores.Add(new Utilizador
        {
            Nome         = "Utilizador Existente",
            Email        = "duplicado@eventops.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("senha123"),
            Perfil       = Perfil.Organizador,
        });
        await db.SaveChangesAsync();

        var ctrl   = new AuthController(db, TestHelpers.CreateTokenService());
        var result = await ctrl.Register(
            new RegisterRequest("Novo Utilizador", "duplicado@eventops.com", "outra_senha", "Organizador"));

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(409, conflict.StatusCode);
    }

    // ── 2. Login com password errada deve devolver 401 ───────────────────────

    [Fact]
    public async Task Login_PasswordErrada_DeveDevolver401()
    {
        using var db = TestHelpers.CreateDb(nameof(Login_PasswordErrada_DeveDevolver401));

        db.Utilizadores.Add(new Utilizador
        {
            Nome         = "Utilizador",
            Email        = "user@eventops.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("senhaCorreta"),
            Perfil       = Perfil.Organizador,
        });
        await db.SaveChangesAsync();

        var ctrl   = new AuthController(db, TestHelpers.CreateTokenService());
        var result = await ctrl.Login(new LoginRequest("user@eventops.com", "senhaErrada"));

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(401, unauthorized.StatusCode);
    }

    // ── 3. Login com conta bloqueada deve devolver 403 ───────────────────────

    [Fact]
    public async Task Login_ContaBloqueada_DeveDevolver403()
    {
        using var db = TestHelpers.CreateDb(nameof(Login_ContaBloqueada_DeveDevolver403));

        db.Utilizadores.Add(new Utilizador
        {
            Nome         = "Utilizador Bloqueado",
            Email        = "bloqueado@eventops.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("senha123"),
            Perfil       = Perfil.Organizador,
            Bloqueado    = true,
        });
        await db.SaveChangesAsync();

        var ctrl   = new AuthController(db, TestHelpers.CreateTokenService());
        var result = await ctrl.Login(new LoginRequest("bloqueado@eventops.com", "senha123"));

        // AuthController devolve StatusCode(403, "...")
        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbidden.StatusCode);
    }

    // ── 4. Login bem-sucedido deve devolver token e dados corretos ───────────

    [Fact]
    public async Task Login_CredenciaisValidas_DeveDevolver200ComToken()
    {
        using var db = TestHelpers.CreateDb(nameof(Login_CredenciaisValidas_DeveDevolver200ComToken));

        db.Utilizadores.Add(new Utilizador
        {
            Nome         = "Admin",
            Email        = "admin@eventops.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("senha123"),
            Perfil       = Perfil.Administrador,
        });
        await db.SaveChangesAsync();

        var ctrl   = new AuthController(db, TestHelpers.CreateTokenService());
        var result = await ctrl.Login(new LoginRequest("admin@eventops.com", "senha123"));

        var ok       = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuthResponse>(ok.Value);

        Assert.Equal("Admin",              response.Nome);
        Assert.Equal("admin@eventops.com", response.Email);
        Assert.Equal("Administrador",      response.Perfil);
        Assert.False(string.IsNullOrEmpty(response.Token));
    }
}
