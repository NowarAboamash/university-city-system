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
        private const string AdminRoles = "admin,super_admin";

        private readonly IFeedbackService _feedbackService;

        public FeedbacksController(IFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        [Authorize(Roles = AdminRoles)]
        [HttpGet]
        public async Task<ActionResult<PagedResult<FeedbackReadDto>>> GetAll([FromQuery] PaginationParams parameters)
        {
            var feedbacks = await _feedbackService.GetAllAsync(parameters);
            return Ok(feedbacks);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<FeedbackReadDto>> GetById(int id)
        {
            var feedback = await _feedbackService.GetByIdAsync(id);
            if (feedback is null)
            {
                return NotFound();
            }

            if (!User.IsInRole("admin") && !User.IsInRole("super_admin"))
            {
                if (!User.TryGetUserId(out var studentId) || feedback.StudentId != studentId)
                {
                    return Forbid();
                }
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

        [Authorize(Roles = AdminRoles)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] FeedbackUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var updated = await _feedbackService.UpdateAsync(id, dto);
            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }

        [Authorize(Roles = AdminRoles)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _feedbackService.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
