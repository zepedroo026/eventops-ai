using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventOps.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SalasController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Sala>>> GetAll([FromQuery] int? eventoId)
    {
        var query = db.Salas.Include(s => s.Evento).AsQueryable();

        if (eventoId.HasValue)
            query = query.Where(s => s.EventoId == eventoId.Value);

        return Ok(await query.ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Sala>> GetById(int id)
    {
        var sala = await db.Salas
            .Include(s => s.Evento)
            .Include(s => s.Atividades)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sala is null) return NotFound();
        return Ok(sala);
    }

    [HttpPost]
    public async Task<ActionResult<Sala>> Create(Sala sala)
    {
        var eventoExiste = await db.Eventos.AnyAsync(e => e.Id == sala.EventoId);
        if (!eventoExiste) return BadRequest($"Evento com id {sala.EventoId} não existe.");

        db.Salas.Add(sala);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = sala.Id }, sala);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Sala sala)
    {
        if (id != sala.Id) return BadRequest("O id do URL não corresponde ao do body.");

        var existe = await db.Salas.AnyAsync(s => s.Id == id);
        if (!existe) return NotFound();

        db.Entry(sala).State = EntityState.Modified;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var sala = await db.Salas.FindAsync(id);
        if (sala is null) return NotFound();

        db.Salas.Remove(sala);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
