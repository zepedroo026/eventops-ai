using EventOps.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EventOps.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Utilizador>          Utilizadores       { get; set; }
    public DbSet<Evento>              Eventos            { get; set; }
    public DbSet<Sala>                Salas              { get; set; }
    public DbSet<Atividade>           Atividades         { get; set; }
    public DbSet<Staff>               Staff              { get; set; }
    public DbSet<AlocacaoStaff>       AlocacoesStaff     { get; set; }
    public DbSet<Despesa>             Despesas           { get; set; }
    public DbSet<Tarefa>              Tarefas            { get; set; }
    public DbSet<Orador>              Oradores           { get; set; }
    public DbSet<RequisitoOrador>     RequisitosOrador   { get; set; }
    public DbSet<Sponsor>             Sponsors           { get; set; }
    public DbSet<Fornecedor>          Fornecedores       { get; set; }
    public DbSet<FicheiroFornecedor>  FicheirosFornecedor { get; set; }
    public DbSet<OrcamentoCategoria>  OrcamentosCategoria { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Utilizador>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Utilizador>()
            .HasOne(u => u.FornecedorAssociado)
            .WithMany()
            .HasForeignKey(u => u.FornecedorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Evento>()
            .HasOne(e => e.Organizador)
            .WithMany(u => u.Eventos)
            .HasForeignKey(e => e.OrganizadorId);

        modelBuilder.Entity<Sala>()
            .HasOne(s => s.Evento)
            .WithMany(e => e.Salas)
            .HasForeignKey(s => s.EventoId);

        modelBuilder.Entity<Atividade>()
            .HasOne(a => a.Sala)
            .WithMany(s => s.Atividades)
            .HasForeignKey(a => a.SalaId);

        modelBuilder.Entity<Atividade>()
            .HasOne(a => a.Evento)
            .WithMany(e => e.Atividades)
            .HasForeignKey(a => a.EventoId);

        modelBuilder.Entity<Staff>()
            .HasOne(s => s.Criador)
            .WithMany()
            .HasForeignKey(s => s.CriadorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AlocacaoStaff>()
            .HasOne(a => a.Staff)
            .WithMany(s => s.Alocacoes)
            .HasForeignKey(a => a.StaffId);

        modelBuilder.Entity<AlocacaoStaff>()
            .HasOne(a => a.Atividade)
            .WithMany(a => a.Alocacoes)
            .HasForeignKey(a => a.AtividadeId);

        modelBuilder.Entity<Despesa>()
            .HasOne(d => d.Evento)
            .WithMany(e => e.Despesas)
            .HasForeignKey(d => d.EventoId);

        modelBuilder.Entity<Despesa>()
            .HasOne(d => d.Fornecedor)
            .WithMany()
            .HasForeignKey(d => d.FornecedorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Tarefa>()
            .HasOne(t => t.Evento)
            .WithMany(e => e.Tarefas)
            .HasForeignKey(t => t.EventoId);

        modelBuilder.Entity<Orador>()
            .HasOne(o => o.Evento)
            .WithMany()
            .HasForeignKey(o => o.EventoId);

        modelBuilder.Entity<RequisitoOrador>()
            .HasOne(r => r.Orador)
            .WithMany(o => o.Requisitos)
            .HasForeignKey(r => r.OradorId);

        modelBuilder.Entity<Sponsor>()
            .HasOne(s => s.Evento)
            .WithMany()
            .HasForeignKey(s => s.EventoId);

        modelBuilder.Entity<Fornecedor>()
            .HasOne(f => f.Evento)
            .WithMany()
            .HasForeignKey(f => f.EventoId);

        modelBuilder.Entity<FicheiroFornecedor>()
            .HasOne(ff => ff.Fornecedor)
            .WithMany(f => f.Ficheiros)
            .HasForeignKey(ff => ff.FornecedorId);

        modelBuilder.Entity<OrcamentoCategoria>()
            .HasOne(o => o.Evento)
            .WithMany()
            .HasForeignKey(o => o.EventoId);
    }
}
