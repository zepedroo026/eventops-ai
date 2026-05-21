namespace EventOps.API.DTOs;

public record RegisterRequest(string Nome, string Email, string Password, string Perfil);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, string Nome, string Email, string Perfil, DateTime Expira);

public record AtualizarPerfilRequest(string Nome, string? PasswordAtual, string? NovaPassword);

public record AtualizarPerfilResponse(string Nome, string Email, string Perfil);
