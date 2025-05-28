using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PushNotifications.Models
{
    public class DeviceInstallation
    {
        [JsonPropertyName("installationId")]
        public string InstallationId { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; }

        [JsonPropertyName("pushChannel")]
        public string PushChannel { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new List<string>();
    }
}
