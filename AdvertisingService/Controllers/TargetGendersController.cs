using AdvertisingService.Enums;
using Microsoft.AspNetCore.Mvc;

namespace AdvertisingService.Controllers;

[ApiController]
[Route("api/target-genders")]
public class TargetGendersController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<object>> GetAll()
    {
        var targetGenders = Enum.GetValues<TargetGender>()
            .Select(targetGender => new
            {
                Id = (int)targetGender,
                Name = targetGender.ToString()
            })
            .ToList();

        return Ok(targetGenders);
    }
}
