using EventOps.Core.Models;
using EventOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventOps.API.Controllers;

public record StaffUtilizadorDto(int Id, string Nome, string Email);

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UtilizadoresController(AppDbContext db) : ControllerBase
{
    // GET /api/utilizadores/staff — lista de utilizadores com perfil Staff
    [HttpGet("staff")]
    public async Task<ActionResult<IEnumerable<StaffUtilizadorDto>>> GetStaffUsers()
    {
        var users = await db.Utilizadores
            .Where(u => u.Perfil == Perfil.Staff)
            .OrderBy(u => u.Nome)
            .Select(u => new StaffUtilizadorDto(u.Id, u.Nome, u.Email))
            .ToListAsync();

        return Ok(users);
    }
}
