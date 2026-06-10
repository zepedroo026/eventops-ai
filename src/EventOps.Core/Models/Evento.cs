namespace EventOps.Core.Models;

public enum EstadoEvento { Pendente, Aprovado, Rejeitado }

public class Evento
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public DateTime DataInicio { get; set; }
    public DateTime DataFim { get; set; }
    public string? Localizacao { get; set; }
    public decimal OrcamentoMaximo { get; set; }
    public int OrganizadorId { get; set; }
    public Utilizador? Organizador { get; set; }
    public string? Notas { get; set; }
    public EstadoEvento Estado { get; set; } = EstadoEvento.Pendente;
    public ICollection<Sala> Salas { get; set; } = new List<Sala>();
    public ICollection<Atividade> Atividades { get; set; } = new List<Atividade>();
    public ICollection<Despesa> Despesas { get; set; } = new List<Despesa>();
    public ICollection<Tarefa> Tarefas { get; set; } = new List<Tarefa>();
}
