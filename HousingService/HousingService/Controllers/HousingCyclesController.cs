using HousingService.DTOs;
using HousingService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HousingService.Controllers;

[ApiController]
[Route("api/housing-cycles")]
[Authorize]
public class HousingCyclesController : ControllerBase
{
    private const string AdminRoles = "admin,super_admin";

    private readonly IHousingCycleService _cycleService;

    public HousingCyclesController(IHousingCycleService cycleService)
    {
        _cycleService = cycleService;
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost]
    public async Task<ActionResult<HousingCycleDto>> Create(CreateHousingCycleDto dto)
    {
        try
        {
            var result = await _cycleService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HousingCycleDto>>> GetAll()
    {
        var cycles = await _cycleService.GetAllAsync();
        return Ok(cycles);
    }

    [HttpGet("current")]
    public async Task<ActionResult<HousingCycleDto>> GetCurrent()
    {
        var cycle = await _cycleService.GetCurrentOpenAsync();
        return cycle is null ? NotFound("No housing cycle is currently open.") : Ok(cycle);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HousingCycleDto>> GetById(int id)
    {
        var cycle = await _cycleService.GetByIdAsync(id);
        return cycle is null ? NotFound() : Ok(cycle);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost("{id:int}/open")]
    public async Task<IActionResult> Open(int id)
    {
        try
        {
            var opened = await _cycleService.OpenAsync(id);
            return opened ? NoContent() : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id)
    {
        var closed = await _cycleService.CloseAsync(id);
        return closed ? NoContent() : NotFound();
    }
}
