using HousingService.Domain.Enums;
using HousingService.DTOs;
using HousingService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Auth;

namespace HousingService.Controllers;

[ApiController]
[Route("api/housing-requests")]
[Authorize]
public class HousingRequestsController : ControllerBase
{
    private const string AdminRoles = "admin,super_admin";
    private const string StudentRole = "student";

    private readonly IHousingRequestService _requestService;

    public HousingRequestsController(IHousingRequestService requestService)
    {
        _requestService = requestService;
    }

    [Authorize(Roles = StudentRole)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<HousingRequestDto>> Create(CreateHousingRequestDto dto)
    {
        if (!User.TryGetUserId(out var studentId))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        try
        {
            var result = await _requestService.CreateAsync(studentId, dto);
            return CreatedAtAction(nameof(GetMineById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = StudentRole)]
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<HousingRequestDto>>> GetMine()
    {
        if (!User.TryGetUserId(out var studentId))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        var requests = await _requestService.GetMineAsync(studentId);
        return Ok(requests);
    }

    [Authorize(Roles = StudentRole)]
    [HttpGet("mine/{id:int}")]
    public async Task<ActionResult<HousingRequestDto>> GetMineById(int id)
    {
        if (!User.TryGetUserId(out var studentId))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        var request = await _requestService.GetMineByIdAsync(studentId, id);
        return request is null ? NotFound() : Ok(request);
    }

    [Authorize(Roles = StudentRole)]
    [HttpPut("mine/{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateMine(int id, UpdateHousingRequestDto dto)
    {
        if (!User.TryGetUserId(out var studentId))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        try
        {
            var result = await _requestService.UpdateMineAsync(studentId, id, dto);
            return result switch
            {
                null => NotFound(),
                false => BadRequest("This request has already been decided and can no longer be edited."),
                true => NoContent()
            };
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<HousingRequestDto>>> GetAll(
        [FromQuery] int? housingCycleId,
        [FromQuery] int? governorateId,
        [FromQuery] HousingRequestStatus? status,
        [FromQuery] AdmissionDecisionStatus? admissionStatus,
        [FromQuery] PaginationParams parameters)
    {
        var requests = await _requestService.GetAllAsync(housingCycleId, governorateId, status, admissionStatus, parameters);
        return Ok(requests);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<HousingRequestDto>> GetById(int id)
    {
        var request = await _requestService.GetByIdAsync(id);
        return request is null ? NotFound() : Ok(request);
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost("{id:int}/documents/{documentId:int}/review")]
    public async Task<IActionResult> ReviewDocument(int id, int documentId, ReviewDocumentDto dto)
    {
        if (!User.TryGetUserId(out var reviewedBy))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        var result = await _requestService.ReviewDocumentAsync(id, documentId, dto, reviewedBy);
        return result switch
        {
            null => NotFound(),
            false => BadRequest("This request has already been decided; documents can no longer be reviewed."),
            true => NoContent()
        };
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPost("{id:int}/decision")]
    public async Task<ActionResult<HousingRequestDto>> MakeDecision(int id, MakeAdmissionDecisionDto dto)
    {
        if (!User.TryGetUserId(out var reviewedBy))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        try
        {
            var result = await _requestService.MakeDecisionAsync(id, dto, reviewedBy);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
