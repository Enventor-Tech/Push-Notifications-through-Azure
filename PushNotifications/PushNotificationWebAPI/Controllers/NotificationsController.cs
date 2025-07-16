using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PushNotificationWebAPI.Models;
using PushNotificationWebAPI.Services;
using System;
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
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            INotificationService notificationService,
            ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpPut("installations")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.UnprocessableEntity)]
        public async Task<IActionResult> UpdateInstallation([Required] DeviceInstallation deviceInstallation)
        {
            _logger.LogInformation("Received request to update device installation: {@DeviceInstallation}", deviceInstallation);

            if (deviceInstallation == null ||
                string.IsNullOrWhiteSpace(deviceInstallation.InstallationId) ||
                string.IsNullOrWhiteSpace(deviceInstallation.Platform) ||
                string.IsNullOrWhiteSpace(deviceInstallation.PushChannel))
            {
                _logger.LogWarning("Invalid device installation data.");
                return BadRequest("Invalid device installation data.");
            }

            var success = await _notificationService
                .CreateOrUpdateInstallationAsync(deviceInstallation, HttpContext.RequestAborted);

            if (!success)
            {
                _logger.LogError("Failed to create or update installation: {@DeviceInstallation}", deviceInstallation);
                return new UnprocessableEntityResult();
            }

            _logger.LogInformation("Successfully updated device installation.");
            return Ok();
        }

        [HttpPost("schedule")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.UnprocessableEntity)]
        public async Task<IActionResult> ScheduleNotification([Required][FromBody] ScheduleNotificationDto request)
        {
            _logger.LogInformation("Received request to schedule notification: {@ScheduleRequest}", request);

            if (string.IsNullOrWhiteSpace(request.DeviceToken) || string.IsNullOrWhiteSpace(request.Message))
            {
                _logger.LogWarning("Schedule request missing required fields.");
                return BadRequest("DeviceToken and Message are required.");
            }

            var scheduleRequest = new ScheduledNotificationRequest
            {
                Message = request.Message,
                DeviceToken = request.DeviceToken,
                ScheduledTime = request.ScheduledTime
            };

            var (success, errorMessage) = await _notificationService
                .ScheduleNotificationAsync(scheduleRequest, HttpContext.RequestAborted);

            if (!success)
            {
                _logger.LogError("Failed to schedule notification: {Error}", errorMessage);
                return UnprocessableEntity(new { title = "Unprocessable Entity", status = 422, detail = errorMessage });
            }

            _logger.LogInformation("Notification scheduled successfully for: {ScheduledTime}", request.ScheduledTime);
            return Ok();
        }

        [HttpPost("send-immediate")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.UnprocessableEntity)]
        public async Task<IActionResult> SendImmediateNotification([Required][FromBody] ScheduleNotificationDto request)
        {
            _logger.LogInformation("Received request to send immediate notification: {@Request}", request);

            if (string.IsNullOrWhiteSpace(request.DeviceToken) || string.IsNullOrWhiteSpace(request.Message))
            {
                _logger.LogWarning("Immediate notification request missing required fields.");
                return BadRequest("DeviceToken and Message are required.");
            }

            var (success, errorMessage) = await _notificationService.SendNotificationToInstallationAsync(
                request.DeviceToken,
                "fcmv1", // Adjust as needed for platform
                request.Message,
                HttpContext.RequestAborted);

            if (!success)
            {
                _logger.LogError("Failed to send immediate notification: {Error}", errorMessage);
                return UnprocessableEntity(new { title = "Unprocessable Entity", status = 422, detail = errorMessage });
            }

            _logger.LogInformation("Immediate notification sent successfully to {DeviceToken}.", request.DeviceToken);
            return Ok("Notification sent immediately.");
        }

        [HttpGet]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public IActionResult Get()
        {
            _logger.LogInformation("Health check endpoint called.");
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
