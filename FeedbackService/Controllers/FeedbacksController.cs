using FeedbackService.DTOs;
using FeedbackService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Auth;

namespace FeedbackService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;

        public FeedbacksController(IFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        // A "user" only ever sees/touches their own feedback; admin/super_admin see everything.
        [HttpGet]
        public async Task<ActionResult<PagedResult<FeedbackReadDto>>> GetAll([FromQuery] PaginationParams parameters)
        {
            string? ownerFilter = null;
            if (!IsAdmin())
            {
                if (!User.TryGetUserId(out var studentId))
                {
                    return Unauthorized("Access token does not contain a valid user id.");
                }

                ownerFilter = studentId;
            }

            var feedbacks = await _feedbackService.GetAllAsync(parameters, ownerFilter);
            return Ok(feedbacks);
        }

        // Always the caller's own feedback, id taken from the token — never a client-supplied id.
        [HttpGet("mine")]
        public async Task<ActionResult<PagedResult<FeedbackReadDto>>> GetMine([FromQuery] PaginationParams parameters)
        {
            if (!User.TryGetUserId(out var studentId))
            {
                return Unauthorized("Access token does not contain a valid user id.");
            }

            var feedbacks = await _feedbackService.GetAllAsync(parameters, studentId);
            return Ok(feedbacks);
        }

        // Admin overview tiles: open-complaint count + the latest few feedback items.
        [HttpGet("dashboard")]
        public async Task<ActionResult<FeedbackDashboardDto>> GetDashboard()
        {
            if (!IsAdmin())
            {
                return Forbid();
            }

            return Ok(await _feedbackService.GetDashboardAsync());
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<FeedbackReadDto>> GetById(int id)
        {
            var isAdmin = IsAdmin();

            // IsRead tracks whether staff has seen the feedback, so only an
            // admin viewing it flips it — a student re-checking their own
            // submission shouldn't mark it as read.
            var feedback = await _feedbackService.GetByIdAsync(id, markAsRead: isAdmin);
            if (feedback is null)
            {
                return NotFound();
            }

            if (!isAdmin && !OwnsFeedback(feedback))
            {
                return Forbid();
            }

            return Ok(feedback);
        }

        [HttpPost]
        public async Task<ActionResult<FeedbackReadDto>> Create([FromBody] FeedbackCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (!User.TryGetUserId(out var studentId))
            {
                return Unauthorized("Access token does not contain a valid user id.");
            }

            var created = await _feedbackService.CreateAsync(dto, studentId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPost("with-images")]
        public async Task<ActionResult<FeedbackReadDto>> CreateWithImages([FromForm] FeedbackCreateWithImagesDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (!User.TryGetUserId(out var studentId))
            {
                return Unauthorized("Access token does not contain a valid user id.");
            }

            var (created, errorMessage) = await _feedbackService.CreateWithImagesAsync(dto, studentId);
            if (created is null)
            {
                return BadRequest(errorMessage ?? "Failed to create feedback with images.");
            }

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] FeedbackUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (!IsAdmin())
            {
                var existing = await _feedbackService.GetByIdAsync(id);
                if (existing is null)
                {
                    return NotFound();
                }

                if (!OwnsFeedback(existing))
                {
                    return Forbid();
                }
            }

            var updated = await _feedbackService.UpdateAsync(id, dto);
            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }

        // Admin-only: reply to a complaint/suggestion. Also marks it as read.
        [HttpPost("{id:int}/reply")]
        public async Task<ActionResult<FeedbackReadDto>> Reply(int id, [FromBody] FeedbackReplyDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (!IsAdmin())
            {
                return Forbid();
            }

            if (!User.TryGetUserId(out var adminId))
            {
                return Unauthorized("Access token does not contain a valid user id.");
            }

            var replied = await _feedbackService.ReplyAsync(id, dto, adminId);
            if (replied is null)
            {
                return NotFound();
            }

            return Ok(replied);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAdmin())
            {
                var existing = await _feedbackService.GetByIdAsync(id);
                if (existing is null)
                {
                    return NotFound();
                }

                if (!OwnsFeedback(existing))
                {
                    return Forbid();
                }
            }

            var deleted = await _feedbackService.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }

        private bool IsAdmin() => User.IsInRole("admin") || User.IsInRole("super_admin");

        private bool OwnsFeedback(FeedbackReadDto feedback) =>
            User.TryGetUserId(out var studentId) && feedback.StudentId == studentId;
    }
}
