using EventOps.API.Controllers;
using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace EventOps.Tests;

/// <summary>
/// Testes da lógica de despesas: criação, totais e percentagem de orçamento.
/// </summary>
public class DespesasTests
{
    // ── helper ───────────────────────────────────────────────────────────────

    private static async Task<int> SeedEventoAsync(AppDbContext db, decimal orcamento = 1_000m)
    {
        const int eventoId = 1;
        db.Eventos.Add(new Evento
        {
            Id              = eventoId,
            Nome            = "Evento Teste",
            OrcamentoMaximo = orcamento,
            DataInicio      = DateTime.UtcNow,
            DataFim         = DateTime.UtcNow.AddDays(1),
            OrganizadorId   = 0
        });
        await db.SaveChangesAsync();
        return eventoId;
    }

    private static ResumoDto GetResumo(ActionResult<ResumoDto> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<ResumoDto>(ok.Value);
    }

    // ── 1. Total e percentagem corretos ──────────────────────────────────────

    [Fact]
    public async Task CriarDespesas_ResumoDeveDevolverTotalEPercentagemCorretos()
    {
        using var db    = TestHelpers.CreateDb(nameof(CriarDespesas_ResumoDeveDevolverTotalEPercentagemCorretos));
        var eventoId    = await SeedEventoAsync(db, orcamento: 1_000m);
        var ctrl        = new DespesasController(db).WithUser();

        // Criar duas despesas: 300 + 250 = 550
        await ctrl.Create(new Despesa { Descricao = "Aluguer AV",  Valor = 300m, EventoId = eventoId, Data = DateTime.UtcNow, Categoria = "Técnico"  });
        await ctrl.Create(new Despesa { Descricao = "Catering",    Valor = 250m, EventoId = eventoId, Data = DateTime.UtcNow, Categoria = "Catering" });

        var resumo = GetResumo(await ctrl.GetResumo(eventoId));

        Assert.Equal(550m,   resumo.TotalGasto);
        Assert.Equal(1_000m, resumo.OrcamentoMaximo);
        Assert.Equal(55m,    resumo.PercentagemUtilizada);   // 550 / 1000 * 100 = 55.00%
    }

    // ── 2. Orçamento zero — percentagem deve ser zero (sem divisão por zero) ─

    [Fact]
    public async Task Resumo_OrcamentoZero_DeveDevolver0Percentagem()
    {
        using var db = TestHelpers.CreateDb(nameof(Resumo_OrcamentoZero_DeveDevolver0Percentagem));
        var eventoId = await SeedEventoAsync(db, orcamento: 0m);

        db.Despesas.Add(new Despesa { Descricao = "Teste", Valor = 100m, EventoId = eventoId, Data = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var ctrl   = new DespesasController(db).WithUser();
        var resumo = GetResumo(await ctrl.GetResumo(eventoId));

        Assert.Equal(100m, resumo.TotalGasto);
        Assert.Equal(0m,   resumo.PercentagemUtilizada);
    }

    // ── 3. Sem despesas — total e percentagem devem ser zero ─────────────────

    [Fact]
    public async Task Resumo_SemDespesas_DeveDevolverZeros()
    {
        using var db = TestHelpers.CreateDb(nameof(Resumo_SemDespesas_DeveDevolverZeros));
        var eventoId = await SeedEventoAsync(db, orcamento: 500m);

        var ctrl   = new DespesasController(db).WithUser();
        var resumo = GetResumo(await ctrl.GetResumo(eventoId));

        Assert.Equal(0m,   resumo.TotalGasto);
        Assert.Equal(0m,   resumo.PercentagemUtilizada);
        Assert.Equal(500m, resumo.OrcamentoMaximo);
    }

    // ── 4. Percentagem acima de 100% quando orçamento é excedido ─────────────

    [Fact]
    public async Task Resumo_OrcamentoExcedido_DeveDevolvermaisde100Porcento()
    {
        using var db = TestHelpers.CreateDb(nameof(Resumo_OrcamentoExcedido_DeveDevolvermaisde100Porcento));
        var eventoId = await SeedEventoAsync(db, orcamento: 200m);

        db.Despesas.Add(new Despesa { Descricao = "Overspend", Valor = 300m, EventoId = eventoId, Data = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var ctrl   = new DespesasController(db).WithUser();
        var resumo = GetResumo(await ctrl.GetResumo(eventoId));

        Assert.Equal(300m, resumo.TotalGasto);
        Assert.True(resumo.PercentagemUtilizada > 100m);  // 150%
        Assert.Equal(150m, resumo.PercentagemUtilizada);
    }

    // ── 5. Despesa com valor negativo deve devolver 400 ──────────────────────

    [Fact]
    public async Task CriarDespesa_ValorNegativo_DeveDevolver400()
    {
        using var db = TestHelpers.CreateDb(nameof(CriarDespesa_ValorNegativo_DeveDevolver400));
        var eventoId = await SeedEventoAsync(db);

        var ctrl   = new DespesasController(db).WithUser();
        var result = await ctrl.Create(new Despesa { Descricao = "Inválida", Valor = -50m, EventoId = eventoId, Data = DateTime.UtcNow });

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, bad.StatusCode);
    }
}
