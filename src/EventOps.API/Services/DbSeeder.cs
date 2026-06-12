using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EventOps.API.Services;

public static class DbSeeder
{
    /// <summary>
    /// Garante que existe pelo menos um Utilizador com Perfil.Administrador.
    /// Idempotente: não cria duplicados se o admin já existir.
    /// Em Development usa defaults se as variáveis não estiverem configuradas.
    /// Em Production só cria se Admin:Email e Admin:Password estiverem definidos.
    /// </summary>
    public static async Task SeedAdminAsync(
        AppDbContext db, IConfiguration config, bool isDevelopment)
    {
        // Já existe pelo menos um admin — nada a fazer
        if (await db.Utilizadores.AnyAsync(u => u.Perfil == Perfil.Administrador))
            return;

        var email    = config["Admin:Email"];
        var password = config["Admin:Password"];

        if (isDevelopment)
        {
            email    ??= "admin@eventops.com";
            password ??= "Password123";
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("[Seed] Admin:Email / Admin:Password não configurados — administrador não criado.");
            return;
        }

        // Email já tomado por outra conta (não admin) — avisa mas não duplica
        if (await db.Utilizadores.AnyAsync(u => u.Email == email))
        {
            Console.WriteLine($"[Seed] Email {email} já existe — administrador não criado.");
            return;
        }

        db.Utilizadores.Add(new Utilizador
        {
            Nome         = "Administrador",
            Email        = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Perfil       = Perfil.Administrador,
        });
        await db.SaveChangesAsync();

        Console.WriteLine($"[Seed] Administrador criado: {email}");
    }
}
