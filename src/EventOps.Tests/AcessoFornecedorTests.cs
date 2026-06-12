using EventOps.API.Controllers;
using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace EventOps.Tests;

/// <summary>
/// Testes para POST /api/fornecedores/{id}/criar-acesso
/// e restrição de acesso a ficheiros por utilizador Fornecedor.
/// </summary>
public class AcessoFornecedorTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private static async Task<(int eventoId, int fornecedorId)> SeedBaseAsync(AppDbContext db)
    {
        var ev = new Evento
        {
            Nome = "Evento", OrcamentoMaximo = 1_000m,
            DataInicio = DateTime.UtcNow, DataFim = DateTime.UtcNow.AddDays(1),
            OrganizadorId = 1, Estado = EstadoEvento.Aprovado,
        };
        db.Eventos.Add(ev);
        await db.SaveChangesAsync();

        var forn = new Fornecedor { Nome = "Gráfica XYZ", Email = "grafica@xyz.pt", EventoId = ev.Id };
        db.Fornecedores.Add(forn);
        await db.SaveChangesAsync();

        return (ev.Id, forn.Id);
    }

    // ── 1. Criar acesso com sucesso → 201, utilizador com Perfil.Fornecedor ──

    [Fact]
    public async Task CriarAcesso_Sucesso_DeveDevolver201ComPerfilFornecedor()
    {
        using var db = TestHelpers.CreateDb(nameof(CriarAcesso_Sucesso_DeveDevolver201ComPerfilFornecedor));
        var (_, fornId) = await SeedBaseAsync(db);

        var ctrl   = new FornecedoresController(db, null!).WithUser("Organizador", userId: 1);
        var result = await ctrl.CriarAcesso(fornId, new CriarAcessoFornecedorRequest(
            Email: "portal@grafica.pt",
            Password: "senha123",
            Nome: "Gráfica XYZ Portal"));

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, created.StatusCode);

        // Verifica que o utilizador foi criado com o perfil correto
        var u = db.Utilizadores.First(x => x.Email == "portal@grafica.pt");
        Assert.Equal(Perfil.Fornecedor, u.Perfil);
        Assert.Equal(fornId, u.FornecedorId);
        Assert.True(BCrypt.Net.BCrypt.Verify("senha123", u.PasswordHash));
    }

    // ── 2. Email duplicado → 409 Conflict ────────────────────────────────────

    [Fact]
    public async Task CriarAcesso_EmailDuplicado_DeveDevolver409()
    {
        using var db = TestHelpers.CreateDb(nameof(CriarAcesso_EmailDuplicado_DeveDevolver409));
        var (_, fornId) = await SeedBaseAsync(db);

        // Pré-seed utilizador com o mesmo email
        db.Utilizadores.Add(new Utilizador
        {
            Nome = "Existente", Email = "duplicado@email.pt",
            PasswordHash = "x", Perfil = Perfil.Organizador,
        });
        await db.SaveChangesAsync();

        var ctrl   = new FornecedoresController(db, null!).WithUser("Organizador", userId: 1);
        var result = await ctrl.CriarAcesso(fornId, new CriarAcessoFornecedorRequest(
            Email: "duplicado@email.pt", Password: "senha123", Nome: null));

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
        Assert.Contains("Email", conflict.Value?.ToString());
    }

    // ── 3. Fornecedor já tem acesso → 409 ────────────────────────────────────

    [Fact]
    public async Task CriarAcesso_FornecedorJaTemAcesso_DeveDevolver409()
    {
        using var db = TestHelpers.CreateDb(nameof(CriarAcesso_FornecedorJaTemAcesso_DeveDevolver409));
        var (_, fornId) = await SeedBaseAsync(db);

        // Pré-seed acesso existente
        db.Utilizadores.Add(new Utilizador
        {
            Nome = "Já existe", Email = "ja@existe.pt",
            PasswordHash = "x", Perfil = Perfil.Fornecedor,
            FornecedorId = fornId,
        });
        await db.SaveChangesAsync();

        var ctrl   = new FornecedoresController(db, null!).WithUser("Organizador", userId: 1);
        var result = await ctrl.CriarAcesso(fornId, new CriarAcessoFornecedorRequest(
            Email: "outro@email.pt", Password: "senha123", Nome: null));

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
        Assert.Contains("já tem acesso", conflict.Value?.ToString());
    }

    // ── 4. Password muito curta → 400 ────────────────────────────────────────

    [Fact]
    public async Task CriarAcesso_PasswordCurta_DeveDevolver400()
    {
        using var db = TestHelpers.CreateDb(nameof(CriarAcesso_PasswordCurta_DeveDevolver400));
        var (_, fornId) = await SeedBaseAsync(db);

        var ctrl   = new FornecedoresController(db, null!).WithUser("Organizador", userId: 1);
        var result = await ctrl.CriarAcesso(fornId, new CriarAcessoFornecedorRequest(
            Email: "novo@email.pt", Password: "abc", Nome: null));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── 5. Fornecedor só vê os seus próprios ficheiros → 403 para outros ─────

    [Fact]
    public async Task GetFicheiros_FornecedorOutroId_DeveDevolver403()
    {
        using var db = TestHelpers.CreateDb(nameof(GetFicheiros_FornecedorOutroId_DeveDevolver403));
        var (_, fornId) = await SeedBaseAsync(db);

        // Utilizador Fornecedor com fornecedorId = 999 (diferente de fornId)
        var ctrl   = new FornecedoresController(db, null!)
            .WithUser("Fornecedor", userId: 5, fornecedorId: 999);

        // Tenta aceder aos ficheiros do fornecedor fornId (não é o seu)
        var result = await ctrl.GetFicheiros(fornId);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // ── 6. Fornecedor vê os seus próprios ficheiros → 200 ────────────────────

    [Fact]
    public async Task GetFicheiros_FornecedorProprioId_DeveDevolver200()
    {
        using var db = TestHelpers.CreateDb(nameof(GetFicheiros_FornecedorProprioId_DeveDevolver200));
        var (_, fornId) = await SeedBaseAsync(db);

        db.FicheirosFornecedor.Add(new FicheiroFornecedor
        {
            FornecedorId = fornId, NomeOriginal = "fatura.pdf",
            Caminho = "uploads/fornecedores/1/fatura.pdf",
            Tipo = TipoFicheiro.Fatura, TamanhoBytes = 1024,
        });
        await db.SaveChangesAsync();

        // Utilizador Fornecedor cujo fornecedorId coincide com fornId
        var ctrl = new FornecedoresController(db, null!)
            .WithUser("Fornecedor", userId: 5, fornecedorId: fornId);

        var result = await ctrl.GetFicheiros(fornId);

        var ok   = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<FicheiroFornecedor>>(ok.Value).ToList();
        Assert.Single(list);
        Assert.Equal("fatura.pdf", list[0].NomeOriginal);
    }
}
