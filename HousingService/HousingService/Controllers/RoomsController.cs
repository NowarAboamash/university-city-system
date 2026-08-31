using HousingService.DTOs;
using HousingService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HousingService.Controllers;

[ApiController]
[Route("api/buildings/{buildingId:int}/rooms")]
[Authorize]
public class RoomsController : ControllerBase
{
    private const string AdminRoles = "admin,super_admin";

    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    // Minimal id/floor/roomNumber list any authenticated role can read — so a student's app
    // can offer a floor/room picker for the "previous residence" fields on a housing request,
    // without exposing status or occupant ids (the admin GET below keeps those).
    [HttpGet("lookup")]
    public async Task<ActionResult<IReadOnlyList<RoomLookupDto>>> GetLookup(int buildingId)
    {
        var rooms = await _roomService.GetLookupByBuildingAsync(buildingId);
        return rooms is null ? NotFound($"Building {buildingId} was not found.") : Ok(rooms);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost]
    public async Task<ActionResult<RoomDto>> Create(int buildingId, CreateRoomDto dto)
    {
        try
        {
            var result = await _roomService.CreateAsync(buildingId, dto);
            if (result is null)
            {
                return NotFound($"Building {buildingId} was not found.");
            }

            return CreatedAtAction(nameof(GetById), new { buildingId, id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoomDto>>> GetByBuilding(int buildingId)
    {
        var rooms = await _roomService.GetByBuildingAsync(buildingId);
        return rooms is null ? NotFound($"Building {buildingId} was not found.") : Ok(rooms);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<RoomDto>> GetById(int buildingId, int id)
    {
        var room = await _roomService.GetByIdAsync(buildingId, id);
        return room is null ? NotFound() : Ok(room);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int buildingId, int id, UpdateRoomDto dto)
    {
        try
        {
            var result = await _roomService.UpdateAsync(buildingId, id, dto);
            return result switch
            {
                null => NotFound($"Building {buildingId} was not found."),
                false => NotFound($"Room {id} was not found in building {buildingId}."),
                true => NoContent()
            };
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
