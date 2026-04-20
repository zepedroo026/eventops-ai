using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventOps.API.Controllers;

public record CategoriaTotalDto(string Categoria, decimal Total, int Quantidade);

public record ResumoDto(
    decimal TotalGasto,
    decimal OrcamentoMaximo,
    decimal PercentagemUtilizada,
    IEnumerable<CategoriaTotalDto> PorCategoria
);

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DespesasController(AppDbContext db) : ControllerBase
{
    // GET /api/despesas?eventoId=X
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Despesa>>> GetAll([FromQuery] int? eventoId)
    {
        var query = db.Despesas.AsQueryable();

        if (eventoId.HasValue)
            query = query.Where(d => d.EventoId == eventoId.Value);

        return Ok(await query.OrderByDescending(d => d.Data).ToListAsync());
    }

    // GET /api/despesas/resumo?eventoId=X  — antes de {id:int}
    [HttpGet("resumo")]
    public async Task<ActionResult<ResumoDto>> GetResumo([FromQuery] int eventoId)
    {
        var evento = await db.Eventos.FindAsync(eventoId);
        if (evento is null) return NotFound($"Evento com id {eventoId} não existe.");

        var despesas = await db.Despesas
            .Where(d => d.EventoId == eventoId)
            .ToListAsync();

        var total = despesas.Sum(d => d.Valor);
        var percentagem = evento.OrcamentoMaximo > 0
            ? Math.Round(total / evento.OrcamentoMaximo * 100, 2)
            : 0;

        var porCategoria = despesas
            .GroupBy(d => string.IsNullOrWhiteSpace(d.Categoria) ? "Sem categoria" : d.Categoria)
            .Select(g => new CategoriaTotalDto(g.Key, g.Sum(d => d.Valor), g.Count()))
            .OrderByDescending(c => c.Total);

        return Ok(new ResumoDto(total, evento.OrcamentoMaximo, percentagem, porCategoria));
    }

    // GET /api/despesas/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Despesa>> GetById(int id)
    {
        var despesa = await db.Despesas.FindAsync(id);
        if (despesa is null) return NotFound();
        return Ok(despesa);
    }

    // POST /api/despesas
    [HttpPost]
    public async Task<ActionResult<Despesa>> Create(Despesa despesa)
    {
        if (despesa.Valor < 0)
            return BadRequest("O valor não pode ser negativo.");

        if (!await db.Eventos.AnyAsync(e => e.Id == despesa.EventoId))
            return BadRequest($"Evento com id {despesa.EventoId} não existe.");

        db.Despesas.Add(despesa);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = despesa.Id }, despesa);
    }

    // PUT /api/despesas/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Despesa despesa)
    {
        if (id != despesa.Id) return BadRequest("O id do URL não corresponde ao do body.");
        if (despesa.Valor < 0) return BadRequest("O valor não pode ser negativo.");

        var existing = await db.Despesas.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Descricao = despesa.Descricao;
        existing.Valor     = despesa.Valor;
        existing.Categoria = despesa.Categoria;
        existing.Data      = despesa.Data;
        existing.EventoId  = despesa.EventoId;

        await db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/despesas/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var despesa = await db.Despesas.FindAsync(id);
        if (despesa is null) return NotFound();

        db.Despesas.Remove(despesa);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
