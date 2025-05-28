using PushNotifications.Models;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PushNotifications.Services
{
    public class NotificationRegistrationService : INotificationRegistrationService
    {
        private const string RequestUrl = "api/notifications/installations";
        private const string ScheduleUrl = "api/notifications/schedule";
        private const string CachedDeviceTokenKey = "cached_device_token";
        const string CachedTagsKey = "cached_tags";

        private readonly string _baseApiUrl;
        private readonly HttpClient _client;
        IDeviceInstallationService _deviceInstallationService;

        IDeviceInstallationService DeviceInstallationService =>
            _deviceInstallationService ?? (_deviceInstallationService = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<IDeviceInstallationService>());

        public NotificationRegistrationService(string baseApiUri, string apiKey)
        {
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("Accept", "application/json");
            _client.DefaultRequestHeaders.Add("apikey", apiKey);
            _baseApiUrl = baseApiUri;
            _deviceInstallationService = Application.Current?.Windows[0]?.Page?.Handler?.MauiContext?.Services?.GetService<IDeviceInstallationService>();
        }

        /* public async Task RegisterDeviceAsync(params string[] tags)
         {
             var deviceInstallation = DeviceInstallationService?.GetDeviceInstallation(tags);

             await SendAsync<DeviceInstallation>(HttpMethod.Put, RequestUrl, deviceInstallation)
                 .ConfigureAwait(false);

             await SecureStorage.SetAsync(CachedDeviceTokenKey, deviceInstallation.PushChannel)
                 .ConfigureAwait(false);

             await SecureStorage.SetAsync(CachedTagsKey, JsonSerializer.Serialize(tags));
         }
 */
        public async Task ScheduleNotificationAsync(string message, DateTime scheduledTime)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Notification message cannot be empty.", nameof(message));

            var cachedToken = await SecureStorage.GetAsync(CachedDeviceTokenKey);
            if (string.IsNullOrWhiteSpace(cachedToken))
                throw new InvalidOperationException("Device not registered. Please register the device first.");

            var easternZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            var utcScheduledTime = TimeZoneInfo.ConvertTimeToUtc(scheduledTime, easternZone);

            // Add 1-minute buffer
            utcScheduledTime = utcScheduledTime.AddMinutes(1);

            if (utcScheduledTime <= DateTime.UtcNow.AddSeconds(30))
            {
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
                    Console.WriteLine($"Attempt {attempt}: Sending schedule request: {JsonSerializer.Serialize(request)}");
                    await SendAsync(HttpMethod.Post, ScheduleUrl, request);
                    return;
                }
                catch (HttpRequestException ex)
                {
                    string errorContent = ex.Message;
                    if (ex.Data.Contains("Response") && ex.Data["Response"] is HttpResponseMessage response)
                    {
                        errorContent = await response.Content.ReadAsStringAsync();
                    }
                    if (attempt == maxRetries)
                    {
                        throw new Exception($"Failed to schedule notification after {maxRetries} attempts: {errorContent}", ex);
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

            var response = await _client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var ex = new HttpRequestException($"Request failed with status {response.StatusCode}");
                ex.Data["Response"] = response;
                throw ex;
            }
        }
        public async Task RegisterDeviceAsync(params string[] tags)
        {
            var deviceInstallation = DeviceInstallationService?.GetDeviceInstallation(tags);

            await SendAsync<DeviceInstallation>(HttpMethod.Put, RequestUrl, deviceInstallation)
                .ConfigureAwait(false);

            await SecureStorage.SetAsync(CachedDeviceTokenKey, deviceInstallation.PushChannel)
                .ConfigureAwait(false);

            await SecureStorage.SetAsync(CachedTagsKey, JsonSerializer.Serialize(tags));
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
                return;

            var tags = JsonSerializer.Deserialize<string[]>(serializedTags);

            await RegisterDeviceAsync(tags);
        }
    }
}