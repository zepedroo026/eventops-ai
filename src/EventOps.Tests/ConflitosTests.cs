using EventOps.API.Controllers;
using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace EventOps.Tests;

/// <summary>
/// Testes do algoritmo de deteção de conflitos (sala e staff).
/// </summary>
public class ConflitosTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    /// Data/hora fixa para tornar os testes determinísticos.
    private static DateTime T(int hour, int minute = 0) =>
        new DateTime(2024, 6, 1, hour, minute, 0, DateTimeKind.Utc);

    /// Semeia um evento + salas base na BD em memória e devolve os IDs.
    private static async Task<(int eventoId, int sala1Id, int sala2Id)>
        SeedBaseAsync(AppDbContext db, bool comSegundaSala = false)
    {
        const int eventoId = 1;
        const int sala1Id  = 1;
        const int sala2Id  = 2;

        db.Eventos.Add(new Evento
        {
            Id              = eventoId,
            Nome            = "Evento Teste",
            OrcamentoMaximo = 1_000m,
            DataInicio      = DateTime.UtcNow,
            DataFim         = DateTime.UtcNow.AddDays(1),
            OrganizadorId   = 0           // FK não é validada pelo InMemory
        });

        db.Salas.Add(new Sala { Id = sala1Id, Nome = "Sala A", Capacidade = 100, EventoId = eventoId });
        if (comSegundaSala)
            db.Salas.Add(new Sala { Id = sala2Id, Nome = "Sala B", Capacidade = 100, EventoId = eventoId });

        await db.SaveChangesAsync();
        return (eventoId, sala1Id, sala2Id);
    }

    /// Extrai a lista tipada de um OkObjectResult.
    private static List<T> OkList<T>(ActionResult<IEnumerable<T>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<IEnumerable<T>>(ok.Value).ToList();
    }

    // ── 1. Conflito de sala ──────────────────────────────────────────────────

    [Fact]
    public async Task SalaMesmaHora_DeveDetectarConflitoDeSala()
    {
        using var db = TestHelpers.CreateDb(nameof(SalaMesmaHora_DeveDetectarConflitoDeSala));
        var (eventoId, salaId, _) = await SeedBaseAsync(db);

        // Duas atividades na mesma sala com horários sobrepostos (10h–11h)
        db.Atividades.AddRange(
            new Atividade { Id = 1, Nome = "Keynote",  HoraInicio = T(9),  HoraFim = T(11), SalaId = salaId, EventoId = eventoId },
            new Atividade { Id = 2, Nome = "Workshop", HoraInicio = T(10), HoraFim = T(12), SalaId = salaId, EventoId = eventoId }
        );
        await db.SaveChangesAsync();

        var ctrl = new AtividadesController(db).WithUser();
        var conflitos = OkList<ConflitosDto>(await ctrl.GetConflitos(eventoId));

        Assert.Single(conflitos);
        Assert.Equal("SalaConflito", conflitos[0].Tipo);
        Assert.Contains("Keynote",  conflitos[0].Descricao);
        Assert.Contains("Workshop", conflitos[0].Descricao);
    }

    [Fact]
    public async Task AtividadesSequenciais_MesmaSala_NaoDeveDetectarConflito()
    {
        using var db = TestHelpers.CreateDb(nameof(AtividadesSequenciais_MesmaSala_NaoDeveDetectarConflito));
        var (eventoId, salaId, _) = await SeedBaseAsync(db);

        // Atividades que se tocam exatamente no limite (10h == 10h) — sem sobreposição
        db.Atividades.AddRange(
            new Atividade { Id = 1, Nome = "Manhã", HoraInicio = T(9),  HoraFim = T(10), SalaId = salaId, EventoId = eventoId },
            new Atividade { Id = 2, Nome = "Tarde", HoraInicio = T(10), HoraFim = T(11), SalaId = salaId, EventoId = eventoId }
        );
        await db.SaveChangesAsync();

        var ctrl = new AtividadesController(db).WithUser();
        var conflitos = OkList<ConflitosDto>(await ctrl.GetConflitos(eventoId));

        Assert.Empty(conflitos);
    }

    // ── 2. Conflito de staff ─────────────────────────────────────────────────

    [Fact]
    public async Task StaffNaDuasSalas_HorariosSobrepostos_DeveDetectarConflito()
    {
        using var db = TestHelpers.CreateDb(nameof(StaffNaDuasSalas_HorariosSobrepostos_DeveDetectarConflito));
        var (eventoId, sala1Id, sala2Id) = await SeedBaseAsync(db, comSegundaSala: true);

        db.Staff.Add(new Staff { Id = 1, Nome = "João", CriadorId = 0 }); // organizer 0 owns evento 1

        // Duas atividades em SALAS DIFERENTES (sem conflito de sala)
        // mas com o mesmo staff alocado e horários sobrepostos
        db.Atividades.AddRange(
            new Atividade { Id = 1, Nome = "Sessão A", HoraInicio = T(9),  HoraFim = T(11), SalaId = sala1Id, EventoId = eventoId },
            new Atividade { Id = 2, Nome = "Sessão B", HoraInicio = T(10), HoraFim = T(12), SalaId = sala2Id, EventoId = eventoId }
        );
        db.AlocacoesStaff.AddRange(
            new AlocacaoStaff { Id = 1, StaffId = 1, AtividadeId = 1 },
            new AlocacaoStaff { Id = 2, StaffId = 1, AtividadeId = 2 }
        );
        await db.SaveChangesAsync();

        var ctrl = new AtividadesController(db).WithUser();
        var conflitos = OkList<ConflitosDto>(await ctrl.GetConflitos(eventoId));

        // Apenas conflito de staff (salas diferentes)
        Assert.Single(conflitos);
        Assert.Equal("StaffConflito", conflitos[0].Tipo);
        Assert.Contains("João", conflitos[0].Descricao);
    }

    [Fact]
    public async Task StaffEmAtividadesSequenciais_NaoDeveDetectarConflito()
    {
        using var db = TestHelpers.CreateDb(nameof(StaffEmAtividadesSequenciais_NaoDeveDetectarConflito));
        var (eventoId, sala1Id, sala2Id) = await SeedBaseAsync(db, comSegundaSala: true);

        db.Staff.Add(new Staff { Id = 1, Nome = "Ana", CriadorId = 0 });

        db.Atividades.AddRange(
            new Atividade { Id = 1, Nome = "Manhã", HoraInicio = T(9),  HoraFim = T(10), SalaId = sala1Id, EventoId = eventoId },
            new Atividade { Id = 2, Nome = "Tarde", HoraInicio = T(10), HoraFim = T(11), SalaId = sala2Id, EventoId = eventoId }
        );
        db.AlocacoesStaff.AddRange(
            new AlocacaoStaff { Id = 1, StaffId = 1, AtividadeId = 1 },
            new AlocacaoStaff { Id = 2, StaffId = 1, AtividadeId = 2 }
        );
        await db.SaveChangesAsync();

        var ctrl = new AtividadesController(db).WithUser();
        var conflitos = OkList<ConflitosDto>(await ctrl.GetConflitos(eventoId));

        Assert.Empty(conflitos);
    }
}
