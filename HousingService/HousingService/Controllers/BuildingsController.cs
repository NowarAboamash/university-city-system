using HousingService.DTOs;
using HousingService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HousingService.Controllers;

[ApiController]
[Route("api/buildings")]
[Authorize]
public class BuildingsController : ControllerBase
{
    private const string AdminRoles = "admin,super_admin";

    private readonly IBuildingService _buildingService;
    private readonly IBuildingEvacuationService _evacuationService;

    public BuildingsController(IBuildingService buildingService, IBuildingEvacuationService evacuationService)
    {
        _buildingService = buildingService;
        _evacuationService = evacuationService;
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost]
    public async Task<ActionResult<BuildingDto>> Create(CreateBuildingDto dto)
    {
        try
        {
            var result = await _buildingService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BuildingDto>>> GetAll()
    {
        var buildings = await _buildingService.GetAllAsync();
        return Ok(buildings);
    }

    // Minimal id+name list any authenticated role can read — e.g. so a student's app can
    // let them pick their previous building when submitting a housing request, without
    // exposing the full admin management view (gender/status/capacity/description).
    [HttpGet("lookup")]
    public async Task<ActionResult<IReadOnlyList<BuildingLookupDto>>> GetLookup()
    {
        var buildings = await _buildingService.GetLookupAsync();
        return Ok(buildings);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BuildingDto>> GetById(int id)
    {
        var building = await _buildingService.GetByIdAsync(id);
        return building is null ? NotFound() : Ok(building);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateBuildingDto dto)
    {
        try
        {
            var updated = await _buildingService.UpdateAsync(id, dto);
            return updated ? NoContent() : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost("{id:int}/evacuation/announce")]
    public async Task<ActionResult<int>> AnnounceEvacuation(int id, AnnounceEvacuationDto dto)
    {
        var notifiedCount = await _evacuationService.AnnounceAsync(id, dto);
        return notifiedCount is null ? NotFound() : Ok(new { notifiedCount });
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost("{id:int}/evacuation/execute")]
    public async Task<ActionResult<EvacuationResultDto>> ExecuteEvacuation(int id, ExecuteEvacuationDto dto)
    {
        var result = await _evacuationService.ExecuteAsync(id, dto);
        return result is null ? NotFound() : Ok(result);
    }
}
