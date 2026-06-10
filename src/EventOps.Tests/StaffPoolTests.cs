using EventOps.API.Controllers;
using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace EventOps.Tests;

/// <summary>
/// Testes da pool de staff por organizador:
/// POST define CriadorId a partir do JWT; GET filtra por CriadorId.
/// </summary>
public class StaffPoolTests
{
    // ── 1. POST /api/staff define CriadorId = userId do JWT ──────────────────

    [Fact]
    public async Task CriarStaff_CriadorIdDeveSerIgualAoUserId()
    {
        using var db = TestHelpers.CreateDb(nameof(CriarStaff_CriadorIdDeveSerIgualAoUserId));
        var ctrl     = new StaffController(db).WithUser("Organizador", userId: 42);

        var novoStaff = new Staff
        {
            Nome    = "Marta Silva",
            Funcao  = "Técnico de Som",
            Contacto = "marta@exemplo.com",
            CriadorId = 0, // o cliente envia 0 — o controller deve ignorar e usar o JWT
        };

        var result  = await ctrl.Create(novoStaff);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var saved   = Assert.IsType<Staff>(created.Value);

        Assert.Equal(42, saved.CriadorId);
        Assert.Equal("Marta Silva", saved.Nome);
    }

    // ── 2. POST por dois organizadores distintos — CriadorIds distintos ───────

    [Fact]
    public async Task CriarStaff_DoisOrganizadores_CriadorIdsDiferentes()
    {
        using var db  = TestHelpers.CreateDb(nameof(CriarStaff_DoisOrganizadores_CriadorIdsDiferentes));

        var ctrl1 = new StaffController(db).WithUser("Organizador", userId: 10);
        var ctrl2 = new StaffController(db).WithUser("Organizador", userId: 20);

        await ctrl1.Create(new Staff { Nome = "Staff A" });
        await ctrl2.Create(new Staff { Nome = "Staff B" });

        var staffNoBd = db.Staff.ToList();
        Assert.Equal(2, staffNoBd.Count);
        Assert.Equal(10, staffNoBd.First(s => s.Nome == "Staff A").CriadorId);
        Assert.Equal(20, staffNoBd.First(s => s.Nome == "Staff B").CriadorId);
    }

    // ── 3. GET /api/staff (Organizador) devolve apenas o seu próprio staff ────

    [Fact]
    public async Task GetStaff_Organizador_DeveDevolverApenasPoolPropria()
    {
        using var db = TestHelpers.CreateDb(nameof(GetStaff_Organizador_DeveDevolverApenasPoolPropria));

        // Seed: staff de dois organizadores diferentes
        db.Staff.AddRange(
            new Staff { Id = 1, Nome = "Alice", CriadorId = 5 },
            new Staff { Id = 2, Nome = "Bruno", CriadorId = 5 },
            new Staff { Id = 3, Nome = "Carlos", CriadorId = 9 }   // outro organizador
        );
        await db.SaveChangesAsync();

        var ctrl   = new StaffController(db).WithUser("Organizador", userId: 5);
        var result = await ctrl.GetAll();

        var ok   = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<Staff>>(ok.Value).ToList();

        Assert.Equal(2, list.Count);
        Assert.All(list, s => Assert.Equal(5, s.CriadorId));
        Assert.DoesNotContain(list, s => s.Nome == "Carlos");
    }

    // ── 4. GET /api/staff (Administrador) devolve todo o staff global ─────────

    [Fact]
    public async Task GetStaff_Admin_DeveDevolverStaffGlobal()
    {
        using var db = TestHelpers.CreateDb(nameof(GetStaff_Admin_DeveDevolverStaffGlobal));

        db.Staff.AddRange(
            new Staff { Id = 1, Nome = "Alice",  CriadorId = 5 },
            new Staff { Id = 2, Nome = "Bruno",  CriadorId = 5 },
            new Staff { Id = 3, Nome = "Carlos", CriadorId = 9 }
        );
        await db.SaveChangesAsync();

        var ctrl   = new StaffController(db).WithUser("Administrador", userId: 99);
        var result = await ctrl.GetAll();

        var ok   = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<Staff>>(ok.Value).ToList();

        Assert.Equal(3, list.Count); // admin vê todos
    }

    // ── 5. GET /api/staff (Organizador) devolve lista vazia se não tem staff ──

    [Fact]
    public async Task GetStaff_OrganizadorSemStaff_DeveDevolverListaVazia()
    {
        using var db = TestHelpers.CreateDb(nameof(GetStaff_OrganizadorSemStaff_DeveDevolverListaVazia));

        // Seed: staff de outro organizador (CriadorId = 7)
        db.Staff.Add(new Staff { Id = 1, Nome = "Outro", CriadorId = 7 });
        await db.SaveChangesAsync();

        var ctrl   = new StaffController(db).WithUser("Organizador", userId: 3);
        var result = await ctrl.GetAll();

        var ok   = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<Staff>>(ok.Value).ToList();

        Assert.Empty(list);
    }
}
