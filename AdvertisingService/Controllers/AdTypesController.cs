using AdvertisingService.Enums;
using Microsoft.AspNetCore.Mvc;

namespace AdvertisingService.Controllers;

[ApiController]
[Route("api/ad-types")]
public class AdTypesController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<object>> GetAll()
    {
        var adTypes = Enum.GetValues<AdType>()
            .Select(adType => new
            {
                Id = (int)adType,
                Name = adType.ToString()
            })
            .ToList();

        return Ok(adTypes);
    }
}
