namespace EventOps.Core.Models;

public class Despesa
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    public DateTime Data { get; set; }
    public int EventoId { get; set; }
    public Evento? Evento { get; set; }
}
