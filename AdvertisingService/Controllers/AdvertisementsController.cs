using AdvertisingService.DTOs;
using AdvertisingService.Enums;
using AdvertisingService.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AdvertisingService.Controllers;

[ApiController]
[Route("api/ads")]
public class AdvertisementsController : ControllerBase
{
    private readonly IAdvertisementService _advertisementService;

    public AdvertisementsController(IAdvertisementService advertisementService)
    {
        _advertisementService = advertisementService;
    }

    [HttpPost]
    public async Task<ActionResult<AdvertisementDto>> Create([FromBody] CreateAdvertisementDto dto, [FromHeader(Name = "X-User-Id")] Guid createdBy)
    {
        try
        {
            var result = await _advertisementService.CreateAsync(dto, createdBy);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAdvertisementDto dto)
    {
        try
        {
            var updated = await _advertisementService.UpdateAsync(id, dto);
            return updated ? NoContent() : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _advertisementService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdvertisementDto>>> GetAll()
    {
        var ads = await _advertisementService.GetAllAsync();
        return Ok(ads);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdvertisementDto>> GetById(Guid id)
    {
        var ad = await _advertisementService.GetByIdAsync(id);
        return ad is null ? NotFound() : Ok(ad);
    }

    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<AdvertisementDto>>> GetActive(
        [FromQuery] TargetGender targetGender,
        [FromQuery] int? collegeId,
        [FromQuery] int? governorateId)
    {
        var ads = await _advertisementService.GetActiveAsync(targetGender, collegeId, governorateId);
        return Ok(ads);
    }
}
