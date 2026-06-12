using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EventOps.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FornecedoresController(AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private static readonly string[] AllowedExts = [".pdf", ".png", ".jpg", ".jpeg", ".svg"];
    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB

    // GET /api/fornecedores?eventoId=X  — inclui temAcesso e emailAcesso
    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] int eventoId)
    {
        var fornecedores = await db.Fornecedores
            .AsNoTracking()
            .Where(f => f.EventoId == eventoId)
            .Include(f => f.Ficheiros)
            .OrderBy(f => f.Nome)
            .ToListAsync();

        var acessos = await db.Utilizadores
            .AsNoTracking()
            .Where(u => u.FornecedorId.HasValue)
            .Select(u => new { u.FornecedorId, u.Email })
            .ToListAsync();

        var result = fornecedores.Select(f => new
        {
            f.Id, f.Nome, f.Email, f.NIF, f.Categoria, f.EventoId, f.Ficheiros,
            TemAcesso   = acessos.Any(a => a.FornecedorId == f.Id),
            EmailAcesso = acessos.FirstOrDefault(a => a.FornecedorId == f.Id)?.Email,
        });

        return Ok(result);
    }

    // GET /api/fornecedores/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Fornecedor>> GetById(int id)
    {
        var f = await db.Fornecedores
            .AsNoTracking()
            .Include(f => f.Ficheiros)
            .FirstOrDefaultAsync(f => f.Id == id);
        if (f is null) return NotFound();
        return Ok(f);
    }

    // POST /api/fornecedores
    [HttpPost]
    public async Task<ActionResult<Fornecedor>> Create(Fornecedor fornecedor)
    {
        if (!await db.Eventos.AnyAsync(e => e.Id == fornecedor.EventoId))
            return BadRequest($"Evento com id {fornecedor.EventoId} não existe.");
        db.Fornecedores.Add(fornecedor);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = fornecedor.Id }, fornecedor);
    }

    // PUT /api/fornecedores/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Fornecedor fornecedor)
    {
        if (id != fornecedor.Id) return BadRequest("O id do URL não corresponde ao do body.");
        var existing = await db.Fornecedores.FindAsync(id);
        if (existing is null) return NotFound();
        existing.Nome      = fornecedor.Nome;
        existing.Email     = fornecedor.Email;
        existing.NIF       = fornecedor.NIF;
        existing.Categoria = fornecedor.Categoria;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/fornecedores/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var fornecedor = await db.Fornecedores.FindAsync(id);
        if (fornecedor is null) return NotFound();
        db.Fornecedores.Remove(fornecedor);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Upload de ficheiros ─────────────────────────────────────────────────

    // POST /api/fornecedores/{id}/ficheiros  (multipart/form-data)
    [HttpPost("{id:int}/ficheiros")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<FicheiroFornecedor>> UploadFicheiro(
        int id,
        IFormFile ficheiro,
        [FromForm] TipoFicheiro tipo = TipoFicheiro.Outro)
    {
        if (!await db.Fornecedores.AnyAsync(f => f.Id == id))
            return NotFound("Fornecedor não encontrado.");

        var ext = Path.GetExtension(ficheiro.FileName).ToLowerInvariant();
        if (!AllowedExts.Contains(ext))
            return BadRequest($"Extensão não permitida. Aceites: {string.Join(", ", AllowedExts)}");

        if (ficheiro.Length > MaxBytes)
            return BadRequest("O ficheiro não pode ter mais de 10 MB.");

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var dir     = Path.Combine(webRoot, "uploads", "fornecedores", id.ToString());
        Directory.CreateDirectory(dir);

        var uniqueName = $"{Guid.NewGuid():N}{ext}";
        var fullPath   = Path.Combine(dir, uniqueName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
            await ficheiro.CopyToAsync(stream);

        var ff = new FicheiroFornecedor
        {
            FornecedorId  = id,
            NomeOriginal  = ficheiro.FileName,
            Caminho       = Path.Combine("uploads", "fornecedores", id.ToString(), uniqueName)
                                .Replace('\\', '/'),
            Tipo          = tipo,
            TamanhoBytes  = ficheiro.Length,
        };
        db.FicheirosFornecedor.Add(ff);
        await db.SaveChangesAsync();
        return Created($"/api/fornecedores/ficheiros/{ff.Id}/download", ff);
    }

    // GET /api/fornecedores/ficheiros/{id}/download
    [HttpGet("ficheiros/{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var ff = await db.FicheirosFornecedor.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (ff is null) return NotFound();

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var path    = Path.Combine(webRoot, ff.Caminho.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(path)) return NotFound("Ficheiro não encontrado no servidor.");

        var contentType = Path.GetExtension(ff.NomeOriginal).ToLowerInvariant() switch
        {
            ".pdf"  => "application/pdf",
            ".png"  => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg"  => "image/svg+xml",
            _       => "application/octet-stream",
        };
        return PhysicalFile(path, contentType, ff.NomeOriginal);
    }

    // DELETE /api/fornecedores/ficheiros/{id}
    [HttpDelete("ficheiros/{id:int}")]
    public async Task<IActionResult> DeleteFicheiro(int id)
    {
        var ff = await db.FicheirosFornecedor.FindAsync(id);
        if (ff is null) return NotFound();

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var path    = Path.Combine(webRoot, ff.Caminho.Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);

        db.FicheirosFornecedor.Remove(ff);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/fornecedores/ficheiros?fornecedorId=X  (para portal do fornecedor)
    [HttpGet("ficheiros")]
    public async Task<ActionResult<IEnumerable<FicheiroFornecedor>>> GetFicheiros(
        [FromQuery] int fornecedorId)
    {
        // Utilizador com role Fornecedor só pode ver os seus próprios ficheiros
        if (User.IsInRole("Fornecedor"))
        {
            var claimFornId = User.FindFirstValue("fornecedorId");
            if (!int.TryParse(claimFornId, out var myId) || myId != fornecedorId)
                return Forbid();
        }

        return Ok(await db.FicheirosFornecedor
            .AsNoTracking()
            .Where(ff => ff.FornecedorId == fornecedorId)
            .OrderByDescending(ff => ff.DataUpload)
            .ToListAsync());
    }

    // POST /api/fornecedores/{id}/criar-acesso  — cria conta Fornecedor para o portal
    [HttpPost("{id:int}/criar-acesso")]
    [Authorize(Roles = "Organizador,Administrador")]
    public async Task<ActionResult> CriarAcesso(int id, CriarAcessoFornecedorRequest req)
    {
        if (!await db.Fornecedores.AnyAsync(f => f.Id == id))
            return NotFound("Fornecedor não encontrado.");

        if (await db.Utilizadores.AnyAsync(u => u.FornecedorId == id))
            return Conflict("Este fornecedor já tem acesso ao portal.");

        if (await db.Utilizadores.AnyAsync(u => u.Email == req.Email))
            return Conflict("Email já está registado.");

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return BadRequest("A password deve ter pelo menos 6 caracteres.");

        var fornecedor  = await db.Fornecedores.AsNoTracking().FirstAsync(f => f.Id == id);
        var utilizador  = new Utilizador
        {
            Nome          = req.Nome ?? fornecedor.Nome,
            Email         = req.Email,
            PasswordHash  = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Perfil        = Perfil.Fornecedor,
            FornecedorId  = id,
        };
        db.Utilizadores.Add(utilizador);
        await db.SaveChangesAsync();

        return Created(string.Empty, new
        {
            utilizador.Id,
            utilizador.Email,
            utilizador.Nome,
            Perfil = utilizador.Perfil.ToString(),
            utilizador.FornecedorId,
        });
    }
}

public record CriarAcessoFornecedorRequest(string Email, string Password, string? Nome);
