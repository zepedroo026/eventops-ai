using EventOps.API.Controllers;
using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventOps.Tests;

/// <summary>
/// Testes do ciclo de aprovação de eventos:
/// criação fica Pendente; Admin aprova/rejeita; Organizador não pode aprovar.
/// </summary>
public class EventoAprovacaoTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private static async Task<int> SeedEventoPendenteAsync(AppDbContext db, int organizadorId = 1)
    {
        var evento = new Evento
        {
            Nome            = "Conferência Tech",
            OrcamentoMaximo = 5_000m,
            DataInicio      = DateTime.UtcNow.AddDays(30),
            DataFim         = DateTime.UtcNow.AddDays(32),
            OrganizadorId   = organizadorId,
            Estado          = EstadoEvento.Pendente,
        };
        db.Eventos.Add(evento);
        await db.SaveChangesAsync();
        return evento.Id;
    }

    // ── 1. Evento criado fica com estado Pendente ────────────────────────────

    [Fact]
    public async Task CriarEvento_Organizador_EstadoDeveSerPendente()
    {
        using var db    = TestHelpers.CreateDb(nameof(CriarEvento_Organizador_EstadoDeveSerPendente));
        var ctrl        = new EventosController(db).WithUser("Organizador", userId: 1);

        var novoEvento = new Evento
        {
            Nome            = "Novo Evento",
            OrcamentoMaximo = 1_000m,
            DataInicio      = DateTime.UtcNow.AddDays(10),
            DataFim         = DateTime.UtcNow.AddDays(11),
            OrganizadorId   = 1,
            Estado          = EstadoEvento.Aprovado, // o cliente tenta forçar Aprovado
        };

        var result  = await ctrl.Create(novoEvento);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var saved   = Assert.IsType<Evento>(created.Value);

        // O controller deve ignorar o estado enviado e fixar Pendente
        Assert.Equal(EstadoEvento.Pendente, saved.Estado);
    }

    // ── 2. Admin aprova → estado muda para Aprovado ──────────────────────────

    [Fact]
    public async Task Admin_AprovarEvento_EstadoDeveFicarAprovado()
    {
        using var db   = TestHelpers.CreateDb(nameof(Admin_AprovarEvento_EstadoDeveFicarAprovado));
        var eventoId   = await SeedEventoPendenteAsync(db);
        var ctrl       = new AdminController(db).WithUser("Administrador");

        var result = await ctrl.AprovarEvento(eventoId);

        Assert.IsType<NoContentResult>(result);

        var eventoAtualizado = await db.Eventos.FindAsync(eventoId);
        Assert.Equal(EstadoEvento.Aprovado, eventoAtualizado!.Estado);
    }

    // ── 3. Admin rejeita → estado muda para Rejeitado ────────────────────────

    [Fact]
    public async Task Admin_RejeitarEvento_EstadoDeveFicarRejeitado()
    {
        using var db   = TestHelpers.CreateDb(nameof(Admin_RejeitarEvento_EstadoDeveFicarRejeitado));
        var eventoId   = await SeedEventoPendenteAsync(db);
        var ctrl       = new AdminController(db).WithUser("Administrador");

        var result = await ctrl.RejeitarEvento(eventoId);

        Assert.IsType<NoContentResult>(result);

        var eventoAtualizado = await db.Eventos.FindAsync(eventoId);
        Assert.Equal(EstadoEvento.Rejeitado, eventoAtualizado!.Estado);
    }

    // ── 4. Organizador não pode aprovar: AdminController exige role Admin ─────

    [Fact]
    public void AdminController_AprovarERejeitar_RequeremRoleAdministrador()
    {
        // Verifica que AdminController tem [Authorize(Roles = "Administrador")] a nível de classe.
        // Em produção, o middleware ASP.NET Core bloqueia automaticamente qualquer role diferente.
        var authorizeAttr = typeof(AdminController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorizeAttr);
        Assert.Equal("Administrador", authorizeAttr.Roles);
    }

    // ── 5. Aprovar evento inexistente devolve 404 ─────────────────────────────

    [Fact]
    public async Task Admin_AprovarEventoInexistente_DeveDevolver404()
    {
        using var db = TestHelpers.CreateDb(nameof(Admin_AprovarEventoInexistente_DeveDevolver404));
        var ctrl     = new AdminController(db).WithUser("Administrador");

        var result = await ctrl.AprovarEvento(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
