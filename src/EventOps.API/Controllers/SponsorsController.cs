using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventOps.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SponsorsController(AppDbContext db) : ControllerBase
{
    // GET /api/sponsors?eventoId=X
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Sponsor>>> GetAll([FromQuery] int eventoId)
    {
        var sponsors = await db.Sponsors
            .AsNoTracking()
            .Where(s => s.EventoId == eventoId)
            .OrderByDescending(s => s.Nivel)
            .ThenBy(s => s.Nome)
            .ToListAsync();
        return Ok(sponsors);
    }

    // GET /api/sponsors/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Sponsor>> GetById(int id)
    {
        var sponsor = await db.Sponsors.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (sponsor is null) return NotFound();
        return Ok(sponsor);
    }

    // POST /api/sponsors
    [HttpPost]
    public async Task<ActionResult<Sponsor>> Create(Sponsor sponsor)
    {
        if (!await db.Eventos.AnyAsync(e => e.Id == sponsor.EventoId))
            return BadRequest($"Evento com id {sponsor.EventoId} não existe.");
        if (sponsor.ValorPatrocinio < 0)
            return BadRequest("O valor de patrocínio não pode ser negativo.");
        db.Sponsors.Add(sponsor);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = sponsor.Id }, sponsor);
    }

    // PUT /api/sponsors/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Sponsor sponsor)
    {
        if (id != sponsor.Id) return BadRequest("O id do URL não corresponde ao do body.");
        var existing = await db.Sponsors.FindAsync(id);
        if (existing is null) return NotFound();
        existing.Nome             = sponsor.Nome;
        existing.Empresa          = sponsor.Empresa;
        existing.Email            = sponsor.Email;
        existing.Nivel            = sponsor.Nivel;
        existing.ValorPatrocinio  = sponsor.ValorPatrocinio;
        existing.EstadoContrato   = sponsor.EstadoContrato;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/sponsors/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var sponsor = await db.Sponsors.FindAsync(id);
        if (sponsor is null) return NotFound();
        db.Sponsors.Remove(sponsor);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
