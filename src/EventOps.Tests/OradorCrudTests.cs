using EventOps.API.Controllers;
using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace EventOps.Tests;

/// <summary>
/// Testes CRUD do OradoresController e gestão de requisitos.
/// </summary>
public class OradorCrudTests
{
    private static async Task<int> SeedEventoAsync(AppDbContext db)
    {
        var ev = new Evento
        {
            Nome = "Evento", OrcamentoMaximo = 1_000m,
            DataInicio = DateTime.UtcNow, DataFim = DateTime.UtcNow.AddDays(1),
            OrganizadorId = 1, Estado = EstadoEvento.Aprovado,
        };
        db.Eventos.Add(ev);
        await db.SaveChangesAsync();
        return ev.Id;
    }

    // ── 1. Criar orador com evento válido → 201 ──────────────────────────────

    [Fact]
    public async Task CriarOrador_EventoValido_DeveDevolver201()
    {
        using var db = TestHelpers.CreateDb(nameof(CriarOrador_EventoValido_DeveDevolver201));
        var eventoId = await SeedEventoAsync(db);
        var ctrl     = new OradoresController(db).WithUser();

        var result = await ctrl.Create(new Orador
        {
            Nome = "Ana Ferreira", Email = "ana@conf.pt",
            Cache = 500m, EstadoContrato = EstadoContrato.Proposto,
            EventoId = eventoId,
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        var saved = Assert.IsType<Orador>(created.Value);
        Assert.Equal("Ana Ferreira", saved.Nome);
        Assert.Equal(500m, saved.Cache);
    }

    // ── 2. Criar orador com evento inexistente → 400 ─────────────────────────

    [Fact]
    public async Task CriarOrador_EventoInexistente_DeveDevolver400()
    {
        using var db = TestHelpers.CreateDb(nameof(CriarOrador_EventoInexistente_DeveDevolver400));
        var ctrl     = new OradoresController(db).WithUser();

        var result = await ctrl.Create(new Orador
        {
            Nome = "Pedro", Cache = 0, EventoId = 999,
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ── 3. GET devolve todos os oradores do evento ───────────────────────────

    [Fact]
    public async Task GetAll_DevolveOrадoresDoEvento()
    {
        using var db = TestHelpers.CreateDb(nameof(GetAll_DevolveOrадoresDoEvento));
        var eventoId = await SeedEventoAsync(db);

        db.Oradores.AddRange(
            new Orador { Nome = "A", Cache = 100m, EventoId = eventoId },
            new Orador { Nome = "B", Cache = 200m, EventoId = eventoId }
        );
        await db.SaveChangesAsync();

        var ctrl   = new OradoresController(db).WithUser();
        var result = await ctrl.GetAll(eventoId);

        var ok   = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<Orador>>(ok.Value).ToList();
        Assert.Equal(2, list.Count);
    }

    // ── 4. Atualizar orador → 204 e dados persistidos ────────────────────────

    [Fact]
    public async Task AtualizarOrador_DeveDevolver204EAtualizarDados()
    {
        using var db = TestHelpers.CreateDb(nameof(AtualizarOrador_DeveDevolver204EAtualizarDados));
        var eventoId = await SeedEventoAsync(db);

        var orador = new Orador { Nome = "Carlos", Cache = 300m, EventoId = eventoId };
        db.Oradores.Add(orador);
        await db.SaveChangesAsync();

        var ctrl   = new OradoresController(db).WithUser();
        var result = await ctrl.Update(orador.Id, new Orador
        {
            Id = orador.Id, Nome = "Carlos Atualizado", Cache = 450m,
            EstadoContrato = EstadoContrato.Confirmado, EventoId = eventoId,
        });

        Assert.IsType<NoContentResult>(result);
        var atualizado = await db.Oradores.FindAsync(orador.Id);
        Assert.Equal("Carlos Atualizado", atualizado!.Nome);
        Assert.Equal(450m, atualizado.Cache);
        Assert.Equal(EstadoContrato.Confirmado, atualizado.EstadoContrato);
    }

    // ── 5. Eliminar orador → 204 e orador removido ───────────────────────────

    [Fact]
    public async Task EliminarOrador_DeveDevolver204ERemover()
    {
        using var db = TestHelpers.CreateDb(nameof(EliminarOrador_DeveDevolver204ERemover));
        var eventoId = await SeedEventoAsync(db);

        var orador = new Orador { Nome = "Diana", Cache = 0m, EventoId = eventoId };
        db.Oradores.Add(orador);
        await db.SaveChangesAsync();

        var ctrl   = new OradoresController(db).WithUser();
        var result = await ctrl.Delete(orador.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await db.Oradores.FindAsync(orador.Id));
    }

    // ── 6. Eliminar orador inexistente → 404 ─────────────────────────────────

    [Fact]
    public async Task EliminarOradorInexistente_DeveDevolver404()
    {
        using var db = TestHelpers.CreateDb(nameof(EliminarOradorInexistente_DeveDevolver404));
        var ctrl     = new OradoresController(db).WithUser();
        Assert.IsType<NotFoundResult>(await ctrl.Delete(999));
    }

    // ── 7. Adicionar requisito ao orador → Created ───────────────────────────

    [Fact]
    public async Task AdicionarRequisito_OradorValido_DeveGuardar()
    {
        using var db = TestHelpers.CreateDb(nameof(AdicionarRequisito_OradorValido_DeveGuardar));
        var eventoId = await SeedEventoAsync(db);

        var orador = new Orador { Nome = "Eva", Cache = 0m, EventoId = eventoId };
        db.Oradores.Add(orador);
        await db.SaveChangesAsync();

        var ctrl   = new OradoresController(db).WithUser();
        var result = await ctrl.AddRequisito(orador.Id, new RequisitoOrador
        {
            Tipo = TipoRequisito.Hotel, Descricao = "Hotel 4 estrelas",
            Estado = EstadoRequisito.Pendente, Custo = 200m,
        });

        Assert.IsType<CreatedResult>(result.Result);
        var req = db.RequisitosOrador.FirstOrDefault(r => r.OradorId == orador.Id);
        Assert.NotNull(req);
        Assert.Equal(TipoRequisito.Hotel, req.Tipo);
        Assert.Equal(200m, req.Custo);
    }

    // ── 8. Requisitos pendentes por evento ───────────────────────────────────

    [Fact]
    public async Task GetRequisitosPendentes_DeveDevolverApenasPendentes()
    {
        using var db = TestHelpers.CreateDb(nameof(GetRequisitosPendentes_DeveDevolverApenasPendentes));
        var eventoId = await SeedEventoAsync(db);

        var orador = new Orador { Nome = "Fábio", Cache = 0m, EventoId = eventoId };
        db.Oradores.Add(orador);
        await db.SaveChangesAsync();

        db.RequisitosOrador.AddRange(
            new RequisitoOrador { OradorId = orador.Id, Tipo = TipoRequisito.Voo,   Descricao = "Voo", Estado = EstadoRequisito.Pendente },
            new RequisitoOrador { OradorId = orador.Id, Tipo = TipoRequisito.Hotel, Descricao = "H",   Estado = EstadoRequisito.Tratado  }
        );
        await db.SaveChangesAsync();

        var ctrl   = new OradoresController(db).WithUser();
        var result = await ctrl.GetRequisitosPendentes(eventoId);

        var ok   = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value).ToList();
        Assert.Single(list); // só o Pendente
    }
}
