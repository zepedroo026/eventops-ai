using EventOps.API.Controllers;
using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace EventOps.Tests;

/// <summary>
/// Testes do endpoint GET /api/eventos/{id}/orcamento-resumo:
/// cálculo de desvios, percentagens e alertas de excesso.
/// </summary>
public class OrcamentoResumoTests
{
    private static async Task<int> SeedAsync(AppDbContext db, decimal orcamento = 5_000m)
    {
        var ev = new Evento
        {
            Nome = "Evento Orçamento", OrcamentoMaximo = orcamento,
            DataInicio = DateTime.UtcNow, DataFim = DateTime.UtcNow.AddDays(1),
            OrganizadorId = 1, Estado = EstadoEvento.Aprovado,
        };
        db.Eventos.Add(ev);
        await db.SaveChangesAsync();
        return ev.Id;
    }

    private static OrcamentoResumoDto GetResumo(ActionResult<OrcamentoResumoDto> r)
    {
        var ok = Assert.IsType<OkObjectResult>(r.Result);
        return Assert.IsType<OrcamentoResumoDto>(ok.Value);
    }

    // ── 1. Desvio correto quando real excede o previsto ──────────────────────

    [Fact]
    public async Task Resumo_DesvioPositivoQuandoRealExcedePrevisto()
    {
        using var db = TestHelpers.CreateDb(nameof(Resumo_DesvioPositivoQuandoRealExcedePrevisto));
        var id = await SeedAsync(db, orcamento: 3_000m);

        db.OrcamentosCategoria.Add(new OrcamentoCategoria { EventoId = id, Categoria = "Venue",    ValorPrevisto = 1_000m });
        db.OrcamentosCategoria.Add(new OrcamentoCategoria { EventoId = id, Categoria = "Catering", ValorPrevisto = 500m  });
        db.Despesas.Add(new Despesa { EventoId = id, Categoria = "Venue",    Valor = 1_200m, Descricao = "d", Data = DateTime.UtcNow });
        db.Despesas.Add(new Despesa { EventoId = id, Categoria = "Catering", Valor = 400m,   Descricao = "d", Data = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var ctrl   = new EventosController(db).WithUser(userId: 1);
        var resumo = GetResumo(await ctrl.GetOrcamentoResumo(id));

        var venue = resumo.Linhas.First(l => l.Categoria == "Venue");
        Assert.Equal(1_000m,  venue.Previsto);
        Assert.Equal(1_200m,  venue.Real);
        Assert.Equal(200m,    venue.Desvio);
        Assert.Equal(20m,     venue.DesvioPerc);

        var catering = resumo.Linhas.First(l => l.Categoria == "Catering");
        Assert.Equal(-100m, catering.Desvio);
        Assert.Equal(-20m,  catering.DesvioPerc);

        Assert.Equal(1_500m, resumo.TotalPrevisto);
        Assert.Equal(1_600m, resumo.TotalReal);
        Assert.Equal(100m,   resumo.TotalDesvio);
        Assert.False(resumo.ExcedeOrcamento); // 1600 < 3000
    }

    // ── 2. ExcedeOrcamento = true quando real > OrcamentoMaximo ─────────────

    [Fact]
    public async Task Resumo_ExcedeOrcamento_QuandoRealMaiorQueMaximo()
    {
        using var db = TestHelpers.CreateDb(nameof(Resumo_ExcedeOrcamento_QuandoRealMaiorQueMaximo));
        var id = await SeedAsync(db, orcamento: 500m);

        db.Despesas.Add(new Despesa { EventoId = id, Valor = 600m, Descricao = "d", Data = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var ctrl   = new EventosController(db).WithUser(userId: 1);
        var resumo = GetResumo(await ctrl.GetOrcamentoResumo(id));

        Assert.Equal(600m, resumo.TotalReal);
        Assert.True(resumo.ExcedeOrcamento);
    }

    // ── 3. Categoria sem previsão tem Previsto = 0 e desvio = real ──────────

    [Fact]
    public async Task Resumo_CategoriaSemPrevisao_DesvioIgualAoReal()
    {
        using var db = TestHelpers.CreateDb(nameof(Resumo_CategoriaSemPrevisao_DesvioIgualAoReal));
        var id = await SeedAsync(db);

        db.Despesas.Add(new Despesa { EventoId = id, Categoria = "AV", Valor = 300m, Descricao = "d", Data = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var ctrl   = new EventosController(db).WithUser(userId: 1);
        var resumo = GetResumo(await ctrl.GetOrcamentoResumo(id));

        var av = resumo.Linhas.First(l => l.Categoria == "AV");
        Assert.Equal(0m,    av.Previsto);
        Assert.Equal(300m,  av.Real);
        Assert.Equal(300m,  av.Desvio);
        Assert.Equal(0m,    av.DesvioPerc); // prev=0, pct=0 por definição
    }

    // ── 4. Evento sem despesas e sem previsões devolve totais zero ───────────

    [Fact]
    public async Task Resumo_SemDadosRetornaTotaisZero()
    {
        using var db = TestHelpers.CreateDb(nameof(Resumo_SemDadosRetornaTotaisZero));
        var id = await SeedAsync(db);

        var ctrl   = new EventosController(db).WithUser(userId: 1);
        var resumo = GetResumo(await ctrl.GetOrcamentoResumo(id));

        Assert.Empty(resumo.Linhas);
        Assert.Equal(0m, resumo.TotalPrevisto);
        Assert.Equal(0m, resumo.TotalReal);
        Assert.False(resumo.ExcedeOrcamento);
    }
}
