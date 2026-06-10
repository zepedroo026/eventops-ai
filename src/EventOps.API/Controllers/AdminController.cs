using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EventOps.API.Controllers;

public record UtilizadorAdminDto(
    int      Id,
    string   Nome,
    string   Email,
    string   Perfil,
    bool     Bloqueado,
    DateTime CriadoEm
);

public record StatsAdminDto(
    int     TotalEventos,
    int     TotalUtilizadores,
    int     TotalStaff,
    decimal TotalDespesas
);

public record AlterarPerfilRequest(int Perfil);

[Authorize(Roles = "Administrador")]
[ApiController]
[Route("api/[controller]")]
public class AdminController(AppDbContext db) : ControllerBase
{
    // GET /api/admin/utilizadores
    [HttpGet("utilizadores")]
    public async Task<ActionResult<IEnumerable<UtilizadorAdminDto>>> GetUtilizadores()
    {
        var utilizadores = await db.Utilizadores
            .OrderBy(u => u.Nome)
            .Select(u => new UtilizadorAdminDto(
                u.Id,
                u.Nome,
                u.Email,
                u.Perfil.ToString(),
                u.Bloqueado,
                u.CriadoEm))
            .ToListAsync();

        return Ok(utilizadores);
    }

    // GET /api/admin/stats
    [HttpGet("stats")]
    public async Task<ActionResult<StatsAdminDto>> GetStats()
    {
        var totalEventos      = await db.Eventos.CountAsync();
        var totalUtilizadores = await db.Utilizadores.CountAsync();
        var totalStaff        = await db.Staff.CountAsync();
        var totalDespesas     = await db.Despesas.SumAsync(d => (decimal?)d.Valor) ?? 0m;

        return Ok(new StatsAdminDto(totalEventos, totalUtilizadores, totalStaff, totalDespesas));
    }

    // PUT /api/admin/utilizadores/{id}/perfil
    [HttpPut("utilizadores/{id:int}/perfil")]
    public async Task<IActionResult> AlterarPerfil(int id, [FromBody] AlterarPerfilRequest req)
    {
        if (!Enum.IsDefined(typeof(Perfil), req.Perfil))
            return BadRequest("Perfil inválido. Valores aceites: 0 (Administrador), 1 (Organizador), 2 (Staff).");

        var utilizador = await db.Utilizadores.FindAsync(id);
        if (utilizador is null) return NotFound();

        utilizador.Perfil = (Perfil)req.Perfil;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // PUT /api/admin/utilizadores/{id}/bloquear — toggle Bloqueado
    [HttpPut("utilizadores/{id:int}/bloquear")]
    public async Task<ActionResult> ToggleBloquear(int id)
    {
        var utilizador = await db.Utilizadores.FindAsync(id);
        if (utilizador is null) return NotFound();

        utilizador.Bloqueado = !utilizador.Bloqueado;
        await db.SaveChangesAsync();
        return Ok(new { bloqueado = utilizador.Bloqueado });
    }

    // PUT /api/admin/eventos/{id}/aprovar
    [HttpPut("eventos/{id:int}/aprovar")]
    public async Task<IActionResult> AprovarEvento(int id)
    {
        var evento = await db.Eventos.FindAsync(id);
        if (evento is null) return NotFound($"Evento com id {id} não existe.");
        evento.Estado = EstadoEvento.Aprovado;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // PUT /api/admin/eventos/{id}/rejeitar
    [HttpPut("eventos/{id:int}/rejeitar")]
    public async Task<IActionResult> RejeitarEvento(int id)
    {
        var evento = await db.Eventos.FindAsync(id);
        if (evento is null) return NotFound($"Evento com id {id} não existe.");
        evento.Estado = EstadoEvento.Rejeitado;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/admin/utilizadores/{id}
    [HttpDelete("utilizadores/{id:int}")]
    public async Task<IActionResult> DeleteUtilizador(int id)
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(sub, out var currentUserId) && currentUserId == id)
            return BadRequest("Não é possível remover o próprio utilizador.");

        var utilizador = await db.Utilizadores.FindAsync(id);
        if (utilizador is null) return NotFound();

        db.Utilizadores.Remove(utilizador);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
