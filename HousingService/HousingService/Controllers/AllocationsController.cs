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
    private const string StudentRole = "student";

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
    [HttpGet("students/{studentId}/history")]
    public async Task<ActionResult<IReadOnlyList<AllocationDto>>> GetStudentHistory(string studentId)
    {
        var history = await _allocationService.GetHistoryForStudentAsync(studentId);
        return Ok(history);
    }

    // Student-centric convenience actions: resolve the student's current allocation
    // themselves (individual or via whichever group they're in), no allocation id or room id
    // needed up front — unlike /{id}/vacate and /{id}/transfer, which operate on a room's
    // allocation directly.
    [Authorize(Roles = AdminRoles)]
    [HttpPost("students/{studentId}/vacate")]
    public async Task<ActionResult<AllocationDto>> VacateStudent(string studentId, VacateAllocationDto dto)
    {
        try
        {
            var result = await _allocationService.VacateStudentAsync(studentId, dto);
            return result is null ? NotFound("This student is not currently housed.") : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost("students/{studentId}/transfer")]
    public async Task<ActionResult<AllocationDto>> TransferStudent(string studentId, TransferAllocationDto dto)
    {
        try
        {
            var result = await _allocationService.TransferStudentAsync(studentId, dto);
            return result is null ? NotFound("This student is not currently housed.") : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<AllocationDto>>> GetAll([FromQuery] int? buildingId, [FromQuery] int? roomId, [FromQuery] PaginationParams parameters)
    {
        var allocations = await _allocationService.GetAllAsync(buildingId, roomId, parameters);
        return Ok(allocations);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AllocationDto>> GetById(int id)
    {
        var allocation = await _allocationService.GetByIdAsync(id);
        return allocation is null ? NotFound() : Ok(allocation);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost("{id:int}/transfer")]
    public async Task<ActionResult<AllocationDto>> Transfer(int id, TransferAllocationDto dto)
    {
        try
        {
            var result = await _allocationService.TransferAsync(id, dto);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost("{id:int}/vacate")]
    public async Task<ActionResult<AllocationDto>> Vacate(int id, VacateAllocationDto dto)
    {
        try
        {
            var result = await _allocationService.VacateAsync(id, dto);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // Removes one specific member from a shared group allocation, keeping the rest of the group
    // housed in that room — unlike /vacate, which ends the whole allocation.
    [Authorize(Roles = AdminRoles)]
    [HttpPost("{id:int}/members/{studentId}/remove")]
    public async Task<ActionResult<AllocationDto>> RemoveGroupMember(int id, string studentId)
    {
        try
        {
            var result = await _allocationService.RemoveGroupMemberAsync(id, studentId);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
