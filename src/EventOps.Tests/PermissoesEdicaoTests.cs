using EventOps.API.Controllers;
using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace EventOps.Tests;

/// <summary>
/// Testes das permissões de edição de eventos:
/// criador pode editar; outro organizador não pode; admin não pode.
/// </summary>
public class PermissoesEdicaoTests
{
    // ── helper ───────────────────────────────────────────────────────────────

    private static async Task<int> SeedEventoAsync(AppDbContext db, int criadorId)
    {
        var evento = new Evento
        {
            Nome            = "Evento Original",
            Descricao       = "Descrição original",
            OrcamentoMaximo = 2_000m,
            DataInicio      = DateTime.UtcNow.AddDays(10),
            DataFim         = DateTime.UtcNow.AddDays(11),
            OrganizadorId   = criadorId,
            Estado          = EstadoEvento.Pendente,
        };
        db.Eventos.Add(evento);
        await db.SaveChangesAsync();
        return evento.Id;
    }

    private static Evento EventoAtualizado(int id, int criadorId) => new Evento
    {
        Id              = id,
        Nome            = "Evento Editado",
        OrcamentoMaximo = 3_000m,
        DataInicio      = DateTime.UtcNow.AddDays(15),
        DataFim         = DateTime.UtcNow.AddDays(16),
        OrganizadorId   = criadorId,
        Estado          = EstadoEvento.Pendente,
    };

    // ── 1. Criador consegue editar → 204 NoContent ────────────────────────────

    [Fact]
    public async Task EditarEvento_Criador_DeveDevolver204()
    {
        using var db = TestHelpers.CreateDb(nameof(EditarEvento_Criador_DeveDevolver204));
        var eventoId = await SeedEventoAsync(db, criadorId: 1);

        var ctrl   = new EventosController(db).WithUser("Organizador", userId: 1);
        var result = await ctrl.Update(eventoId, EventoAtualizado(eventoId, criadorId: 1));

        Assert.IsType<NoContentResult>(result);

        var salvo = await db.Eventos.FindAsync(eventoId);
        Assert.Equal("Evento Editado", salvo!.Nome);
        Assert.Equal(3_000m, salvo.OrcamentoMaximo);
    }

    // ── 2. Outro Organizador (userId diferente) não pode editar → 403 ─────────

    [Fact]
    public async Task EditarEvento_OutroOrganizador_DeveDevolver403()
    {
        using var db = TestHelpers.CreateDb(nameof(EditarEvento_OutroOrganizador_DeveDevolver403));
        var eventoId = await SeedEventoAsync(db, criadorId: 1);

        // userId=2 não é o criador (criadorId=1)
        var ctrl   = new EventosController(db).WithUser("Organizador", userId: 2);
        var result = await ctrl.Update(eventoId, EventoAtualizado(eventoId, criadorId: 1));

        Assert.IsType<ForbidResult>(result);
    }

    // ── 3. Administrador não pode editar evento de outro → 403 ────────────────

    [Fact]
    public async Task EditarEvento_Admin_DeveDevolver403()
    {
        using var db = TestHelpers.CreateDb(nameof(EditarEvento_Admin_DeveDevolver403));
        var eventoId = await SeedEventoAsync(db, criadorId: 1);

        // Admin (userId=99) não é o criador do evento
        var ctrl   = new EventosController(db).WithUser("Administrador", userId: 99);
        var result = await ctrl.Update(eventoId, EventoAtualizado(eventoId, criadorId: 1));

        Assert.IsType<ForbidResult>(result);
    }

    // ── 4. Editar evento inexistente devolve 404 ──────────────────────────────

    [Fact]
    public async Task EditarEvento_Inexistente_DeveDevolver404()
    {
        using var db = TestHelpers.CreateDb(nameof(EditarEvento_Inexistente_DeveDevolver404));

        var ctrl   = new EventosController(db).WithUser("Organizador", userId: 1);
        var result = await ctrl.Update(999, EventoAtualizado(999, criadorId: 1));

        Assert.IsType<NotFoundResult>(result);
    }

    // ── 5. Admin pode eliminar evento de outro organizador → 204 ─────────────

    [Fact]
    public async Task EliminarEvento_Admin_DeveDevolver204()
    {
        using var db = TestHelpers.CreateDb(nameof(EliminarEvento_Admin_DeveDevolver204));
        var eventoId = await SeedEventoAsync(db, criadorId: 1);

        var ctrl   = new EventosController(db).WithUser("Administrador", userId: 99);
        var result = await ctrl.Delete(eventoId);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await db.Eventos.FindAsync(eventoId));
    }

    // ── 6. Organizador elimina o seu próprio evento → 204 ────────────────────

    [Fact]
    public async Task EliminarEvento_Criador_DeveDevolver204()
    {
        using var db = TestHelpers.CreateDb(nameof(EliminarEvento_Criador_DeveDevolver204));
        var eventoId = await SeedEventoAsync(db, criadorId: 1);

        var ctrl   = new EventosController(db).WithUser("Organizador", userId: 1);
        var result = await ctrl.Delete(eventoId);

        Assert.IsType<NoContentResult>(result);
    }

    // ── 7. Organizador não pode eliminar evento de outro → 403 ────────────────

    [Fact]
    public async Task EliminarEvento_OutroOrganizador_DeveDevolver403()
    {
        using var db = TestHelpers.CreateDb(nameof(EliminarEvento_OutroOrganizador_DeveDevolver403));
        var eventoId = await SeedEventoAsync(db, criadorId: 1);

        var ctrl   = new EventosController(db).WithUser("Organizador", userId: 2);
        var result = await ctrl.Delete(eventoId);

        Assert.IsType<ForbidResult>(result);
    }
}
