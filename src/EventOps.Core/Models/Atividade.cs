namespace EventOps.Core.Models;

public class Atividade
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public DateTime HoraInicio { get; set; }
    public DateTime HoraFim { get; set; }
    public int SalaId { get; set; }
    public int EventoId { get; set; }
    public Sala? Sala { get; set; }
    public Evento? Evento { get; set; }
    public ICollection<AlocacaoStaff> Alocacoes { get; set; } = new List<AlocacaoStaff>();
}
