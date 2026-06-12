namespace EventOps.Core.Models;

public enum Perfil { Administrador, Organizador, Staff, Fornecedor }

public class Utilizador
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Perfil Perfil { get; set; }
    public bool Bloqueado { get; set; } = false;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public int? FornecedorId { get; set; }
    public Fornecedor? FornecedorAssociado { get; set; }
    public ICollection<Evento> Eventos { get; set; } = new List<Evento>();
}
