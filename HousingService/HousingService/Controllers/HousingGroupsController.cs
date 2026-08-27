using HousingService.DTOs;
using HousingService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Auth;

namespace HousingService.Controllers;

[ApiController]
[Route("api/housing-groups")]
[Authorize]
public class HousingGroupsController : ControllerBase
{
    private const string AdminRoles = "admin,super_admin";
    private const string StudentRole = "student";

    private readonly IHousingGroupService _groupService;

    public HousingGroupsController(IHousingGroupService groupService)
    {
        _groupService = groupService;
    }

    [Authorize(Roles = StudentRole)]
    [HttpPost]
    public async Task<ActionResult<HousingGroupDto>> Create(CreateHousingGroupDto dto)
    {
        if (!User.TryGetUserId(out var leaderId))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        try
        {
            var result = await _groupService.CreateAsync(leaderId, dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = StudentRole)]
    [HttpGet("mine")]
    public async Task<ActionResult<HousingGroupDto>> GetMine()
    {
        if (!User.TryGetUserId(out var studentId))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        var group = await _groupService.GetMineAsync(studentId);
        return group is null ? NotFound("You are not currently part of a housing group.") : Ok(group);
    }

    [Authorize(Roles = StudentRole)]
    [HttpPost("join")]
    public async Task<ActionResult<GroupInvitationDto>> Join(JoinHousingGroupDto dto)
    {
        if (!User.TryGetUserId(out var studentId))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        try
        {
            var result = await _groupService.JoinByCodeAsync(studentId, dto);
            return result is null ? NotFound("Invalid group code.") : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = StudentRole)]
    [HttpPost("mine/leave")]
    public async Task<IActionResult> Leave()
    {
        if (!User.TryGetUserId(out var studentId))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        var left = await _groupService.LeaveAsync(studentId);
        return left ? NoContent() : NotFound("You are not currently part of a housing group.");
    }

    [Authorize(Roles = StudentRole)]
    [HttpPost("invitations/{invitationId:int}/respond")]
    public async Task<IActionResult> RespondToInvitation(int invitationId, RespondToInvitationDto dto)
    {
        if (!User.TryGetUserId(out var leaderId))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        try
        {
            var result = await _groupService.RespondToInvitationAsync(leaderId, invitationId, dto);
            return result is true ? NoContent() : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<HousingGroupDto>>> GetAll([FromQuery] int? housingCycleId, [FromQuery] PaginationParams parameters)
    {
        var groups = await _groupService.GetAllAsync(housingCycleId, parameters);
        return Ok(groups);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<HousingGroupDto>> GetById(int id)
    {
        var group = await _groupService.GetByIdAsync(id);
        return group is null ? NotFound() : Ok(group);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost("{id:int}/members/{studentId}/remove")]
    public async Task<IActionResult> RemoveMember(int id, string studentId)
    {
        var result = await _groupService.RemoveMemberAsync(id, studentId);
        return result switch
        {
            null => NotFound(),
            _ => NoContent()
        };
    }

    [Authorize(Roles = StudentRole)]
    [HttpPost("mine/members/{studentId}/remove")]
    public async Task<IActionResult> RemoveMemberAsLeader(string studentId)
    {
        if (!User.TryGetUserId(out var leaderId))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        try
        {
            var result = await _groupService.RemoveMemberAsLeaderAsync(leaderId, studentId);
            return result is true
                ? NoContent()
                : NotFound("You are not the leader of a group, or that student is not a member.");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
