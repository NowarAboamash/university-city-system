using HousingService.DTOs;
using HousingService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Auth;

namespace HousingService.Controllers;

[ApiController]
[Route("api/allocations")]
[Authorize]
public class AllocationsController : ControllerBase
{
    private const string AdminRoles = "admin,super_admin";
    private const string StudentRole = "user";

    private readonly IAllocationService _allocationService;

    public AllocationsController(IAllocationService allocationService)
    {
        _allocationService = allocationService;
    }

    [Authorize(Roles = StudentRole)]
    [HttpGet("mine")]
    public async Task<ActionResult<AllocationDto>> GetMine()
    {
        if (!User.TryGetUserId(out var studentId))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        var allocation = await _allocationService.GetMineAsync(studentId);
        return allocation is null ? NotFound("You have not been allocated a room yet.") : Ok(allocation);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet("candidate-rooms")]
    public async Task<ActionResult<IReadOnlyList<CandidateRoomDto>>> GetCandidateRooms(
        [FromQuery] int? housingRequestId,
        [FromQuery] int? housingGroupId)
    {
        try
        {
            var rooms = await _allocationService.GetCandidateRoomsAsync(housingRequestId, housingGroupId);
            return Ok(rooms);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost]
    public async Task<ActionResult<AllocationDto>> Create(CreateAllocationDto dto)
    {
        try
        {
            var result = await _allocationService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AllocationDto>>> GetAll([FromQuery] int? buildingId, [FromQuery] int? roomId)
    {
        var allocations = await _allocationService.GetAllAsync(buildingId, roomId);
        return Ok(allocations);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AllocationDto>> GetById(int id)
    {
        var allocation = await _allocationService.GetByIdAsync(id);
        return allocation is null ? NotFound() : Ok(allocation);
    }
}
