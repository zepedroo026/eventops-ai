using EventOps.API.Controllers;
using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace EventOps.Tests;

/// <summary>
/// Testes de validação de negócio: atividades inválidas e alocações duplicadas.
/// </summary>
public class ValidacaoTests
{
    // ── helper ───────────────────────────────────────────────────────────────

    private static async Task<(int eventoId, int salaId, int staffId, int atividadeId)>
        SeedAsync(AppDbContext db, bool comAtividade = false)
    {
        const int eventoId    = 1;
        const int salaId      = 1;
        const int staffId     = 1;
        const int atividadeId = 1;

        db.Eventos.Add(new Evento
        {
            Id              = eventoId,
            Nome            = "Evento",
            OrcamentoMaximo = 500m,
            DataInicio      = DateTime.UtcNow,
            DataFim         = DateTime.UtcNow.AddDays(1),
            OrganizadorId   = 0
        });
        db.Salas.Add(new Sala  { Id = salaId,  Nome = "Sala A", Capacidade = 50, EventoId = eventoId });
        db.Staff.Add(new Staff { Id = staffId, Nome = "Ana",                     EventoId = eventoId });

        if (comAtividade)
        {
            db.Atividades.Add(new Atividade
            {
                Id         = atividadeId,
                Nome       = "Sessão",
                HoraInicio = DateTime.UtcNow,
                HoraFim    = DateTime.UtcNow.AddHours(2),
                SalaId     = salaId,
                EventoId   = eventoId
            });
        }

        await db.SaveChangesAsync();
        return (eventoId, salaId, staffId, atividadeId);
    }

    // ── 1. Atividade com HoraFim <= HoraInicio deve devolver 400 ─────────────

    [Fact]
    public async Task CriarAtividade_HoraFimAnteriorAInicio_DeveDevolver400()
    {
        using var db          = TestHelpers.CreateDb(nameof(CriarAtividade_HoraFimAnteriorAInicio_DeveDevolver400));
        var (eventoId, salaId, _, _) = await SeedAsync(db);

        var ctrl   = new AtividadesController(db).WithUser();
        var result = await ctrl.Create(new Atividade
        {
            Nome       = "Inválida",
            HoraInicio = new DateTime(2024, 6, 1, 11, 0, 0, DateTimeKind.Utc),
            HoraFim    = new DateTime(2024, 6, 1,  9, 0, 0, DateTimeKind.Utc),   // fim < início
            SalaId     = salaId,
            EventoId   = eventoId
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, bad.StatusCode);
        Assert.Contains("HoraInicio", bad.Value?.ToString());
    }

    [Fact]
    public async Task CriarAtividade_HoraFimIgualAInicio_DeveDevolver400()
    {
        using var db          = TestHelpers.CreateDb(nameof(CriarAtividade_HoraFimIgualAInicio_DeveDevolver400));
        var (eventoId, salaId, _, _) = await SeedAsync(db);

        var hora   = new DateTime(2024, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var ctrl   = new AtividadesController(db).WithUser();
        var result = await ctrl.Create(new Atividade
        {
            Nome       = "Inválida",
            HoraInicio = hora,
            HoraFim    = hora,          // igual — duração zero
            SalaId     = salaId,
            EventoId   = eventoId
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ── 2. Alocar o mesmo staff duas vezes na mesma atividade deve devolver 409

    [Fact]
    public async Task AlocarStaff_Duplicado_DeveDevolver409()
    {
        using var db = TestHelpers.CreateDb(nameof(AlocarStaff_Duplicado_DeveDevolver409));
        var (_, _, staffId, atividadeId) = await SeedAsync(db, comAtividade: true);

        // Primeira alocação (manual na seed)
        db.AlocacoesStaff.Add(new AlocacaoStaff { StaffId = staffId, AtividadeId = atividadeId });
        await db.SaveChangesAsync();

        // Segunda alocação — deve falhar com 409
        var ctrl   = new AlocacoesStaffController(db).WithUser();
        var result = await ctrl.Alocar(new AlocarRequest(staffId, atividadeId));

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task AlocarStaff_PrimeiraVez_DeveDevolver201()
    {
        using var db = TestHelpers.CreateDb(nameof(AlocarStaff_PrimeiraVez_DeveDevolver201));
        await SeedAsync(db, comAtividade: true);

        var ctrl   = new AlocacoesStaffController(db).WithUser();
        var result = await ctrl.Alocar(new AlocarRequest(1, 1));

        // CreatedAtAction devolve 201
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
    }
}
