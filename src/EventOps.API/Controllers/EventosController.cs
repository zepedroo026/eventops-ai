using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EventOps.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EventosController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Evento>>> GetAll()
    {
        var query = db.Eventos
            .AsNoTracking()
            .Include(e => e.Organizador)
            .AsQueryable();

        // Organizadores only see their own events; Admins see everything
        if (User.IsInRole("Organizador"))
        {
            var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(sub, out var userId))
                query = query.Where(e => e.OrganizadorId == userId);
        }

        return Ok(await query.ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Evento>> GetById(int id)
    {
        var evento = await db.Eventos
            .AsNoTracking()
            .Include(e => e.Organizador)
            .Include(e => e.Salas)
            .Include(e => e.Atividades)
            .Include(e => e.Staff)
            .Include(e => e.Despesas)
            .Include(e => e.Tarefas)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (evento is null) return NotFound();
        return Ok(evento);
    }

    [HttpPost]
    public async Task<ActionResult<Evento>> Create(Evento evento)
    {
        db.Eventos.Add(evento);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = evento.Id }, evento);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Evento evento)
    {
        if (id != evento.Id) return BadRequest("O id do URL não corresponde ao do body.");

        var existente = await db.Eventos.FindAsync(id);
        if (existente is null) return NotFound();

        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(sub, out var userId) || existente.OrganizadorId != userId)
            return Forbid();

        existente.Nome            = evento.Nome;
        existente.Descricao       = evento.Descricao;
        existente.Localizacao     = evento.Localizacao;
        existente.DataInicio      = evento.DataInicio;
        existente.DataFim         = evento.DataFim;
        existente.OrcamentoMaximo = evento.OrcamentoMaximo;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var evento = await db.Eventos.FindAsync(id);
        if (evento is null) return NotFound();

        db.Eventos.Remove(evento);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/notas")]
    public async Task<IActionResult> AtualizarNotas(int id, [FromBody] AtualizarNotasRequest req)
    {
        var evento = await db.Eventos.FindAsync(id);
        if (evento is null) return NotFound();

        evento.Notas = req.Notas;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record AtualizarNotasRequest(string? Notas);
