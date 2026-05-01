using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentService.Data;

namespace StudentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GovernoratesController : ControllerBase
{
    private readonly AppDbContext _context;

    public GovernoratesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<object>>> GetAll()
    {
        var governorates = await _context.Governorates
            .AsNoTracking()
            .Select(g => new { g.Id, g.Name })
            .ToListAsync();

        return Ok(governorates);
    }
}
