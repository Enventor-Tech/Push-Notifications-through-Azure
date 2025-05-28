using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PushNotificationWebAPI.Models;
using PushNotificationWebAPI.Services;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Threading.Tasks;

namespace PushNotificationWebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpPut("installations")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.UnprocessableEntity)]
        public async Task<IActionResult> UpdateInstallation([Required] DeviceInstallation deviceInstallation)
        {
            if (deviceInstallation == null ||
                string.IsNullOrWhiteSpace(deviceInstallation.InstallationId) ||
                string.IsNullOrWhiteSpace(deviceInstallation.Platform) ||
                string.IsNullOrWhiteSpace(deviceInstallation.PushChannel))
            {
                return BadRequest("Invalid device installation data.");
            }

            var success = await _notificationService
                .CreateOrUpdateInstallationAsync(deviceInstallation, HttpContext.RequestAborted);

            if (!success)
                return new UnprocessableEntityResult();

            return new OkResult();
        }

        [HttpPost("schedule")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.UnprocessableEntity)]
        public async Task<IActionResult> ScheduleNotification([Required][FromBody] ScheduleNotificationDto request)
        {
            if (string.IsNullOrWhiteSpace(request.DeviceToken) || string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("DeviceToken and Message are required.");

            var scheduleRequest = new ScheduledNotificationRequest
            {
                Message = request.Message,
                DeviceToken = request.DeviceToken,
                ScheduledTime = request.ScheduledTime
            };

            var (success, errorMessage) = await _notificationService
                .ScheduleNotificationAsync(scheduleRequest, HttpContext.RequestAborted);

            if (!success)
                return UnprocessableEntity(new { title = "Unprocessable Entity", status = 422, detail = errorMessage });

            return new OkResult();
        }

        [HttpGet]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public IActionResult Get()
        {
            return Ok("Notifications API is up and running.");
        }

        public class ScheduleNotificationDto
        {
            [Required]
            public string DeviceToken { get; set; }
            [Required]
            public string Message { get; set; }
            [Required]
            public DateTime ScheduledTime { get; set; }
        }
    }
}