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
    StartEasternTimeUpdate();
}

#if ANDROID
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            PermissionStatus status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                ShowAlert("Notification permissions are required for this app.");
            }
        }
#endif

        private void StartEasternTimeUpdate()
        {
            Device.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    EasternTimeLabel.Text = GetEasternTime().ToString("MMMM dd, yyyy - hh:mm:ss tt");
                });
                return true;
            });
        }

        private DateTime GetEasternTime()
        {
            var easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone);
        }

        private async void OnRegisterButtonClicked(object sender, EventArgs e)
        {
            try
            {
                var currentEasternTime = GetEasternTime();
                var originalTimeString = currentEasternTime.ToString("hh:mm tt");

                // Calculate target time: next day at current time minus 45 minutes
                var easternZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
                var tomorrow = currentEasternTime.Date.AddDays(1);
                var targetTime = tomorrow
                    .AddHours(currentEasternTime.Hour)
                    .AddMinutes(currentEasternTime.Minute)
                    .AddSeconds(currentEasternTime.Second)
                    .AddMinutes(-45);

                // Log for debugging
                var utcTargetTime = TimeZoneInfo.ConvertTimeToUtc(targetTime, easternZone);
                Console.WriteLine($"Current ET: {currentEasternTime:yyyy-MM-dd HH:mm:ss}, Target ET: {targetTime:yyyy-MM-dd HH:mm:ss}, Target UTC: {utcTargetTime:yyyy-MM-dd HH:mm:ss}");

                await _notificationRegistrationService.RegisterDeviceAsync();
                await Task.Delay(500); // Allow registration to propagate
                await _notificationRegistrationService.ScheduleNotificationAsync(
                    $"Reminder: You scheduled this for {originalTimeString} yesterday.",
                    targetTime);

                ShowAlert($"Notification scheduled for tomorrow at {targetTime.ToString("hh:mm tt")} ET.");
            }
            catch (Exception ex)
            {
                ShowAlert($"Error: {ex.Message}");
            }
        }

        private void ShowAlert(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisplayAlert("Push Notifications Demo", message, "OK")
                    .ContinueWith((task) =>
                    {
                        if (task.IsFaulted)
                            throw task.Exception;
                    });
            });
        }
    }
}
/*using PushNotifications.Services;
using PushNotifications.ViewModels;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PushNotifications
{
    public partial class MainPage : ContentPage
    {
        readonly INotificationRegistrationService _notificationRegistrationService;
#if ANDROID
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            PermissionStatus status = await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
#endif
        public MainPage(INotificationRegistrationService service)
        {
            InitializeComponent();
            BindingContext = new MainPageViewModel();
            _notificationRegistrationService = service;
        }
        void OnRegisterButtonClicked(object sender, EventArgs e)
        {
            _notificationRegistrationService.RegisterDeviceAsync()
                .ContinueWith((task) =>
                {
                    ShowAlert(task.IsFaulted ? task.Exception.Message : $"Device registered");
                });
        }

        void OnDeregisterButtonClicked(object sender, EventArgs e)
        {
            _notificationRegistrationService.DeregisterDeviceAsync()
                .ContinueWith((task) =>
                {
                    ShowAlert(task.IsFaulted ? task.Exception.Message : $"Device deregistered");
                });
        }
        void OnScheduleNotificationClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageEntry.Text) || string.IsNullOrWhiteSpace(MinutesBeforeEntry.Text))
            {
                ShowAlert("Please enter a message and minutes before.");
                return;
            }

            if (!int.TryParse(MinutesBeforeEntry.Text, out int minutesBefore) || minutesBefore < 0)
            {
                ShowAlert("Please enter a valid number of minutes.");
                return;
            }

            _notificationRegistrationService.ScheduleNotificationAsync(MessageEntry.Text, minutesBefore)
                .ContinueWith((task) =>
                {
                    ShowAlert(task.IsFaulted
                        ? task.Exception.Message
                        : $"Notification scheduled for tomorrow at {CalculateScheduledTime(minutesBefore):hh:mm tt}.");
                });
        }

        void OnUpdateNotificationClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageEntry.Text) || string.IsNullOrWhiteSpace(MinutesBeforeEntry.Text))
            {
                ShowAlert("Please enter a message and minutes before.");
                return;
            }

            if (!int.TryParse(MinutesBeforeEntry.Text, out int newMinutesBefore) || newMinutesBefore < 0)
            {
                ShowAlert("Please enter a valid number of minutes.");
                return;
            }

            _notificationRegistrationService.UpdateNotificationAsync(MessageEntry.Text, newMinutesBefore)
                .ContinueWith((task) =>
                {
                    ShowAlert(task.IsFaulted
                        ? task.Exception.Message
                        : $"Notification updated to tomorrow at {CalculateScheduledTime(newMinutesBefore):hh:mm tt}.");
                });
        }


        void ShowAlert(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisplayAlert("Push notifications demo", message, "OK")
                    .ContinueWith((task) =>
                    {
                        if (task.IsFaulted)
                            throw task.Exception;
                    });
            });
        }

        DateTime CalculateScheduledTime(int minutesBefore)
        {
            var now = DateTime.Now; // Local time (PKT)
            var tomorrow = now.AddDays(1);
            return new DateTime(
                tomorrow.Year, tomorrow.Month, tomorrow.Day,
                now.Hour, now.Minute, now.Second).AddMinutes(-minutesBefore);
        }
        
        async void OnSendBroadcastClicked(object sender, EventArgs e)
{
    try
    {
        var cachedToken = await SecureStorage.GetAsync("cached_device_token");

        // If not registered, register first
        if (string.IsNullOrWhiteSpace(cachedToken))
        {
            await _notificationRegistrationService.RegisterDeviceAsync();
            await Task.Delay(500); // Small delay to ensure registration propagates
        }

        await _notificationRegistrationService.SendBroadcastNotificationAsync("Hello, this is a broadcast!");
        ShowAlert("Broadcast sent successfully.");
    }
    catch (Exception ex)
    {
        ShowAlert($"Broadcast failed: {ex.Message}");
    }
}


    }
}
*/
