using FeedbackService.DTOs;
using FeedbackService.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FeedbackService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;

        public FeedbacksController(IFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<FeedbackReadDto>>> GetAll()
        {
            var feedbacks = await _feedbackService.GetAllAsync();
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

            return Ok(feedback);
        }

        [HttpPost]
        public async Task<ActionResult<FeedbackReadDto>> Create([FromBody] FeedbackCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var created = await _feedbackService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPost("with-images")]
        public async Task<ActionResult<FeedbackReadDto>> CreateWithImages([FromForm] FeedbackCreateWithImagesDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var (created, errorMessage) = await _feedbackService.CreateWithImagesAsync(dto);
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

            var updated = await _feedbackService.UpdateAsync(id, dto);
            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }

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
