using EventOps.API.DTOs;
using EventOps.API.Services;
using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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

    [Authorize]
    [HttpPut("perfil")]
    public async Task<ActionResult<AtualizarPerfilResponse>> AtualizarPerfil(AtualizarPerfilRequest req)
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(sub, out var userId)) return Unauthorized();

        var utilizador = await db.Utilizadores.FindAsync(userId);
        if (utilizador is null) return NotFound();

        if (!string.IsNullOrEmpty(req.NovaPassword))
        {
            if (string.IsNullOrEmpty(req.PasswordAtual))
                return BadRequest("A password atual é obrigatória para alterar a password.");
            if (!BCrypt.Net.BCrypt.Verify(req.PasswordAtual, utilizador.PasswordHash))
                return BadRequest("Password atual incorreta.");
            utilizador.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NovaPassword);
        }

        utilizador.Nome = req.Nome.Trim();
        await db.SaveChangesAsync();

        return Ok(new AtualizarPerfilResponse(utilizador.Nome, utilizador.Email, utilizador.Perfil.ToString()));
    }
}
