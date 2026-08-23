using HousingService.DTOs;
using HousingService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HousingService.Controllers;

[ApiController]
[Route("api/governorates")]
[Authorize]
public class GovernoratesController : ControllerBase
{
    private const string AdminRoles = "admin,super_admin";

    private readonly IGovernorateService _governorateService;

    public GovernoratesController(IGovernorateService governorateService)
    {
        _governorateService = governorateService;
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost]
    public async Task<ActionResult<GovernorateDto>> Create(CreateGovernorateDto dto)
    {
        try
        {
            var result = await _governorateService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GovernorateDto>>> GetAll()
    {
        var governorates = await _governorateService.GetAllAsync();
        return Ok(governorates);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GovernorateDto>> GetById(int id)
    {
        var governorate = await _governorateService.GetByIdAsync(id);
        return governorate is null ? NotFound() : Ok(governorate);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateGovernorateDto dto)
    {
        try
        {
            var updated = await _governorateService.UpdateAsync(id, dto);
            return updated ? NoContent() : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
