namespace EventOps.Core.Models;

public class Tarefa
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public bool Concluida { get; set; }
    public int EventoId { get; set; }
    public Evento? Evento { get; set; }
}
