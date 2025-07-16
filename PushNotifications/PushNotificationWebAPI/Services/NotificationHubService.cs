using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.Options;
using PushNotificationWebAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PushNotificationWebAPI.Services
{
    public class NotificationHubService : INotificationService
    {
        private readonly NotificationHubClient _hub;
        private readonly Dictionary<string, NotificationPlatform> _installationPlatform;
        private readonly ILogger<NotificationHubService> _logger;

        public NotificationHubService(IOptions<NotificationHubOptions> options, ILogger<NotificationHubService> logger)
        {
            _logger = logger;
            _hub = NotificationHubClient.CreateClientFromConnectionString(
                options.Value.ConnectionString, options.Value.Name);

            _installationPlatform = new Dictionary<string, NotificationPlatform>
            {
                { nameof(NotificationPlatform.Apns).ToLower(), NotificationPlatform.Apns },
                { nameof(NotificationPlatform.FcmV1).ToLower(), NotificationPlatform.FcmV1 }
            };
        }

        public async Task<bool> CreateOrUpdateInstallationAsync(DeviceInstallation deviceInstallation, CancellationToken token)
        {
            _logger.LogInformation("Starting CreateOrUpdateInstallationAsync for InstallationId: {InstallationId}", deviceInstallation?.InstallationId);

            if (string.IsNullOrWhiteSpace(deviceInstallation?.InstallationId) ||
                string.IsNullOrWhiteSpace(deviceInstallation?.Platform) ||
                string.IsNullOrWhiteSpace(deviceInstallation?.PushChannel))
            {
                _logger.LogWarning("Invalid device installation data received. InstallationId={InstallationId}, Platform={Platform}, PushChannel={PushChannel}",
                    deviceInstallation?.InstallationId, deviceInstallation?.Platform, deviceInstallation?.PushChannel);
                return false;
            }

            if (!_installationPlatform.TryGetValue(deviceInstallation.Platform.ToLower(), out var platform))
            {
                _logger.LogError("Unsupported platform: {Platform}", deviceInstallation.Platform);
                return false;
            }

            var installation = new Installation
            {
                InstallationId = deviceInstallation.InstallationId,
                PushChannel = deviceInstallation.PushChannel,
                Tags = deviceInstallation.Tags?.Count > 0
                    ? new List<string>(deviceInstallation.Tags)
                    : new List<string> { "all" },
                Platform = platform
            };

            try
            {
                await _hub.CreateOrUpdateInstallationAsync(installation, token);
                _logger.LogInformation("Successfully created/updated installation. ID: {InstallationId}, Platform: {Platform}, Tags: {Tags}",
                    installation.InstallationId, installation.Platform, string.Join(", ", installation.Tags));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while creating/updating installation ID: {InstallationId}", installation.InstallationId);
                return false;
            }
        }

        public async Task<(bool Success, string ErrorMessage)> ScheduleNotificationAsync(ScheduledNotificationRequest scheduledNotification, CancellationToken token)
        {
            _logger.LogInformation("Starting ScheduleNotificationAsync for DeviceToken: {DeviceToken}, ScheduledTime: {ScheduledTime}",
                scheduledNotification?.DeviceToken, scheduledNotification?.ScheduledTime);

            if (scheduledNotification == null || string.IsNullOrWhiteSpace(scheduledNotification.Message))
            {
                _logger.LogWarning("Invalid notification schedule request: null or missing message");
                return (false, "Message is required.");
            }

            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime currentET = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone);

            if (scheduledNotification.ScheduledTime <= currentET)
            {
                _logger.LogWarning("Scheduled time {ScheduledTime} is not in the future. Current ET: {CurrentET}",
                    scheduledNotification.ScheduledTime, currentET);
                return (false, "Scheduled time must be in the future.");
            }

            var deliveryTime = new DateTimeOffset(scheduledNotification.ScheduledTime, TimeSpan.Zero);
            var androidPayload = $@"{{
  ""message"": {{
    ""notification"": {{
      ""body"": ""{scheduledNotification.Message}""
    }}
  }}
}}";

            var iOSPayload = $@"{{
  ""aps"": {{
    ""alert"": ""{scheduledNotification.Message}""
  }}
}}";

            try
            {
                var registrations = await _hub.GetAllRegistrationsAsync(100, token);
                int iosCount = registrations.OfType<AppleRegistrationDescription>().Count(r => r.Tags?.Contains("all") ?? false);
                int androidCount = registrations.OfType<FcmV1RegistrationDescription>().Count(r => r.Tags?.Contains("all") ?? false);

                _logger.LogInformation("Registrations found - iOS: {IosCount}, Android: {AndroidCount}", iosCount, androidCount);

                var tasks = new List<Task>();

                if (androidCount > 0)
                {
                    _logger.LogInformation("Scheduling Android notification at {DeliveryTime}", deliveryTime);
                    tasks.Add(_hub.ScheduleNotificationAsync(
                        new FcmV1Notification(androidPayload),
                        deliveryTime,
                        new[] { "android" }, token));
                }

                if (iosCount > 0)
                {
                    _logger.LogInformation("Scheduling iOS notification at {DeliveryTime}", deliveryTime);
                    tasks.Add(_hub.ScheduleNotificationAsync(
                        new AppleNotification(iOSPayload),
                        deliveryTime,
                        new[] { "ios" }, token));
                }

                if (!tasks.Any())
                {
                    _logger.LogWarning("No target platforms found for scheduling.");
                    return (false, "No devices found for scheduling.");
                }

                await Task.WhenAll(tasks);

                _logger.LogInformation("Successfully scheduled notification to {Count} platform(s) at {Time}", tasks.Count, deliveryTime);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling notification for {Time}", deliveryTime);
                return (false, $"Azure Notification Hub error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string ErrorMessage)> SendNotificationToInstallationAsync(string installationId, string platform, string message, CancellationToken token)
        {
            _logger.LogInformation("Starting SendNotificationToInstallationAsync. InstallationId: {InstallationId}, Platform: {Platform}", installationId, platform);

            try
            {
                string escapedMessage = message.Replace("\"", "\\\"");
                Notification notification;

                switch (platform.ToLower())
                {
                    case "apns":
                        var iosPayload = $@"{{ ""aps"": {{ ""alert"": ""{escapedMessage}"" }} }}";
                        notification = new AppleNotification(iosPayload);
                        break;

                    case "fcmv1":
                        var androidPayload = $@"{{
                            ""message"": {{
                                ""notification"": {{
                                    ""body"": ""{escapedMessage}""
                                }},
                                ""android"": {{
                                    ""priority"": ""high""
                                }}
                            }}
                        }}";
                        notification = new FcmV1Notification(androidPayload);
                        break;

                    default:
                        _logger.LogWarning("Unsupported platform specified: {Platform}", platform);
                        return (false, "Unsupported platform");
                }

                await _hub.SendNotificationAsync(notification, new[] { installationId }, token);

                _logger.LogInformation("Notification sent successfully to InstallationId: {InstallationId}", installationId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception sending notification to InstallationId: {InstallationId}", installationId);
                return (false, $"Failed to send notification: {ex.Message}");
            }
        }
    }
}
