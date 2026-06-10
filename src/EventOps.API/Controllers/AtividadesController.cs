using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventOps.API.Controllers;

public record ConflitosDto(
    string Tipo,
    string Descricao,
    int AtividadeAId,
    string AtividadeANome,
    int AtividadeBId,
    string AtividadeBNome
);

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AtividadesController(AppDbContext db) : ControllerBase
{
    // GET /api/atividades?eventoId=X
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Atividade>>> GetAll([FromQuery] int? eventoId)
    {
        var query = db.Atividades
            .AsNoTracking()
            .Include(a => a.Sala)
            .AsQueryable();

        if (eventoId.HasValue)
            query = query.Where(a => a.EventoId == eventoId.Value);

        return Ok(await query.OrderBy(a => a.HoraInicio).ToListAsync());
    }

    // GET /api/atividades/conflitos?eventoId=X  — deve vir antes de {id:int}
    [HttpGet("conflitos")]
    public async Task<ActionResult<IEnumerable<ConflitosDto>>> GetConflitos([FromQuery] int eventoId)
    {
        // Atividades do evento-alvo (para conflitos de sala e como âncora dos conflitos de staff)
        var atividadesEvento = await db.Atividades
            .AsNoTracking()
            .Where(a => a.EventoId == eventoId)
            .Include(a => a.Sala)
            .Include(a => a.Alocacoes)
                .ThenInclude(al => al.Staff)
            .ToListAsync();

        var conflitos = new List<ConflitosDto>();

        // ── 1. Conflitos de sala (dentro do evento) ───────────────────────────
        foreach (var grupo in atividadesEvento.GroupBy(a => a.SalaId))
        {
            var lista = grupo.ToList();
            for (int i = 0; i < lista.Count; i++)
                for (int j = i + 1; j < lista.Count; j++)
                {
                    var a = lista[i]; var b = lista[j];
                    if (a.HoraInicio < b.HoraFim && b.HoraInicio < a.HoraFim)
                        conflitos.Add(new ConflitosDto(
                            Tipo: "SalaConflito",
                            Descricao: $"Sala '{a.Sala?.Nome ?? a.SalaId.ToString()}': " +
                                       $"'{a.Nome}' ({a.HoraInicio:HH:mm}–{a.HoraFim:HH:mm}) e " +
                                       $"'{b.Nome}' ({b.HoraInicio:HH:mm}–{b.HoraFim:HH:mm}) têm horários sobrepostos.",
                            AtividadeAId: a.Id, AtividadeANome: a.Nome,
                            AtividadeBId: b.Id, AtividadeBNome: b.Nome));
                }
        }

        // ── 2. Conflitos de staff (entre todos os eventos do organizador) ─────
        var orgId = await db.Eventos.AsNoTracking()
            .Where(e => e.Id == eventoId)
            .Select(e => e.OrganizadorId)
            .FirstOrDefaultAsync();

        var orgEventIds = await db.Eventos.AsNoTracking()
            .Where(e => e.OrganizadorId == orgId)
            .Select(e => e.Id)
            .ToListAsync();

        // Todas as atividades do organizador com as respetivas alocações
        var todasAtividades = await db.Atividades
            .AsNoTracking()
            .Where(a => orgEventIds.Contains(a.EventoId))
            .Include(a => a.Alocacoes)
                .ThenInclude(al => al.Staff)
            .ToListAsync();

        // Staff IDs presentes no evento-alvo
        var staffNoEvento = atividadesEvento
            .SelectMany(a => a.Alocacoes.Select(al => al.StaffId))
            .ToHashSet();

        var alocacoesPorStaff = todasAtividades
            .SelectMany(a => a.Alocacoes.Select(al => (Alocacao: al, Atividade: a)))
            .GroupBy(x => x.Alocacao.StaffId);

        foreach (var grupo in alocacoesPorStaff)
        {
            // Só interessa se o membro estiver alocado a este evento
            if (!staffNoEvento.Contains(grupo.Key)) continue;

            var lista = grupo.ToList();
            for (int i = 0; i < lista.Count; i++)
                for (int j = i + 1; j < lista.Count; j++)
                {
                    var a = lista[i].Atividade;
                    var b = lista[j].Atividade;

                    if (a.HoraInicio >= b.HoraFim || b.HoraInicio >= a.HoraFim) continue;

                    // Pelo menos uma das atividades deve pertencer ao evento-alvo
                    if (a.EventoId != eventoId && b.EventoId != eventoId) continue;

                    var staffNome  = lista[i].Alocacao.Staff?.Nome ?? $"Staff #{grupo.Key}";
                    var crossEvent = a.EventoId != b.EventoId;
                    var sufixoA    = crossEvent && a.EventoId != eventoId ? $" (outro evento)" : "";
                    var sufixoB    = crossEvent && b.EventoId != eventoId ? $" (outro evento)" : "";

                    conflitos.Add(new ConflitosDto(
                        Tipo: "StaffConflito",
                        Descricao: $"'{staffNome}' está alocado simultaneamente a " +
                                   $"'{a.Nome}'{sufixoA} ({a.HoraInicio:HH:mm}–{a.HoraFim:HH:mm}) e " +
                                   $"'{b.Nome}'{sufixoB} ({b.HoraInicio:HH:mm}–{b.HoraFim:HH:mm}).",
                        AtividadeAId: a.Id, AtividadeANome: a.Nome,
                        AtividadeBId: b.Id, AtividadeBNome: b.Nome));
                }
        }

        return Ok(conflitos);
    }

    // GET /api/atividades/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Atividade>> GetById(int id)
    {
        var atividade = await db.Atividades
            .AsNoTracking()
            .Include(a => a.Sala)
            .Include(a => a.Alocacoes)
                .ThenInclude(al => al.Staff)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (atividade is null) return NotFound();
        return Ok(atividade);
    }

    // POST /api/atividades
    [HttpPost]
    public async Task<ActionResult<Atividade>> Create(Atividade atividade)
    {
        if (atividade.HoraInicio >= atividade.HoraFim)
            return BadRequest("HoraInicio deve ser anterior a HoraFim.");

        if (!await db.Eventos.AnyAsync(e => e.Id == atividade.EventoId))
            return BadRequest($"Evento com id {atividade.EventoId} não existe.");

        var sala = await db.Salas.FindAsync(atividade.SalaId);
        if (sala is null)
            return BadRequest($"Sala com id {atividade.SalaId} não existe.");
        if (sala.EventoId != atividade.EventoId)
            return BadRequest("A sala não pertence ao evento indicado.");

        db.Atividades.Add(atividade);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = atividade.Id }, atividade);
    }

    // PUT /api/atividades/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Atividade atividade)
    {
        if (id != atividade.Id) return BadRequest("O id do URL não corresponde ao do body.");

        if (atividade.HoraInicio >= atividade.HoraFim)
            return BadRequest("HoraInicio deve ser anterior a HoraFim.");

        var existing = await db.Atividades.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Nome        = atividade.Nome;
        existing.Descricao   = atividade.Descricao;
        existing.HoraInicio  = atividade.HoraInicio;
        existing.HoraFim     = atividade.HoraFim;
        existing.SalaId      = atividade.SalaId;
        existing.EventoId    = atividade.EventoId;

        await db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/atividades/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var atividade = await db.Atividades.FindAsync(id);
        if (atividade is null) return NotFound();

        db.Atividades.Remove(atividade);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
