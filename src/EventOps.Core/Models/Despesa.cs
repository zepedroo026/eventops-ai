namespace EventOps.Core.Models;

public enum EstadoDespesa { Pendente, Aprovada, Paga }

public class Despesa
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    public DateTime Data { get; set; }
    public EstadoDespesa Estado { get; set; } = EstadoDespesa.Pendente;
    public int EventoId { get; set; }
    public Evento? Evento { get; set; }
    public int? FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }
}
