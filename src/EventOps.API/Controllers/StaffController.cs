using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EventOps.API.Controllers;

public record SugestaoAlocacaoDto(
    int StaffId,
    string StaffNome,
    string? StaffFuncao,
    int AtividadeId,
    string AtividadeNome,
    string Motivo
);

public record AlocarItemDto(int StaffId, int AtividadeId);

public record AplicarEscalaResultDto(int Criadas, int Duplicadas);

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class StaffController(AppDbContext db) : ControllerBase
{
    // GET /api/staff/me — allocations for the logged-in staff user (matched by contact email)
    [HttpGet("me")]
    public async Task<ActionResult> GetMe()
    {
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email)
                    ?? User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var staffMembers = await db.Staff
            .AsNoTracking()
            .Where(s => s.Contacto == email)
            .Include(s => s.Alocacoes)
                .ThenInclude(a => a.Atividade)
                    .ThenInclude(at => at!.Sala)
            .Include(s => s.Evento)
            .ToListAsync();

        return Ok(staffMembers);
    }

    // GET /api/staff?eventoId=X
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Staff>>> GetAll([FromQuery] int? eventoId)
    {
        var query = db.Staff.AsNoTracking().AsQueryable();
        if (eventoId.HasValue)
            query = query.Where(s => s.EventoId == eventoId.Value);
        return Ok(await query.OrderBy(s => s.Nome).ToListAsync());
    }

    // GET /api/staff/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Staff>> GetById(int id)
    {
        var staff = await db.Staff
            .AsNoTracking()
            .Include(s => s.Alocacoes).ThenInclude(a => a.Atividade)
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
        existing.Nome     = staff.Nome;
        existing.Funcao   = staff.Funcao;
        existing.Contacto = staff.Contacto;
        existing.EventoId = staff.EventoId;
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

    // ── POST /api/staff/gerar-escala?eventoId=X ────────────────────────────
    [HttpPost("gerar-escala")]
    public async Task<ActionResult<IEnumerable<SugestaoAlocacaoDto>>> GerarEscala(
        [FromQuery] int eventoId)
    {
        if (!await db.Eventos.AnyAsync(e => e.Id == eventoId))
            return NotFound($"Evento com id {eventoId} não existe.");

        var atividades = await db.Atividades
            .AsNoTracking()
            .Where(a => a.EventoId == eventoId)
            .OrderBy(a => a.HoraInicio)
            .ToListAsync();

        var staff = await db.Staff
            .AsNoTracking()
            .Where(s => s.EventoId == eventoId)
            .ToListAsync();

        if (atividades.Count == 0 || staff.Count == 0)
            return Ok(Array.Empty<SugestaoAlocacaoDto>());

        // Alocações já existentes para este evento (para não repetir)
        var jaAlocados = await db.AlocacoesStaff
            .AsNoTracking()
            .Where(a => a.Atividade!.EventoId == eventoId)
            .Include(a => a.Atividade)
            .ToListAsync();

        // slots[staffId] = lista de (inicio, fim) já comprometidos (existentes + sugeridos)
        var slots = staff.ToDictionary(s => s.Id, _ => new List<(DateTime, DateTime)>());
        var contagem = staff.ToDictionary(s => s.Id, _ => 0);

        foreach (var al in jaAlocados.Where(a => a.Atividade is not null))
        {
            if (slots.TryGetValue(al.StaffId, out var lista))
            {
                lista.Add((al.Atividade!.HoraInicio, al.Atividade.HoraFim));
                contagem[al.StaffId]++;
            }
        }

        var sugestoes = new List<SugestaoAlocacaoDto>();

        foreach (var atividade in atividades)
        {
            var jaNestaAtividade = jaAlocados
                .Where(a => a.AtividadeId == atividade.Id)
                .Select(a => a.StaffId)
                .ToHashSet();

            // Sugestões já feitas para esta atividade nesta passagem
            var sugeridosNestaAtividade = sugestoes
                .Where(s => s.AtividadeId == atividade.Id)
                .Select(s => s.StaffId)
                .ToHashSet();

            var disponiveis = staff
                .Where(s => !jaNestaAtividade.Contains(s.Id))
                .Where(s => !sugeridosNestaAtividade.Contains(s.Id))
                .Where(s => !slots[s.Id].Any(slot => Overlap(slot, atividade)))
                .OrderByDescending(s => FuncaoScore(s, atividade))
                .ThenBy(s => contagem[s.Id])
                .ToList();

            foreach (var membro in disponiveis.Take(2))
            {
                var score = FuncaoScore(membro, atividade);
                var motivo = BuildMotivo(membro, atividade, contagem[membro.Id], score);

                sugestoes.Add(new SugestaoAlocacaoDto(
                    membro.Id, membro.Nome, membro.Funcao,
                    atividade.Id, atividade.Nome,
                    motivo));

                slots[membro.Id].Add((atividade.HoraInicio, atividade.HoraFim));
                contagem[membro.Id]++;
            }
        }

        return Ok(sugestoes);
    }

    // ── POST /api/staff/aplicar-escala ─────────────────────────────────────
    [HttpPost("aplicar-escala")]
    public async Task<ActionResult<AplicarEscalaResultDto>> AplicarEscala(
        [FromBody] List<AlocarItemDto> itens)
    {
        if (itens is null || itens.Count == 0)
            return BadRequest("Nenhuma alocação enviada.");

        var existentes = await db.AlocacoesStaff
            .Where(a => itens.Select(i => i.StaffId).Contains(a.StaffId))
            .Select(a => new { a.StaffId, a.AtividadeId })
            .ToListAsync();

        int criadas = 0, duplicadas = 0;

        foreach (var item in itens)
        {
            bool jáExiste = existentes.Any(e => e.StaffId == item.StaffId && e.AtividadeId == item.AtividadeId);
            if (jáExiste) { duplicadas++; continue; }

            db.AlocacoesStaff.Add(new AlocacaoStaff
            {
                StaffId     = item.StaffId,
                AtividadeId = item.AtividadeId,
            });
            criadas++;
        }

        await db.SaveChangesAsync();
        return Ok(new AplicarEscalaResultDto(criadas, duplicadas));
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static bool Overlap((DateTime inicio, DateTime fim) slot, Atividade a)
        => slot.inicio < a.HoraFim && a.HoraInicio < slot.fim;

    private static int FuncaoScore(Staff s, Atividade a)
    {
        if (string.IsNullOrWhiteSpace(s.Funcao)) return 0;

        var funcaoTokens = Tokenize(s.Funcao);
        var alvoTokens   = Tokenize(a.Nome + " " + (a.Descricao ?? ""));

        return funcaoTokens.Intersect(alvoTokens).Count();
    }

    private static HashSet<string> Tokenize(string text)
        => text.ToLowerInvariant()
               .Split([' ', '-', '/', '_', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
               .Where(t => t.Length > 2)
               .ToHashSet();

    private static string BuildMotivo(Staff s, Atividade a, int cargaAtual, int score)
    {
        var partes = new List<string>();

        if (score > 0)
            partes.Add($"Função '{s.Funcao}' compatível com a atividade");

        partes.Add(score > 0
            ? $"carga equilibrada ({cargaAtual} alocaç{(cargaAtual == 1 ? "ão" : "ões")} até agora)"
            : $"disponível no horário com menor carga ({cargaAtual} alocaç{(cargaAtual == 1 ? "ão" : "ões")})");

        return string.Join(", ", partes) + ".";
    }
}