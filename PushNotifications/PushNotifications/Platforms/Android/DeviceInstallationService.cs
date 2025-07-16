using Android.Gms.Common;
using Android.Provider;
using Microsoft.Extensions.Logging;
using PushNotifications.Models;
using PushNotifications.Services;
using System;
using System.Collections.Generic;
using static Android.Provider.Settings;

namespace PushNotifications.Platforms.Android
{
    public class DeviceInstallationService : IDeviceInstallationService
    {
        private readonly ILogger<DeviceInstallationService> _logger;
        private string _deviceIdCache;

        public DeviceInstallationService(ILogger<DeviceInstallationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets or sets the FCMv1 token used as the push channel.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Checks if Google Play Services are available, so push notifications are supported.
        /// </summary>
        public bool NotificationsSupported
        {
            get
            {
                var availability = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(Platform.AppContext);
                bool supported = availability == ConnectionResult.Success;
                _logger.LogDebug("Google Play Services availability: {Availability}, supported: {Supported}", availability, supported);
                return supported;
            }
        }

        /// <summary>
        /// Gets the unique Android device ID.
        /// </summary>
        /// <returns>Device ID string</returns>
        public string GetDeviceId()
        {
            if (_deviceIdCache == null)
            {
                _deviceIdCache = Secure.GetString(Platform.AppContext.ContentResolver, Secure.AndroidId);
                _logger.LogInformation("Fetched Android device ID: {DeviceId}", _deviceIdCache);
            }
            return _deviceIdCache;
        }

        /// <summary>
        /// Builds a DeviceInstallation object with installation ID, platform, push channel, and tags.
        /// </summary>
        /// <param name="tags">Tags associated with the installation</param>
        /// <returns>DeviceInstallation instance</returns>
        public DeviceInstallation GetDeviceInstallation(params string[] tags)
        {
            if (!NotificationsSupported)
            {
                var error = GetPlayServicesError();
                _logger.LogError("Push notifications not supported: {Error}", error);
                throw new InvalidOperationException(error);
            }

            if (string.IsNullOrWhiteSpace(Token))
            {
                const string errMsg = "Unable to resolve token for FCMv1.";
                _logger.LogError(errMsg);
                throw new InvalidOperationException(errMsg);
            }

            var installation = new DeviceInstallation
            {
                InstallationId = GetDeviceId(),
                Platform = "fcmv1",
                PushChannel = Token
            };

            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        installation.Tags.Add(tag);
                    }
                }
            }

            _logger.LogInformation("Created DeviceInstallation with InstallationId: {InstallationId}, Platform: {Platform}, Tags: {Tags}",
                installation.InstallationId, installation.Platform, string.Join(", ", installation.Tags));

            return installation;
        }

        private string GetPlayServicesError()
        {
            int resultCode = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(Platform.AppContext);

            if (resultCode != ConnectionResult.Success)
            {
                var errorMsg = GoogleApiAvailability.Instance.IsUserResolvableError(resultCode) ?
                    GoogleApiAvailability.Instance.GetErrorString(resultCode) :
                    "This device isn't supported.";

                return errorMsg;
            }

            return "An error occurred preventing the use of push notifications.";
        }
    }
}
