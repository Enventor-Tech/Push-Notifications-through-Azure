using PushNotifications.Models;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PushNotifications.Services
{
    public class NotificationRegistrationService : INotificationRegistrationService
    {
        private const string RequestUrl = "api/notifications/installations";
        private const string ScheduleUrl = "api/notifications/schedule";
        private const string CachedDeviceTokenKey = "cached_device_token";
        private const string CachedTagsKey = "cached_tags";

        private readonly string _baseApiUrl;
        private readonly HttpClient _client;
        private readonly ILogger<NotificationRegistrationService> _logger;
        private IDeviceInstallationService _deviceInstallationService;

        IDeviceInstallationService DeviceInstallationService =>
            _deviceInstallationService ?? (_deviceInstallationService = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<IDeviceInstallationService>());

        public NotificationRegistrationService(string baseApiUri, string apiKey, ILogger<NotificationRegistrationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("Accept", "application/json");
            _client.DefaultRequestHeaders.Add("apikey", apiKey);
            _baseApiUrl = baseApiUri;
            _deviceInstallationService = Application.Current?.Windows[0]?.Page?.Handler?.MauiContext?.Services?.GetService<IDeviceInstallationService>();
        }

        public async Task ScheduleNotificationAsync(string message, DateTime scheduledTime)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Notification message cannot be empty.", nameof(message));

            var cachedToken = await SecureStorage.GetAsync(CachedDeviceTokenKey);
            if (string.IsNullOrWhiteSpace(cachedToken))
            {
                _logger.LogWarning("Device not registered - no cached token found.");
                throw new InvalidOperationException("Device not registered. Please register the device first.");
            }

            var easternZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            var utcScheduledTime = TimeZoneInfo.ConvertTimeToUtc(scheduledTime, easternZone);

            // Add 1-minute buffer
            utcScheduledTime = utcScheduledTime.AddMinutes(1);

            if (utcScheduledTime <= DateTime.UtcNow.AddSeconds(30))
            {
                _logger.LogWarning("Scheduled time {ScheduledTime} UTC is in the past or too soon", utcScheduledTime);
                throw new ArgumentException($"Scheduled time {utcScheduledTime:yyyy-MM-dd HH:mm:ss} UTC is in the past or too soon.", nameof(scheduledTime));
            }

            var request = new ScheduleNotification
            {
                DeviceToken = cachedToken,
                Message = message,
                ScheduledTime = utcScheduledTime
            };

            const int maxRetries = 2;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Attempt {Attempt}: Sending schedule notification request: {Request}", attempt, JsonSerializer.Serialize(request));
                    await SendAsync(HttpMethod.Post, ScheduleUrl, request);
                    _logger.LogInformation("Successfully scheduled notification.");
                    return;
                }
                catch (HttpRequestException ex)
                {
                    string errorContent = ex.Message;
                    if (ex.Data.Contains("Response") && ex.Data["Response"] is HttpResponseMessage response)
                    {
                        errorContent = await response.Content.ReadAsStringAsync();
                    }

                    _logger.LogWarning(ex, "Attempt {Attempt} failed to schedule notification: {ErrorContent}", attempt, errorContent);

                    if (attempt == maxRetries)
                    {
                        _logger.LogError(ex, "Failed to schedule notification after {MaxRetries} attempts.", maxRetries);
                        throw new Exception($"Failed to schedule notification after {maxRetries} attempts: {errorContent}", ex);
                    }

                    await Task.Delay(1000);
                }
            }
        }

        public async Task RegisterAndNotifyAsync(params string[] tags)
        {
            var deviceInstallation = DeviceInstallationService?.GetDeviceInstallation(tags);

            if (deviceInstallation == null ||
                string.IsNullOrWhiteSpace(deviceInstallation.InstallationId) ||
                string.IsNullOrWhiteSpace(deviceInstallation.Platform) ||
                string.IsNullOrWhiteSpace(deviceInstallation.PushChannel))
            {
                _logger.LogError("Invalid device installation information.");
                throw new ArgumentException("Invalid device installation information.");
            }

            const int maxRetries = 2;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Attempt {Attempt}: Sending register-and-notify request: {Request}", attempt, JsonSerializer.Serialize(deviceInstallation));
                    await SendAsync(HttpMethod.Post, "api/notifications/register-and-notify", deviceInstallation);

                    await SecureStorage.SetAsync(CachedDeviceTokenKey, deviceInstallation.PushChannel);
                    await SecureStorage.SetAsync(CachedTagsKey, JsonSerializer.Serialize(tags));

                    _logger.LogInformation("Device registered and notified successfully.");
                    return;
                }
                catch (HttpRequestException ex)
                {
                    string errorContent = ex.Message;
                    if (ex.Data.Contains("Response") && ex.Data["Response"] is HttpResponseMessage response)
                    {
                        errorContent = await response.Content.ReadAsStringAsync();
                    }

                    _logger.LogWarning(ex, "Attempt {Attempt} failed to register and notify: {ErrorContent}", attempt, errorContent);

                    if (attempt == maxRetries)
                    {
                        _logger.LogError(ex, "Failed to register and notify after {MaxRetries} attempts.", maxRetries);
                        throw new Exception($"Failed to register and notify after {maxRetries} attempts: {errorContent}", ex);
                    }

                    await Task.Delay(1000);
                }
            }
        }

        public async Task SendImmediateNotificationAsync(string message)
        {
            var cachedToken = await SecureStorage.GetAsync(CachedDeviceTokenKey);
            if (string.IsNullOrWhiteSpace(cachedToken))
            {
                _logger.LogWarning("Device not registered - no cached token found.");
                throw new InvalidOperationException("Device not registered. Please register the device first.");
            }

            var request = new
            {
                DeviceToken = cachedToken,
                Message = message,
                ScheduledTime = DateTime.UtcNow // Not used for immediate, but required DTO field
            };

            const int maxRetries = 2;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Attempt {Attempt}: Sending immediate notification request: {Request}", attempt, JsonSerializer.Serialize(request));
                    await SendAsync(HttpMethod.Post, "api/notifications/send-immediate", request);
                    _logger.LogInformation("Immediate notification sent successfully.");
                    return;
                }
                catch (HttpRequestException ex)
                {
                    string errorContent = ex.Message;
                    if (ex.Data.Contains("Response") && ex.Data["Response"] is HttpResponseMessage response)
                    {
                        errorContent = await response.Content.ReadAsStringAsync();
                    }

                    _logger.LogWarning(ex, "Attempt {Attempt} failed to send immediate notification: {ErrorContent}", attempt, errorContent);

                    if (attempt == maxRetries)
                    {
                        _logger.LogError(ex, "Failed to send immediate notification after {MaxRetries} attempts.", maxRetries);
                        throw new Exception($"Failed to send immediate notification after {maxRetries} attempts: {errorContent}", ex);
                    }

                    await Task.Delay(1000);
                }
            }
        }

        private async Task SendAsync<T>(HttpMethod requestType, string requestUri, T obj)
        {
            string serializedContent = JsonSerializer.Serialize(obj);
            await SendAsync(requestType, requestUri, serializedContent);
        }

        private async Task SendAsync(HttpMethod requestType, string requestUri, string jsonRequest = null)
        {
            var request = new HttpRequestMessage(requestType, new Uri($"{_baseApiUrl}{requestUri}"));
            if (jsonRequest != null)
                request.Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            _logger.LogDebug("Sending HTTP {Method} request to {Uri} with content: {Content}", requestType, requestUri, jsonRequest ?? "<no content>");

            var response = await _client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("HTTP request to {Uri} failed with status {StatusCode}: {ErrorContent}", requestUri, response.StatusCode, errorContent);

                var ex = new HttpRequestException($"Request failed with status {response.StatusCode}");
                ex.Data["Response"] = response;
                throw ex;
            }
            else
            {
                _logger.LogDebug("HTTP request to {Uri} succeeded with status {StatusCode}", requestUri, response.StatusCode);
            }
        }

        public async Task RegisterDeviceAsync(params string[] tags)
        {
            var deviceInstallation = DeviceInstallationService?.GetDeviceInstallation(tags);

            _logger.LogInformation("Registering device with tags: {Tags}", string.Join(",", tags));

            await SendAsync<DeviceInstallation>(HttpMethod.Put, RequestUrl, deviceInstallation)
                .ConfigureAwait(false);

            await SecureStorage.SetAsync(CachedDeviceTokenKey, deviceInstallation.PushChannel)
                .ConfigureAwait(false);

            await SecureStorage.SetAsync(CachedTagsKey, JsonSerializer.Serialize(tags));

            _logger.LogInformation("Device registered successfully with InstallationId: {InstallationId}", deviceInstallation.InstallationId);
        }

        public async Task RefreshRegistrationAsync()
        {
            var cachedToken = await SecureStorage.GetAsync(CachedDeviceTokenKey)
                .ConfigureAwait(false);

            var serializedTags = await SecureStorage.GetAsync(CachedTagsKey)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(cachedToken) ||
                string.IsNullOrWhiteSpace(serializedTags) ||
                string.IsNullOrWhiteSpace(_deviceInstallationService.Token) ||
                cachedToken == DeviceInstallationService.Token)
            {
                _logger.LogInformation("No registration refresh needed: cached token or tags missing or token unchanged.");
                return;
            }

            var tags = JsonSerializer.Deserialize<string[]>(serializedTags);

            _logger.LogInformation("Refreshing device registration with tags: {Tags}", string.Join(",", tags));

            await RegisterDeviceAsync(tags);
        }
    }
}
