using FeedbackService.DTOs;
using FeedbackService.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FeedbackService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeedbackImagesController : ControllerBase
    {
        private readonly IFeedbackImageService _feedbackImageService;

        public FeedbackImagesController(IFeedbackImageService feedbackImageService)
        {
            _feedbackImageService = feedbackImageService;
        }

        [HttpGet("feedback/{feedbackId:int}")]
        public async Task<ActionResult<IReadOnlyList<FeedbackImageReadDto>>> GetByFeedbackId(int feedbackId)
        {
            var images = await _feedbackImageService.GetByFeedbackIdAsync(feedbackId);
            return Ok(images);
        }

        [HttpPost("upload")]
        public async Task<ActionResult<FeedbackImageReadDto>> Upload([FromForm] FeedbackImageUploadDto dto)
        {
            var (image, errorMessage) = await _feedbackImageService.UploadAsync(dto.File, dto.FeedbackId);
            if (image is null)
            {
                if (!string.IsNullOrWhiteSpace(errorMessage) && errorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(errorMessage);
                }

                return BadRequest(errorMessage ?? "Image upload failed.");
            }

            return CreatedAtAction(nameof(GetByFeedbackId), new { feedbackId = image.FeedbackId }, image);
        }

        [HttpPost]
        public async Task<ActionResult<FeedbackImageReadDto>> Create([FromBody] FeedbackImageCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var created = await _feedbackImageService.AddAsync(dto);
            if (created is null)
            {
                return NotFound($"Feedback with ID {dto.FeedbackId} was not found.");
            }

            return CreatedAtAction(nameof(GetByFeedbackId), new { feedbackId = created.FeedbackId }, created);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _feedbackImageService.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpDelete("by-path")]
        public async Task<IActionResult> DeleteByPath([FromQuery] string fileNameOrPath)
        {
            var deleted = await _feedbackImageService.DeleteByPathAsync(fileNameOrPath);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }

    }
}
