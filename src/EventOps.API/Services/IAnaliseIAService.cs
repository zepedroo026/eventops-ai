namespace EventOps.API.Services;

public record SugestaoConflito(int ConflitoIndex, string Sugestao);

public record AnaliseIAResultado(
    string Resumo,
    IEnumerable<SugestaoConflito> Sugestoes,
    IEnumerable<string> Riscos);

public interface IAnaliseIAService
{
    /// <summary>
    /// Envia o contexto do cronograma ao LLM e devolve a análise estruturada.
    /// Devolve null se a API key não estiver configurada, se houver timeout ou erro de rede.
    /// </summary>
    Task<AnaliseIAResultado?> AnalisarCronogramaAsync(string contexto, CancellationToken ct = default);
}
