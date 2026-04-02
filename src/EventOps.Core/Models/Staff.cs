namespace EventOps.Core.Models;

public class Staff
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Funcao { get; set; }
    public string? Contacto { get; set; }
    public int EventoId { get; set; }
    public Evento? Evento { get; set; }
    public ICollection<AlocacaoStaff> Alocacoes { get; set; } = new List<AlocacaoStaff>();
}
