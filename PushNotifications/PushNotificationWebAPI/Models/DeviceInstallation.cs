namespace PushNotificationWebAPI.Models
{
    public class DeviceInstallation
    {
        public string InstallationId { get; set; }
        public string Platform { get; set; }
        public string PushChannel { get; set; }
        public IList<string> Tags { get; set; }
    }
}
/*using System.ComponentModel.DataAnnotations;

namespace PushNotificationWebAPI.Models
{
    public class DeviceInstallation
    {
        [Required]
        public string InstallationId { get; set; }

        [Required]
        public string Platform { get; set; }

        [Required]
        public string PushChannel { get; set; }

        public IList<string> Tags { get; set; } = Array.Empty<string>();
    }
}
*/