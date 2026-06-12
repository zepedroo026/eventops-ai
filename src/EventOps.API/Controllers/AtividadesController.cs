using EventOps.API.Services;
using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

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
public class AtividadesController(AppDbContext db, IAnaliseIAService analiseIA) : ControllerBase
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

    // GET /api/atividades/validar-cronograma?eventoId=X
    [HttpGet("validar-cronograma")]
    public async Task<ActionResult<ValidacaoCronogramaDto>> ValidarCronograma([FromQuery] int eventoId)
    {
        // Reutiliza a lógica de conflitos existente
        var conflitosResult = await GetConflitos(eventoId);
        var conflitos = conflitosResult.Result is OkObjectResult ok
            ? (ok.Value as IEnumerable<ConflitosDto>)?.ToList() ?? []
            : [];

        var atividades = await db.Atividades.AsNoTracking()
            .Where(a => a.EventoId == eventoId)
            .Include(a => a.Alocacoes)
            .OrderBy(a => a.HoraInicio)
            .ToListAsync();

        var conflitosStaff = conflitos.Count(c => c.Tipo == "StaffConflito");
        var conflitosSala  = conflitos.Count(c => c.Tipo == "SalaConflito");
        var semStaff       = atividades.Count(a => !a.Alocacoes.Any());

        var riscos = new List<string>();
        if (conflitosSala  > 0) riscos.Add($"{conflitosSala} conflito(s) de sala — mesma sala usada em horários sobrepostos.");
        if (conflitosStaff > 0) riscos.Add($"{conflitosStaff} conflito(s) de staff — membro(s) alocados a atividades simultâneas.");
        if (semStaff       > 0) riscos.Add($"{semStaff} atividade(s) sem staff alocado.");
        if (!atividades.Any()) riscos.Add("Nenhuma atividade registada — cronograma vazio.");

        var nivel = riscos.Count == 0 ? "OK"
            : (conflitosSala + conflitosStaff) > 0 ? "Critico"
            : "Aviso";

        var resumo = nivel == "OK"
            ? "Cronograma sem conflitos detetados. Pronto para execução."
            : $"Detetados {conflitos.Count} conflito(s): {string.Join(" ", riscos)}";

        return Ok(new ValidacaoCronogramaDto(nivel, resumo, riscos, conflitos));
    }

    // POST /api/atividades/analisar-cronograma?eventoId=X
    [HttpPost("analisar-cronograma")]
    [Authorize(Roles = "Organizador,Administrador")]
    public async Task<ActionResult<AnaliseCronogramaDto>> AnalisarCronograma(
        [FromQuery] int eventoId, CancellationToken ct)
    {
        // 1. Validação determinística existente
        var validacaoResult = await ValidarCronograma(eventoId);
        var validacao = validacaoResult.Result is OkObjectResult vok
            ? (ValidacaoCronogramaDto?)vok.Value
            : null;
        if (validacao is null) return NotFound();

        // 2. Carrega contexto completo para o LLM
        var evento = await db.Eventos.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventoId, ct);
        var atividades = await db.Atividades.AsNoTracking()
            .Where(a => a.EventoId == eventoId)
            .Include(a => a.Sala)
            .Include(a => a.Alocacoes).ThenInclude(al => al.Staff)
            .OrderBy(a => a.HoraInicio)
            .ToListAsync(ct);
        var salas = await db.Salas.AsNoTracking()
            .Where(s => s.EventoId == eventoId)
            .ToListAsync(ct);

        // 3. Constrói o prompt com todo o contexto
        var sb = new StringBuilder();
        sb.AppendLine($"EVENTO: {evento?.Nome ?? "N/A"}");
        sb.AppendLine($"DATA: {evento?.DataInicio:dd/MM/yyyy HH:mm} → {evento?.DataFim:dd/MM/yyyy HH:mm}");
        sb.AppendLine();

        sb.AppendLine("SALAS E OCUPAÇÕES:");
        foreach (var sala in salas)
        {
            var ocups = atividades
                .Where(a => a.SalaId == sala.Id)
                .OrderBy(a => a.HoraInicio)
                .Select(a => $"{a.HoraInicio:HH:mm}-{a.HoraFim:HH:mm} ({a.Nome})");
            sb.AppendLine($"  {sala.Nome} (cap.{sala.Capacidade}): {(ocups.Any() ? string.Join(", ", ocups) : "livre")}");
        }
        sb.AppendLine();

        sb.AppendLine($"CRONOGRAMA ({atividades.Count} atividades):");
        foreach (var a in atividades)
        {
            var staffNomes = a.Alocacoes.Any()
                ? string.Join(", ", a.Alocacoes.Select(al => al.Staff?.Nome ?? "?"))
                : "sem staff";
            sb.AppendLine($"  {a.HoraInicio:dd/MM HH:mm}-{a.HoraFim:HH:mm} | {a.Nome} | Sala: {a.Sala?.Nome ?? "?"} | Staff: {staffNomes}");
        }
        sb.AppendLine();

        var conflitos = validacao.Conflitos.ToList();
        sb.AppendLine($"CONFLITOS DETETADOS ({conflitos.Count}):");
        for (int i = 0; i < conflitos.Count; i++)
            sb.AppendLine($"  [{i}] {conflitos[i].Tipo}: {conflitos[i].Descricao}");
        sb.AppendLine();

        sb.AppendLine("RISCOS AUTOMÁTICOS:");
        foreach (var r in validacao.Riscos)
            sb.AppendLine($"  - {r}");
        sb.AppendLine();
        sb.AppendLine("Analisa o cronograma e fornece: (a) resumo executivo, (b) sugestão para cada conflito usando as salas livres nos horários indicados, (c) riscos operacionais não óbvios.");

        // 4. Chama o LLM com fallback gracioso
        var resultado = await analiseIA.AnalisarCronogramaAsync(sb.ToString(), ct);

        return Ok(new AnaliseCronogramaDto(validacao, resultado, IaDisponivel: resultado is not null));
    }
}

public record ValidacaoCronogramaDto(
    string Nivel,      // "OK" | "Aviso" | "Critico"
    string Resumo,
    IEnumerable<string> Riscos,
    IEnumerable<ConflitosDto> Conflitos);

public record AnaliseCronogramaDto(
    ValidacaoCronogramaDto Validacao,
    AnaliseIAResultado?    AnaliseIA,
    bool                   IaDisponivel);
