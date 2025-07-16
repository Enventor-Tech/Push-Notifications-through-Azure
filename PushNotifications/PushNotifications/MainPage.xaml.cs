using PushNotifications.Services;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

[assembly: Dependency(typeof(NotificationRegistrationService))]

namespace PushNotifications
{
    public partial class MainPage : ContentPage
    {
        private readonly INotificationRegistrationService _notificationRegistrationService;

        public MainPage()
        {
            InitializeComponent();

            _notificationRegistrationService = DependencyService.Get<INotificationRegistrationService>();
            if (_notificationRegistrationService == null)
            {
                Console.WriteLine("Warning: INotificationRegistrationService dependency not found!");
            }

            StartEasternTimeUpdate();
        }

#if ANDROID
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            }

            if (status != PermissionStatus.Granted)
            {
                ShowAlert("Notification permissions are required for this app to function properly.");
            }
            else
            {
                Console.WriteLine("Notification permissions granted.");
            }
        }
#endif

        private void StartEasternTimeUpdate()
        {
            Device.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var et = GetEasternTime();
                    EasternTimeLabel.Text = et.ToString("MMMM dd, yyyy - hh:mm:ss tt");
                });
                return true;
            });
        }

        private DateTime GetEasternTime()
        {
            // Windows uses "Eastern Standard Time", Linux/macOS "America/New_York"
            string tzId = OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York";
            var easternZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone);
        }

        private async void OnRegisterButtonClicked(object sender, EventArgs e)
        {
            try
            {
                var currentEasternTime = GetEasternTime();
                var originalTimeString = currentEasternTime.ToString("hh:mm tt");

                // Target time: next day at current time minus 45 minutes
                var easternZone = TimeZoneInfo.FindSystemTimeZoneById(
                    OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York");
                var tomorrow = currentEasternTime.Date.AddDays(1);
                var targetTime = tomorrow
                    .AddHours(currentEasternTime.Hour)
                    .AddMinutes(currentEasternTime.Minute)
                    .AddSeconds(currentEasternTime.Second)
                    .AddMinutes(-45);

                var utcTargetTime = TimeZoneInfo.ConvertTimeToUtc(targetTime, easternZone);

                Console.WriteLine($"[Register] Current ET: {currentEasternTime:yyyy-MM-dd HH:mm:ss}, Target ET: {targetTime:yyyy-MM-dd HH:mm:ss}, Target UTC: {utcTargetTime:yyyy-MM-dd HH:mm:ss}");

                if (_notificationRegistrationService == null)
                    throw new InvalidOperationException("NotificationRegistrationService not initialized.");

                await _notificationRegistrationService.RegisterDeviceAsync();

                // Slight delay to ensure registration propagates before scheduling
                await Task.Delay(500);

                await _notificationRegistrationService.ScheduleNotificationAsync(
                    $"Reminder: You scheduled this for {originalTimeString} yesterday.",
                    targetTime);

                ShowAlert($"Notification scheduled for tomorrow at {targetTime:hh:mm tt} ET.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnRegisterButtonClicked: {ex}");
                ShowAlert($"Error: {ex.Message}");
            }
        }

        private async void OnSendImmediateNotificationClicked(object sender, EventArgs e)
        {
            try
            {
                if (_notificationRegistrationService == null)
                    throw new InvalidOperationException("NotificationRegistrationService not initialized.");

                var message = $"Immediate notification sent at {DateTime.Now:hh:mm:ss tt}";

                await _notificationRegistrationService.SendImmediateNotificationAsync(message);

                Console.WriteLine("[Immediate Notification] Sent message: " + message);

                ShowAlert("Immediate notification request sent to Azure.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending immediate notification: {ex}");
                ShowAlert($"Error sending immediate notification: {ex.Message}");
            }
        }

        private void ShowAlert(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisplayAlert("Push Notifications Demo", message, "OK")
                    .ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                        {
                            Console.WriteLine($"DisplayAlert exception: {task.Exception}");
                        }
                    });
            });
        }
    }
}
