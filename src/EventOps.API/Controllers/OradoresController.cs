using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventOps.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OradoresController(AppDbContext db) : ControllerBase
{
    // GET /api/oradores?eventoId=X
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Orador>>> GetAll([FromQuery] int eventoId)
    {
        var oradores = await db.Oradores
            .AsNoTracking()
            .Where(o => o.EventoId == eventoId)
            .Include(o => o.Requisitos)
            .OrderBy(o => o.Nome)
            .ToListAsync();
        return Ok(oradores);
    }

    // GET /api/oradores/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Orador>> GetById(int id)
    {
        var orador = await db.Oradores
            .AsNoTracking()
            .Include(o => o.Requisitos)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (orador is null) return NotFound();
        return Ok(orador);
    }

    // POST /api/oradores
    [HttpPost]
    public async Task<ActionResult<Orador>> Create(Orador orador)
    {
        if (!await db.Eventos.AnyAsync(e => e.Id == orador.EventoId))
            return BadRequest($"Evento com id {orador.EventoId} não existe.");
        db.Oradores.Add(orador);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = orador.Id }, orador);
    }

    // PUT /api/oradores/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Orador orador)
    {
        if (id != orador.Id) return BadRequest("O id do URL não corresponde ao do body.");
        var existing = await db.Oradores.FindAsync(id);
        if (existing is null) return NotFound();
        existing.Nome           = orador.Nome;
        existing.Email          = orador.Email;
        existing.Telefone       = orador.Telefone;
        existing.Bio            = orador.Bio;
        existing.EstadoContrato = orador.EstadoContrato;
        existing.Cache          = orador.Cache;
        existing.NotasContrato  = orador.NotasContrato;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/oradores/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var orador = await db.Oradores.FindAsync(id);
        if (orador is null) return NotFound();
        db.Oradores.Remove(orador);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Requisitos ──────────────────────────────────────────────────────────

    // GET /api/oradores/requisitos-pendentes?eventoId=X
    [HttpGet("requisitos-pendentes")]
    public async Task<ActionResult<IEnumerable<object>>> GetRequisitosPendentes([FromQuery] int eventoId)
    {
        var pendentes = await db.RequisitosOrador
            .AsNoTracking()
            .Where(r => r.Estado == EstadoRequisito.Pendente && r.Orador!.EventoId == eventoId)
            .Include(r => r.Orador)
            .OrderBy(r => r.Tipo)
            .Select(r => new
            {
                r.Id,
                r.Tipo,
                r.Descricao,
                r.Estado,
                r.Custo,
                OradorId   = r.OradorId,
                OradorNome = r.Orador!.Nome,
            })
            .ToListAsync();
        return Ok(pendentes);
    }

    // POST /api/oradores/{oradorId}/requisitos
    [HttpPost("{oradorId:int}/requisitos")]
    public async Task<ActionResult<RequisitoOrador>> AddRequisito(int oradorId, RequisitoOrador req)
    {
        if (!await db.Oradores.AnyAsync(o => o.Id == oradorId)) return NotFound("Orador não encontrado.");
        req.OradorId = oradorId;
        db.RequisitosOrador.Add(req);
        await db.SaveChangesAsync();
        return Created(string.Empty, req);
    }

    // PUT /api/oradores/requisitos/{id}
    [HttpPut("requisitos/{id:int}")]
    public async Task<IActionResult> UpdateRequisito(int id, RequisitoOrador req)
    {
        var existing = await db.RequisitosOrador.FindAsync(id);
        if (existing is null) return NotFound();
        existing.Tipo      = req.Tipo;
        existing.Descricao = req.Descricao;
        existing.Estado    = req.Estado;
        existing.Custo     = req.Custo;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/oradores/requisitos/{id}
    [HttpDelete("requisitos/{id:int}")]
    public async Task<IActionResult> DeleteRequisito(int id)
    {
        var req = await db.RequisitosOrador.FindAsync(id);
        if (req is null) return NotFound();
        db.RequisitosOrador.Remove(req);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
