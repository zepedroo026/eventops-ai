using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventOps.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EventosController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Evento>>> GetAll()
    {
        var eventos = await db.Eventos
            .Include(e => e.Organizador)
            .ToListAsync();
        return Ok(eventos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Evento>> GetById(int id)
    {
        var evento = await db.Eventos
            .Include(e => e.Organizador)
            .Include(e => e.Salas)
            .Include(e => e.Atividades)
            .Include(e => e.Staff)
            .Include(e => e.Despesas)
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

        var existe = await db.Eventos.AnyAsync(e => e.Id == id);
        if (!existe) return NotFound();

        db.Entry(evento).State = EntityState.Modified;
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
}
