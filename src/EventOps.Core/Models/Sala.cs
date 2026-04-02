namespace EventOps.Core.Models;

public class Sala
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Capacidade { get; set; }
    public string? Localizacao { get; set; }
    public int EventoId { get; set; }
    public Evento? Evento { get; set; }
    public ICollection<Atividade> Atividades { get; set; } = new List<Atividade>();
}
