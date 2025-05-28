namespace PushNotifications.Services;
public interface INotificationRegistrationService
{
   
    Task RefreshRegistrationAsync();
    Task RegisterDeviceAsync(params string[] tags);
    Task ScheduleNotificationAsync(string message, DateTime scheduledTime);
}