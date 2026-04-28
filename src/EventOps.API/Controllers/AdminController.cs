using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventOps.API.Controllers;

public record UtilizadorAdminDto(
    int    Id,
    string Nome,
    string Email,
    string Perfil,
    DateTime CriadoEm
);

public record StatsAdminDto(
    int     TotalEventos,
    int     TotalUtilizadores,
    int     TotalStaff,
    decimal TotalDespesas
);

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
}
