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
    private readonly IHousingSettingsService _settingsService;

    public HousingRequestsController(IHousingRequestService requestService, IHousingSettingsService settingsService)
    {
        _requestService = requestService;
        _settingsService = settingsService;
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

    [Authorize(Roles = StudentRole)]
    [HttpPost("{id:int}/pay")]
    public async Task<IActionResult> Pay(int id)
    {
        if (!User.TryGetUserId(out var studentId))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        var result = await _requestService.PayAsync(studentId, id);
        return result.Outcome switch
        {
            PaymentOutcome.Success => Ok(new { message = result.Message, balance = result.NewBalance }),
            PaymentOutcome.RequestNotFound => NotFound(),
            PaymentOutcome.NotOwned => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Message }),
            PaymentOutcome.NotAccepted => BadRequest(new { message = result.Message }),
            PaymentOutcome.AlreadyPaid => Conflict(new { message = result.Message }),
            PaymentOutcome.FeeNotConfigured => Conflict(new { message = result.Message }),
            PaymentOutcome.InsufficientBalance => StatusCode(StatusCodes.Status402PaymentRequired, new { message = result.Message }),
            PaymentOutcome.GatewayError => StatusCode(StatusCodes.Status502BadGateway, new { message = result.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.Message })
        };
    }

    [Authorize(Roles = AdminRoles)]
    [HttpGet("settings")]
    public async Task<ActionResult<HousingSettingsDto>> GetSettings()
    {
        return Ok(await _settingsService.GetAsync());
    }

    [Authorize(Roles = AdminRoles)]
    [HttpPut("settings")]
    public async Task<ActionResult<HousingSettingsDto>> UpdateSettings(UpdateHousingSettingsDto dto)
    {
        try
        {
            return Ok(await _settingsService.UpdateAsync(dto));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // Available to every role: a student may delete their own request, an admin may delete any.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized("Access token does not contain a valid user id.");
        }

        var isAdmin = IsAdmin();
        if (!isAdmin)
        {
            var existing = await _requestService.GetMineByIdAsync(userId, id);
            if (existing is null)
            {
                return NotFound();
            }
        }

        try
        {
            var result = await _requestService.DeleteAsync(id, userId, isAdmin);
            return result is null ? NotFound() : NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private bool IsAdmin() => User.IsInRole("admin") || User.IsInRole("super_admin");
}
