namespace PushNotifications.Models
{
    public class ScheduleNotification
    {
        public string DeviceToken { get; set; }
        public string Message { get; set; }
        public DateTime ScheduledTime { get; set; }
    }
}