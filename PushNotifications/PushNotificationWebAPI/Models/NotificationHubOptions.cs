namespace PushNotificationWebAPI.Models
{
    public class NotificationHubOptions
    {
        public string ConnectionString { get; set; }
        public string Name { get; set; }
    }
}
/*using System.ComponentModel.DataAnnotations;

namespace PushNotificationWebAPI.Models
{
    public class NotificationHubOptions
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string ConnectionString { get; set; }
    }
}
*/