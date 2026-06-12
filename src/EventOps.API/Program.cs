using System.Text;
using System.Text.Json.Serialization;
using EventOps.API.Services;
using Microsoft.Extensions.Configuration;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Base de dados: SQLite em Development, PostgreSQL em Production ──────────
if (builder.Environment.IsDevelopment())
{
    var sqliteConn = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=eventops.db";
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(sqliteConn));
}
else
{
    var pgConn = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? throw new InvalidOperationException("Connection string não configurada.");

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(pgConn, npgsql =>
            npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null)));
}

// ── Serviços ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<TokenService>();

builder.Services.AddHttpClient("anthropic", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/");
    // timeout complementar definido no serviço com CancellationTokenSource
    client.Timeout = TimeSpan.FromSeconds(35);
});
builder.Services.AddScoped<IAnaliseIAService, AnaliseIAService>();

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(o => {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
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

// ── Inicializar base de dados + seed ───────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    if (app.Environment.IsDevelopment())
        db.Database.EnsureCreated(); // Cria o eventops.db a partir do model sem migrations
    else
        db.Database.Migrate();       // Aplica as migrations do PostgreSQL

    await DbSeeder.SeedAdminAsync(db, config, app.Environment.IsDevelopment());
}

// ── Pipeline HTTP ───────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
