using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.DTOs;
using NotificationService.Interfaces;
using SharedKernel.Auth;

namespace NotificationService.Controllers
{
    [Route("api/device-tokens")]
    [ApiController]
    [Authorize]
    public class DeviceTokensController : ControllerBase
    {
        private readonly IDeviceTokenService _deviceTokenService;

        public DeviceTokensController(IDeviceTokenService deviceTokenService)
        {
            _deviceTokenService = deviceTokenService;
        }

        // The mobile app calls this on login/launch to register/refresh its FCM token.
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] DeviceTokenRegisterDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (!User.TryGetUserId(out var studentId))
            {
                return Unauthorized("Access token does not contain a valid user id.");
            }

            var role = User.GetRole() ?? "user";

            await _deviceTokenService.RegisterAsync(studentId, role, dto.FcmToken, dto.Platform);
            return NoContent();
        }

        [HttpDelete]
        public async Task<IActionResult> Unregister([FromQuery] string fcmToken)
        {
            if (!User.TryGetUserId(out var studentId))
            {
                return Unauthorized("Access token does not contain a valid user id.");
            }

            var removed = await _deviceTokenService.UnregisterAsync(studentId, fcmToken);
            if (!removed)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
