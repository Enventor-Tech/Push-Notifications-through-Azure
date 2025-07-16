using PushNotificationWebAPI.Models;
using System.Threading;
using System.Threading.Tasks;

public interface INotificationService
{
    Task<bool> CreateOrUpdateInstallationAsync(DeviceInstallation deviceInstallation, CancellationToken token);

    Task<(bool Success, string ErrorMessage)> ScheduleNotificationAsync(ScheduledNotificationRequest scheduledNotification, CancellationToken token);

    Task<(bool Success, string ErrorMessage)> SendNotificationToInstallationAsync(string installationId, string platform, string message, CancellationToken token);
}
