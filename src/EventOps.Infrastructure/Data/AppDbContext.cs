using EventOps.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EventOps.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Utilizador> Utilizadores { get; set; }
    public DbSet<Evento> Eventos { get; set; }
    public DbSet<Sala> Salas { get; set; }
    public DbSet<Atividade> Atividades { get; set; }
    public DbSet<Staff> Staff { get; set; }
    public DbSet<AlocacaoStaff> AlocacoesStaff { get; set; }
    public DbSet<Despesa> Despesas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Utilizador>()
            .HasIndex(u => u.Email)
            .IsUnique();

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
            .HasOne(s => s.Evento)
            .WithMany(e => e.Staff)
            .HasForeignKey(s => s.EventoId);

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
    }
}
