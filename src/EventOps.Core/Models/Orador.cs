namespace EventOps.Core.Models;

public enum EstadoContrato { Proposto, Contratado, Confirmado, Cancelado }
public enum TipoRequisito  { Voo, Hotel, Apresentacao, Outro }
public enum EstadoRequisito { Pendente, Tratado }

public class Orador
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public string? Bio { get; set; }
    public int EventoId { get; set; }
    public Evento? Evento { get; set; }
    public EstadoContrato EstadoContrato { get; set; } = EstadoContrato.Proposto;
    public decimal Cache { get; set; }
    public string? NotasContrato { get; set; }
    public ICollection<RequisitoOrador> Requisitos { get; set; } = new List<RequisitoOrador>();
}

public class RequisitoOrador
{
    public int Id { get; set; }
    public int OradorId { get; set; }
    public Orador? Orador { get; set; }
    public TipoRequisito Tipo { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public EstadoRequisito Estado { get; set; } = EstadoRequisito.Pendente;
    public decimal? Custo { get; set; }
}
