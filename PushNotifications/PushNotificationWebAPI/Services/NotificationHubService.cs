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
            if (string.IsNullOrWhiteSpace(deviceInstallation?.InstallationId) ||
                string.IsNullOrWhiteSpace(deviceInstallation?.Platform) ||
                string.IsNullOrWhiteSpace(deviceInstallation?.PushChannel))
            {
                _logger.LogWarning("Invalid device installation data: InstallationId={InstallationId}, Platform={Platform}, PushChannel={PushChannel}",
                    deviceInstallation?.InstallationId, deviceInstallation?.Platform, deviceInstallation?.PushChannel);
                return false;
            }

            var installation = new Installation
            {
                InstallationId = deviceInstallation.InstallationId,
                PushChannel = deviceInstallation.PushChannel,
                Tags = deviceInstallation.Tags?.Count > 0
                    ? new List<string>(deviceInstallation.Tags)
                    : new List<string> { "all" }, 
                Platform = _installationPlatform.TryGetValue(deviceInstallation.Platform.ToLower(), out var platform)
                    ? platform
                    : throw new ArgumentException($"Invalid platform: {deviceInstallation.Platform}")
            };

            try
            {
                await _hub.CreateOrUpdateInstallationAsync(installation, token);
                _logger.LogInformation("Successfully created/updated installation: {InstallationId}, Platform: {Platform}, Tags: {Tags}",
                    deviceInstallation.InstallationId, deviceInstallation.Platform, string.Join(",", installation.Tags));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create or update installation: {InstallationId}", deviceInstallation.InstallationId);
                return false;
            }
        }

        public async Task<(bool Success, string ErrorMessage)> ScheduleNotificationAsync(ScheduledNotificationRequest scheduledNotification, CancellationToken token)
        {
            if (scheduledNotification == null ||
                string.IsNullOrWhiteSpace(scheduledNotification.Message))
            {
                _logger.LogWarning("Invalid notification request: Message={Message}", scheduledNotification?.Message);
                return (false, "Message is required.");
            }

            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime currentET = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone);

            if (scheduledNotification.ScheduledTime <= currentET)
            {
                _logger.LogWarning("Scheduled time {ScheduledTime} is not in the future (current Eastern Time: {currentET})",
                    scheduledNotification.ScheduledTime, currentET);
                return (false, $"Scheduled time {scheduledNotification.ScheduledTime:yyyy-MM-dd HH:mm:ss} is not in the future.");
            }

            var androidPayload = $@"{{ ""data"": {{ ""message"": ""{scheduledNotification.Message}"" }} }}";
            var iOSPayload = $@"{{ ""aps"": {{ ""alert"": ""{scheduledNotification.Message}"", ""badge"": 1, ""sound"": ""default"" }} }}";

            // Convert DateTime to DateTimeOffset, assuming ScheduledTime is in UTC
            var deliveryTime = new DateTimeOffset(scheduledNotification.ScheduledTime, TimeSpan.Zero);

            try
            {
                // Check registrations
                var registrations = await _hub.GetAllRegistrationsAsync(100, token);
                int iosCount = registrations.OfType<AppleRegistrationDescription>().Count(r => r.Tags != null && r.Tags.Contains(""));
                int androidCount = registrations.OfType<FcmV1RegistrationDescription>().Count(r => r.Tags != null && (r.Tags.Contains("all") || r.Tags.Contains("")));
                _logger.LogInformation("Found {IosCount} iOS and {AndroidCount} Android registrations with 'all' tag", iosCount, androidCount);

                var tasks = new List<Task>();

                if (androidCount > 0)
                {
                    tasks.Add(_hub.ScheduleNotificationAsync(
                        new FcmV1Notification(androidPayload),
                        deliveryTime,
                        tags: new[] { "android" }, // Use "all" tag
                        token));
                    _logger.LogInformation("Scheduling FCM notification for {ScheduledTime} UTC", deliveryTime);
                }
                else
                {
                    _logger.LogWarning("No Android registrations found with 'all' tag; skipping FCM notification");
                }

                if (iosCount > 0)
                {
                    tasks.Add(_hub.ScheduleNotificationAsync(
                        new AppleNotification(iOSPayload),
                        deliveryTime,
                        tags: new[] { "ios" }, // Use "all" tag
                        token));
                    _logger.LogInformation("Scheduling APNs notification for {ScheduledTime} UTC", deliveryTime);
                }
                else
                {
                    _logger.LogWarning("No iOS registrations found with 'all' tag; skipping APNs notification");
                }

                if (tasks.Count == 0)
                {
                    _logger.LogWarning("No registered devices found with 'all' tag");
                    return (false, "No registered devices found with 'all' tag.");
                }

                await Task.WhenAll(tasks);

                _logger.LogInformation("Scheduled notification for {ScheduledTime} UTC to {PlatformCount} platform(s)", deliveryTime, tasks.Count);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule notification for {ScheduledTime} UTC: {ErrorMessage}",
                    deliveryTime, ex.Message);
                return (false, $"Azure Notification Hub error: {ex.Message}");
            }
        }
    }
}