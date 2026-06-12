using System.Text;
using System.Text.Json.Serialization;
using EventOps.API.Services;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Porta dinâmica para o Render (injeta PORT) ───────────────────────────────
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://+:{port}");

// ── Base de dados ────────────────────────────────────────────────────────────
if (builder.Environment.IsDevelopment())
{
    var sqliteConn = builder.Configuration.GetConnectionString("DefaultConnection")
                     ?? "Data Source=eventops.db";
    builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(sqliteConn));
}
else
{
    var pgConn = builder.Configuration.GetConnectionString("Supabase")
        ?? throw new InvalidOperationException(
            "Connection string 'Supabase' não está configurada. " +
            "Define a variável de ambiente ConnectionStrings__Supabase no Render.");

    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseNpgsql(pgConn, npgsql =>
            npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null)));
}

// ── CORS ─────────────────────────────────────────────────────────────────────
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    }));

// ── Serviços ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<TokenService>();

builder.Services.AddHttpClient("anthropic", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/");
    client.Timeout = TimeSpan.FromSeconds(35);
});
builder.Services.AddScoped<IAnaliseIAService, AnaliseIAService>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key não está configurada.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT",
        In          = ParameterLocation.Header,
        Description = "Insere o token JWT obtido no login. Exemplo: Bearer eyJhbG..."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ── Inicializar base de dados + seed ──────────────────────────────────────────
//
// Dois providers, duas estratégias:
//
//   Development (SQLite) → EnsureCreatedAsync
//     A BD local é descartável; cria o schema directamente a partir do modelo
//     sem precisar de migrations. O EF não cria __EFMigrationsHistory, por isso
//     Migrate() falharia com PendingModelChangesWarning quando o snapshot é Npgsql.
//
//   Production (Npgsql/Supabase) → MigrateAsync
//     Aplica as migrations Npgsql geradas pelo dotnet-ef (via AppDbContextDesignTimeFactory).
//     Cria __EFMigrationsHistory e aplica todas as migrations pendentes na ordem certa.
//
// O dotnet-ef usa sempre Npgsql (AppDbContextDesignTimeFactory) para gerar migrations,
// independentemente do ambiente em que o programador está a correr a API.
//
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    if (app.Environment.IsDevelopment())
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();

    await DbSeeder.SeedAdminAsync(db, config, app.Environment.IsDevelopment());
}

// ── Pipeline HTTP ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
