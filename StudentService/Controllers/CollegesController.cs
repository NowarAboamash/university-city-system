using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentService.Data;

namespace StudentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CollegesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CollegesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<object>>> GetAll()
    {
        // Frontend calls GET /api/colleges to populate a dropdown, then posts CollegeId in /api/students.
        var colleges = await _context.Colleges
            .AsNoTracking()
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();

        return Ok(colleges);
    }
}
