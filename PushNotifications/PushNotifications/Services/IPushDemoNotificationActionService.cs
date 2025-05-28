using PushNotifications.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushNotifications.Services
{
    public interface IPushDemoNotificationActionService : INotificationActionService
    {
        event EventHandler<PushDemoAction> ActionTriggered;
    }
}
