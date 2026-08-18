using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.DTOs;
using NotificationService.Interfaces;
using SharedKernel.Auth;

namespace NotificationService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // Notification management (compose/send/edit/delete) is an admin capability;
        // "user" only ever receives and reads via /mine and {id}/read below.
        [Authorize(Roles = "admin,super_admin")]
        [HttpGet]
        public async Task<ActionResult<PagedResult<NotificationReadDto>>> GetAll([FromQuery] PaginationParams parameters)
        {
            var result = await _notificationService.GetAllAsync(parameters);
            return Ok(result);
        }

        [Authorize(Roles = "admin,super_admin")]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<NotificationReadDto>> GetById(int id)
        {
            var notification = await _notificationService.GetByIdAsync(id);
            if (notification is null)
            {
                return NotFound();
            }

            return Ok(notification);
        }

        [Authorize(Roles = "admin,super_admin")]
        [HttpPost]
        public async Task<ActionResult<NotificationReadDto>> Create([FromBody] NotificationCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (!User.TryGetUserId(out var createdBy))
            {
                return Unauthorized("Access token does not contain a valid user id.");
            }

            var (created, errorMessage) = await _notificationService.CreateAndSendAsync(dto, createdBy);
            if (created is null)
            {
                return BadRequest(errorMessage ?? "Failed to create notification.");
            }

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [Authorize(Roles = "admin,super_admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] NotificationUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var updated = await _notificationService.UpdateAsync(id, dto);
            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }

        [Authorize(Roles = "admin,super_admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _notificationService.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }

        // Any role's own notification inbox, id taken from the token.
        [HttpGet("mine")]
        public async Task<ActionResult<PagedResult<NotificationInboxItemDto>>> GetMine([FromQuery] PaginationParams parameters)
        {
            if (!User.TryGetUserId(out var studentId))
            {
                return Unauthorized("Access token does not contain a valid user id.");
            }

            var inbox = await _notificationService.GetInboxAsync(studentId, parameters);
            return Ok(inbox);
        }

        [HttpPost("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            if (!User.TryGetUserId(out var studentId))
            {
                return Unauthorized("Access token does not contain a valid user id.");
            }

            var marked = await _notificationService.MarkAsReadAsync(id, studentId);
            if (!marked)
            {
                return NotFound();
            }

            return NoContent();
        }

        // Service-to-service only — no user JWT. This is the endpoint SharedKernel's
        // INotificationPublisher calls from any other backend service.
        [AllowAnonymous]
        [RequireServiceKey]
        [HttpPost("broadcast")]
        public async Task<ActionResult<NotificationReadDto>> Broadcast([FromBody] InternalNotificationRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var createDto = new NotificationCreateDto
            {
                Title = dto.Title,
                Body = dto.Body,
                Data = dto.Data,
                TargetType = dto.TargetType,
                TargetStudentIds = dto.TargetStudentIds,
                TargetRole = dto.TargetRole
            };

            var (created, errorMessage) = await _notificationService.CreateAndSendAsync(createDto, dto.SourceService);
            if (created is null)
            {
                return BadRequest(errorMessage ?? "Failed to create notification.");
            }

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
    }
}
