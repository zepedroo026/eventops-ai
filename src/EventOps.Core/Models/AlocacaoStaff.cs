namespace EventOps.Core.Models;

public class AlocacaoStaff
{
    public int Id { get; set; }
    public int StaffId { get; set; }
    public int AtividadeId { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public Staff? Staff { get; set; }
    public Atividade? Atividade { get; set; }
}
