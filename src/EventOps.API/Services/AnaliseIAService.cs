using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace EventOps.API.Services;

public sealed class AnaliseIAService(IHttpClientFactory httpFactory, IConfiguration config) : IAnaliseIAService
{
    private const string ModelId      = "claude-haiku-4-5-20251001";
    private const string SystemPrompt =
        """
        És um assistente especializado em gestão de eventos e logística. Analisa cronogramas de eventos.

        Responde SEMPRE com JSON válido neste formato exacto, sem texto fora do JSON:
        {
          "resumo": "2-3 frases de resumo executivo do estado do cronograma",
          "sugestoes": [
            {"conflitoIndex": 0, "sugestao": "sugestão concreta de resolução para o conflito 0"}
          ],
          "riscos": ["risco operacional não óbvio 1", "risco 2"]
        }

        Usa português europeu (de Portugal). Sê directo e prático.
        """;

    public async Task<AnaliseIAResultado?> AnalisarCronogramaAsync(string contexto, CancellationToken ct = default)
    {
        var apiKey = config["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        try
        {
            var client = httpFactory.CreateClient("anthropic");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var body = JsonSerializer.Serialize(new
            {
                model      = ModelId,
                max_tokens = 1024,
                system     = SystemPrompt,
                messages   = new[] { new { role = "user", content = contexto } }
            });

            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var response = await client.PostAsync(
                "v1/messages",
                new StringContent(body, Encoding.UTF8, "application/json"),
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                Console.WriteLine($"[IA] Anthropic API error {response.StatusCode}: {err[..Math.Min(200, err.Length)]}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseResposta(json);
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or TaskCanceledException)
        {
            Console.WriteLine($"[IA] Chamada ao LLM falhou: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extrai e deserializa a análise do payload de resposta da Anthropic.
    /// Exposto como internal para permitir testes unitários.
    /// </summary>
    internal static AnaliseIAResultado? ParseResposta(string anthropicJson)
    {
        try
        {
            var root    = JsonNode.Parse(anthropicJson);
            var content = root?["content"]?[0]?["text"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(content)) return null;

            // O LLM pode envolver o JSON em ```json … ```
            var text = content.Trim();
            if (text.StartsWith("```"))
            {
                var start = text.IndexOf('\n') + 1;
                var end   = text.LastIndexOf("```");
                text = end > start ? text[start..end].Trim() : text;
            }

            var parsed = JsonSerializer.Deserialize<AnaliseIAJsonDto>(text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed is null) return null;

            return new AnaliseIAResultado(
                parsed.Resumo ?? string.Empty,
                parsed.Sugestoes?.Select(s => new SugestaoConflito(s.ConflitoIndex, s.Sugestao ?? string.Empty)) ?? [],
                parsed.Riscos ?? []);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IA] Falha ao fazer parse da resposta: {ex.Message}");
            return null;
        }
    }

    // DTOs privados para deserialização do JSON do LLM
    private sealed record AnaliseIAJsonDto(
        string? Resumo,
        List<SugestaoJsonDto>? Sugestoes,
        List<string>? Riscos);

    private sealed record SugestaoJsonDto(int ConflitoIndex, string? Sugestao);
}
