namespace EventOps.Core.Models;

public enum NivelSponsor { Ouro, Prata, Bronze }

public class Sponsor
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Empresa { get; set; }
    public string? Email { get; set; }
    public NivelSponsor Nivel { get; set; } = NivelSponsor.Bronze;
    public decimal ValorPatrocinio { get; set; }
    public EstadoContrato EstadoContrato { get; set; } = EstadoContrato.Proposto;
    public int EventoId { get; set; }
    public Evento? Evento { get; set; }
}
