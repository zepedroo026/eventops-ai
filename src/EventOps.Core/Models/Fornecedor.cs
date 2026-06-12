namespace EventOps.Core.Models;

public enum TipoFicheiro { Fatura, MaterialGrafico, Outro }

public class Fornecedor
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? NIF { get; set; }
    public string? Categoria { get; set; }
    public int EventoId { get; set; }
    public Evento? Evento { get; set; }
    public ICollection<FicheiroFornecedor> Ficheiros { get; set; } = new List<FicheiroFornecedor>();
}

public class FicheiroFornecedor
{
    public int Id { get; set; }
    public int FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }
    public string NomeOriginal { get; set; } = string.Empty;
    public string Caminho { get; set; } = string.Empty;
    public TipoFicheiro Tipo { get; set; }
    public DateTime DataUpload { get; set; } = DateTime.UtcNow;
    public long TamanhoBytes { get; set; }
}
