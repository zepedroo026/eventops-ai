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
            .Include(e => e.Tarefas)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (evento is null) return NotFound();
        return Ok(evento);
    }

    [HttpPost]
    [Authorize(Roles = "Organizador")]
    public async Task<ActionResult<Evento>> Create(Evento evento)
    {
        evento.Estado = EstadoEvento.Pendente; // garantia — nunca aceitar estado do cliente
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

        // Admin pode apagar qualquer evento; Organizador só os seus
        if (!User.IsInRole("Administrador"))
        {
            var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(sub, out var userId) || evento.OrganizadorId != userId)
                return Forbid();
        }

        db.Eventos.Remove(evento);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/notas")]
    public async Task<IActionResult> AtualizarNotas(int id, [FromBody] AtualizarNotasRequest req)
    {
        var evento = await db.Eventos.FindAsync(id);
        if (evento is null) return NotFound();

        // Apenas o criador pode editar notas
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(sub, out var userId) || evento.OrganizadorId != userId)
            return Forbid();

        evento.Notas = req.Notas;
        await db.SaveChangesAsync();
        return NoContent();
    }
    // GET /api/eventos/{id}/orcamento-resumo
    [HttpGet("{id:int}/orcamento-resumo")]
    public async Task<ActionResult<OrcamentoResumoDto>> GetOrcamentoResumo(int id)
    {
        var evento = await db.Eventos.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (evento is null) return NotFound();

        var previstos = await db.OrcamentosCategoria.AsNoTracking()
            .Where(o => o.EventoId == id).ToListAsync();

        var despesas  = await db.Despesas.AsNoTracking()
            .Where(d => d.EventoId == id).ToListAsync();

        var realPorCat = despesas
            .GroupBy(d => d.Categoria ?? "Sem categoria")
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Valor));

        var todasCats = previstos.Select(p => p.Categoria)
            .Union(realPorCat.Keys).Distinct();

        var linhas = todasCats.Select(cat =>
        {
            var prev   = previstos.FirstOrDefault(p => p.Categoria == cat)?.ValorPrevisto ?? 0m;
            var real   = realPorCat.GetValueOrDefault(cat, 0m);
            var desvio = real - prev;
            var pct    = prev > 0 ? Math.Round(desvio / prev * 100, 2) : 0m;
            return new OrcamentoLinhaDto(cat, prev, real, desvio, pct);
        }).OrderBy(l => l.Categoria).ToList();

        var totPrev    = linhas.Sum(l => l.Previsto);
        var totReal    = linhas.Sum(l => l.Real);
        var totDesvio  = totReal - totPrev;
        var totPct     = totPrev > 0 ? Math.Round(totDesvio / totPrev * 100, 2) : 0m;

        return Ok(new OrcamentoResumoDto(
            linhas, totPrev, totReal, totDesvio, totPct,
            evento.OrcamentoMaximo,
            totReal > evento.OrcamentoMaximo));
    }

    // POST /api/eventos/{id}/orcamento-categorias  (upsert previsto por categoria)
    [HttpPost("{id:int}/orcamento-categorias")]
    public async Task<IActionResult> UpsertOrcamentoCategoria(
        int id, [FromBody] OrcamentoCategoriaRequest req)
    {
        if (!await db.Eventos.AnyAsync(e => e.Id == id)) return NotFound();

        var existing = await db.OrcamentosCategoria
            .FirstOrDefaultAsync(o => o.EventoId == id && o.Categoria == req.Categoria);

        if (existing is null)
            db.OrcamentosCategoria.Add(new OrcamentoCategoria
                { EventoId = id, Categoria = req.Categoria, ValorPrevisto = req.ValorPrevisto });
        else
            existing.ValorPrevisto = req.ValorPrevisto;

        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record AtualizarNotasRequest(string? Notas);
public record OrcamentoCategoriaRequest(string Categoria, decimal ValorPrevisto);
public record OrcamentoLinhaDto(
    string Categoria, decimal Previsto, decimal Real,
    decimal Desvio, decimal DesvioPerc);
public record OrcamentoResumoDto(
    IEnumerable<OrcamentoLinhaDto> Linhas,
    decimal TotalPrevisto, decimal TotalReal,
    decimal TotalDesvio, decimal TotalDesvioPerc,
    decimal OrcamentoMaximo, bool ExcedeOrcamento);
