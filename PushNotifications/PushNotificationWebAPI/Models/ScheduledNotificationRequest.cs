namespace PushNotificationWebAPI.Models
{
    public class ScheduledNotificationRequest
    {
        public string DeviceToken { get; set; }
        public string Message { get; set; }
        public DateTime ScheduledTime { get; set; }
    }
}/*using System.ComponentModel.DataAnnotations;

namespace PushNotificationWebAPI.Models
{
    public class ScheduledNotificationRequest
    {
        [Required] 
        public string Message { get; set; }
        
        [Required]
        public string DeviceToken { get; set; }

        [Required]
        public DateTime ScheduledTime { get; set; }
    }

}
*/