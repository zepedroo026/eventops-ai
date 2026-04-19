using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventOps.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class StaffController(AppDbContext db) : ControllerBase
{
    // GET /api/staff?eventoId=X
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Staff>>> GetAll([FromQuery] int? eventoId)
    {
        var query = db.Staff.AsQueryable();

        if (eventoId.HasValue)
            query = query.Where(s => s.EventoId == eventoId.Value);

        return Ok(await query.OrderBy(s => s.Nome).ToListAsync());
    }

    // GET /api/staff/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Staff>> GetById(int id)
    {
        var staff = await db.Staff
            .Include(s => s.Alocacoes)
                .ThenInclude(a => a.Atividade)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (staff is null) return NotFound();
        return Ok(staff);
    }

    // POST /api/staff
    [HttpPost]
    public async Task<ActionResult<Staff>> Create(Staff staff)
    {
        if (!await db.Eventos.AnyAsync(e => e.Id == staff.EventoId))
            return BadRequest($"Evento com id {staff.EventoId} não existe.");

        db.Staff.Add(staff);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = staff.Id }, staff);
    }

    // PUT /api/staff/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Staff staff)
    {
        if (id != staff.Id) return BadRequest("O id do URL não corresponde ao do body.");

        var existing = await db.Staff.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Nome      = staff.Nome;
        existing.Funcao    = staff.Funcao;
        existing.Contacto  = staff.Contacto;
        existing.EventoId  = staff.EventoId;

        await db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/staff/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var staff = await db.Staff.FindAsync(id);
        if (staff is null) return NotFound();

        db.Staff.Remove(staff);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
