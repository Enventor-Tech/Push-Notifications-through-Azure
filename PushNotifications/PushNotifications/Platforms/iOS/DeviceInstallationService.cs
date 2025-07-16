using PushNotifications.Models;
using PushNotifications.Services;
using System;
using System.Collections.Generic;
using UIKit;
using Microsoft.Extensions.Logging;

namespace PushNotifications.Platforms.iOS
{
    public class DeviceInstallationService : IDeviceInstallationService
    {
        private readonly ILogger<DeviceInstallationService> _logger;

        const int SupportedVersionMajor = 16;
        const int SupportedVersionMinor = 0;

        private string _deviceIdCache;

        public DeviceInstallationService(ILogger<DeviceInstallationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets or sets the APNS token used as the push channel.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Indicates whether the current iOS version supports notifications.
        /// </summary>
        public bool NotificationsSupported =>
            UIDevice.CurrentDevice.CheckSystemVersion(SupportedVersionMajor, SupportedVersionMinor);

        /// <summary>
        /// Gets the device identifier.
        /// </summary>
        /// <returns>Device Identifier string</returns>
        public string GetDeviceId()
        {
            if (_deviceIdCache == null)
            {
                _deviceIdCache = UIDevice.CurrentDevice.IdentifierForVendor?.ToString();
                _logger.LogInformation("Fetched iOS device ID: {DeviceId}", _deviceIdCache);
            }
            return _deviceIdCache;
        }

        /// <summary>
        /// Creates a DeviceInstallation instance with current device info and tags.
        /// </summary>
        /// <param name="tags">Tags to assign</param>
        /// <returns>DeviceInstallation object</returns>
        public DeviceInstallation GetDeviceInstallation(params string[] tags)
        {
            if (!NotificationsSupported)
            {
                var error = GetNotificationsSupportError();
                _logger.LogError("Push notifications not supported: {Error}", error);
                throw new InvalidOperationException(error);
            }

            if (string.IsNullOrWhiteSpace(Token))
            {
                const string errMsg = "Unable to resolve token for APNS.";
                _logger.LogError(errMsg);
                throw new InvalidOperationException(errMsg);
            }

            var installation = new DeviceInstallation
            {
                InstallationId = GetDeviceId(),
                Platform = "apns",
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

        private string GetNotificationsSupportError()
        {
            if (!NotificationsSupported)
            {
                return $"This app only supports notifications on iOS {SupportedVersionMajor}.{SupportedVersionMinor} and above. You are running {UIDevice.CurrentDevice.SystemVersion}.";
            }

            if (Token == null)
            {
                return $"This app can support notifications but you must enable this in your settings.";
            }

            return "An error occurred preventing the use of push notifications.";
        }
    }
}
