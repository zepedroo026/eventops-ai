using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventOps.API.Controllers;

public record AlocarRequest(int StaffId, int AtividadeId);

[Authorize]
[ApiController]
[Route("api/alocacoes")]
public class AlocacoesStaffController(AppDbContext db) : ControllerBase
{
    // POST /api/alocacoes
    [HttpPost]
    public async Task<ActionResult<AlocacaoStaff>> Alocar(AlocarRequest req)
    {
        var staff = await db.Staff.FindAsync(req.StaffId);
        if (staff is null)
            return BadRequest($"Staff com id {req.StaffId} não existe.");

        var atividade = await db.Atividades.FindAsync(req.AtividadeId);
        if (atividade is null)
            return BadRequest($"Atividade com id {req.AtividadeId} não existe.");

        if (staff.EventoId != atividade.EventoId)
            return BadRequest("O staff e a atividade pertencem a eventos diferentes.");

        var jaExiste = await db.AlocacoesStaff
            .AnyAsync(a => a.StaffId == req.StaffId && a.AtividadeId == req.AtividadeId);
        if (jaExiste)
            return Conflict("Este membro de staff já está alocado a esta atividade.");

        var alocacao = new AlocacaoStaff
        {
            StaffId     = req.StaffId,
            AtividadeId = req.AtividadeId,
        };

        db.AlocacoesStaff.Add(alocacao);
        await db.SaveChangesAsync();

        await db.Entry(alocacao).Reference(a => a.Staff).LoadAsync();
        await db.Entry(alocacao).Reference(a => a.Atividade).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = alocacao.Id }, alocacao);
    }

    // GET /api/alocacoes/{id}  — usado pelo CreatedAtAction
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AlocacaoStaff>> GetById(int id)
    {
        var alocacao = await db.AlocacoesStaff
            .Include(a => a.Staff)
            .Include(a => a.Atividade)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (alocacao is null) return NotFound();
        return Ok(alocacao);
    }

    // DELETE /api/alocacoes/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remover(int id)
    {
        var alocacao = await db.AlocacoesStaff.FindAsync(id);
        if (alocacao is null) return NotFound();

        db.AlocacoesStaff.Remove(alocacao);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
