using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventOps.API.Controllers;

public record CriarTarefaRequest(string Descricao, int EventoId);

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TarefasController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Tarefa>>> GetByEvento([FromQuery] int eventoId)
    {
        var tarefas = await db.Tarefas
            .Where(t => t.EventoId == eventoId)
            .OrderBy(t => t.Id)
            .ToListAsync();
        return Ok(tarefas);
    }

    [HttpPost]
    public async Task<ActionResult<Tarefa>> Create(CriarTarefaRequest req)
    {
        var tarefa = new Tarefa { Descricao = req.Descricao, EventoId = req.EventoId };
        db.Tarefas.Add(tarefa);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetByEvento), new { eventoId = tarefa.EventoId }, tarefa);
    }

    [HttpPut("{id:int}/toggle")]
    public async Task<ActionResult> Toggle(int id)
    {
        var tarefa = await db.Tarefas.FindAsync(id);
        if (tarefa is null) return NotFound();
        tarefa.Concluida = !tarefa.Concluida;
        await db.SaveChangesAsync();
        return Ok(new { tarefa.Id, tarefa.Concluida });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var tarefa = await db.Tarefas.FindAsync(id);
        if (tarefa is null) return NotFound();
        db.Tarefas.Remove(tarefa);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
