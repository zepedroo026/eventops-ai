namespace EventOps.Core.Models;

public class OrcamentoCategoria
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public Evento? Evento { get; set; }
    public string Categoria { get; set; } = string.Empty;
    public decimal ValorPrevisto { get; set; }
}
