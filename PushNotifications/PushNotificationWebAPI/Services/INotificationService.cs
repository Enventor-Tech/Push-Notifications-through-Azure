using PushNotificationWebAPI.Models;

public interface INotificationService
{
    Task<bool> CreateOrUpdateInstallationAsync(DeviceInstallation deviceInstallation, CancellationToken token);
    Task<(bool Success, string ErrorMessage)> ScheduleNotificationAsync(ScheduledNotificationRequest scheduledNotification, CancellationToken token);
}