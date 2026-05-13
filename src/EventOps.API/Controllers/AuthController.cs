using EventOps.API.DTOs;
using EventOps.API.Services;
using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventOps.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, TokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        if (await db.Utilizadores.AnyAsync(u => u.Email == req.Email))
            return Conflict("Email já registado.");

        if (!Enum.TryParse<Perfil>(req.Perfil, ignoreCase: true, out var perfil))
            return BadRequest($"Perfil inválido. Valores aceites: {string.Join(", ", Enum.GetNames<Perfil>())}");

        var utilizador = new Utilizador
        {
            Nome = req.Nome,
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Perfil = perfil
        };

        db.Utilizadores.Add(utilizador);
        await db.SaveChangesAsync();

        var (token, expira) = tokenService.Generate(utilizador);
        return StatusCode(StatusCodes.Status201Created,
            new AuthResponse(token, utilizador.Nome, utilizador.Email, utilizador.Perfil.ToString(), expira));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var utilizador = await db.Utilizadores.FirstOrDefaultAsync(u => u.Email == req.Email);

        if (utilizador is null || !BCrypt.Net.BCrypt.Verify(req.Password, utilizador.PasswordHash))
            return Unauthorized("Email ou password incorretos.");

        if (utilizador.Bloqueado)
            return StatusCode(StatusCodes.Status403Forbidden, "Conta bloqueada.");

        var (token, expira) = tokenService.Generate(utilizador);
        return Ok(new AuthResponse(token, utilizador.Nome, utilizador.Email, utilizador.Perfil.ToString(), expira));
    }
}
