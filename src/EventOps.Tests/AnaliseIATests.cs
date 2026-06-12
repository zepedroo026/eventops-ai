using EventOps.API.Services;

namespace EventOps.Tests;

public class AnaliseIATests
{
    // ── ParseResposta ────────────────────────────────────────────────────────

    [Fact]
    public void ParseResposta_RespostaValida_RetornaResultado()
    {
        var payload = """
            {
              "content": [{ "type": "text", "text": "{\"resumo\":\"Tudo bem.\",\"sugestoes\":[{\"conflitoIndex\":0,\"sugestao\":\"Muda a sala.\"}],\"riscos\":[\"Risco A\"]}" }],
              "model": "claude-haiku-4-5-20251001"
            }
            """;

        var result = AnaliseIAService.ParseResposta(payload);

        Assert.NotNull(result);
        Assert.Equal("Tudo bem.", result.Resumo);
        Assert.Single(result.Sugestoes);
        Assert.Equal(0, result.Sugestoes.First().ConflitoIndex);
        Assert.Equal("Muda a sala.", result.Sugestoes.First().Sugestao);
        Assert.Single(result.Riscos);
        Assert.Equal("Risco A", result.Riscos.First());
    }

    [Fact]
    public void ParseResposta_JsonEnvolvidoEmMarkdown_RetornaResultado()
    {
        // Serializa para garantir que as aspas do innerJson ficam corretamente escapadas no payload
        var innerJson = "{\"resumo\":\"OK.\",\"sugestoes\":[],\"riscos\":[\"Sem pessoal\"]}";
        var textContent = "```json\n" + innerJson + "\n```";
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text = textContent } }
        });

        var result = AnaliseIAService.ParseResposta(payload);

        Assert.NotNull(result);
        Assert.Equal("OK.", result.Resumo);
        Assert.Empty(result.Sugestoes);
        Assert.Single(result.Riscos);
    }

    [Fact]
    public void ParseResposta_JsonMalformado_RetornaNull()
    {
        var payload = """
            {
              "content": [{ "type": "text", "text": "Desculpe, não consigo responder agora." }]
            }
            """;

        var result = AnaliseIAService.ParseResposta(payload);

        Assert.Null(result);
    }

    [Fact]
    public void ParseResposta_RespostaVazia_RetornaNull()
    {
        var result = AnaliseIAService.ParseResposta(string.Empty);
        Assert.Null(result);
    }

    [Fact]
    public void ParseResposta_ContentAusente_RetornaNull()
    {
        var payload = """{ "model": "claude-haiku-4-5-20251001", "stop_reason": "end_turn" }""";

        var result = AnaliseIAService.ParseResposta(payload);

        Assert.Null(result);
    }

    [Fact]
    public void ParseResposta_TextoVazioNoContent_RetornaNull()
    {
        var payload = """{ "content": [{ "type": "text", "text": "" }] }""";

        var result = AnaliseIAService.ParseResposta(payload);

        Assert.Null(result);
    }

    [Fact]
    public void ParseResposta_SemSugestoesNemRiscos_RetornaResultadoComListasVazias()
    {
        var payload = """
            {
              "content": [{ "type": "text", "text": "{\"resumo\":\"Sem problemas.\",\"sugestoes\":[],\"riscos\":[]}" }]
            }
            """;

        var result = AnaliseIAService.ParseResposta(payload);

        Assert.NotNull(result);
        Assert.Equal("Sem problemas.", result.Resumo);
        Assert.Empty(result.Sugestoes);
        Assert.Empty(result.Riscos);
    }

    // ── AnalisarCronogramaAsync: sem API key não chama HTTP ──────────────────

    [Fact]
    public async Task AnalisarCronogramaAsync_SemApiKey_RetornaNull()
    {
        var config     = TestHelpers.BuildConfig(new() { ["Anthropic:ApiKey"] = "" });
        var httpClient = new HttpClient(new NeverCalledHandler());
        var factory    = TestHelpers.BuildHttpClientFactory("anthropic", httpClient);
        var service    = new AnaliseIAService(factory, config);

        var result = await service.AnalisarCronogramaAsync("contexto qualquer");

        Assert.Null(result);
    }

    // ── AnalisarCronogramaAsync: erro HTTP devolve null (não lança) ──────────

    [Fact]
    public async Task AnalisarCronogramaAsync_ErroHttp_RetornaNull()
    {
        var config = TestHelpers.BuildConfig(new() { ["Anthropic:ApiKey"] = "sk-test-key" });
        var handler = new FixedStatusHandler(System.Net.HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/") };
        var factory = TestHelpers.BuildHttpClientFactory("anthropic", httpClient);
        var service = new AnaliseIAService(factory, config);

        var result = await service.AnalisarCronogramaAsync("contexto");

        Assert.Null(result);
    }

    // ── AnalisarCronogramaAsync: resposta bem formada é devolvida ────────────

    [Fact]
    public async Task AnalisarCronogramaAsync_RespostaValida_RetornaAnalise()
    {
        const string anthropicResponse = """
            {
              "content": [{ "type": "text", "text": "{\"resumo\":\"Cronograma OK.\",\"sugestoes\":[],\"riscos\":[]}" }],
              "model": "claude-haiku-4-5-20251001",
              "stop_reason": "end_turn"
            }
            """;

        var config = TestHelpers.BuildConfig(new() { ["Anthropic:ApiKey"] = "sk-test-key" });
        var handler = new FixedBodyHandler(anthropicResponse);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/") };
        var factory = TestHelpers.BuildHttpClientFactory("anthropic", httpClient);
        var service = new AnaliseIAService(factory, config);

        var result = await service.AnalisarCronogramaAsync("contexto");

        Assert.NotNull(result);
        Assert.Equal("Cronograma OK.", result.Resumo);
    }
}

/* ── HttpMessageHandler helpers ─────────────────────────────────────────── */

file sealed class NeverCalledHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        => throw new InvalidOperationException("O HttpClient não deve ser chamado sem API key.");
}

file sealed class FixedStatusHandler(System.Net.HttpStatusCode status) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("error") });
}

file sealed class FixedBodyHandler(string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        });
}
