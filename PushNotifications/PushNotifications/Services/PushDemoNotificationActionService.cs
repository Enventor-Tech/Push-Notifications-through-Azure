using PushNotifications.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace PushNotifications.Services
{
    public class PushDemoNotificationActionService : IPushDemoNotificationActionService
    {
        private readonly Dictionary<string, PushDemoAction> _actionMappings = new()
        {
            { "action_a", PushDemoAction.ActionA },
            { "action_b", PushDemoAction.ActionB }
        };

        private readonly ILogger<PushDemoNotificationActionService> _logger;

        public PushDemoNotificationActionService(ILogger<PushDemoNotificationActionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public event EventHandler<PushDemoAction> ActionTriggered = delegate { };

        public void TriggerAction(string action)
        {
            if (!_actionMappings.TryGetValue(action, out var pushDemoAction))
            {
                _logger.LogWarning("Attempted to trigger unknown action '{Action}' - ignoring.", action);
                return;
            }

            _logger.LogInformation("Triggering action '{Action}' mapped to enum value {PushDemoAction}", action, pushDemoAction);

            var exceptions = new List<Exception>();

            // Get a snapshot of the invocation list for thread-safety
            var handlers = ActionTriggered?.GetInvocationList();

            if (handlers == null || handlers.Length == 0)
            {
                _logger.LogInformation("No subscribers found for action '{Action}'.", action);
                return;
            }

            foreach (var handler in handlers)
            {
                try
                {
                    handler.DynamicInvoke(this, pushDemoAction);
                    _logger.LogDebug("Handler {Handler} executed successfully for action '{Action}'.", handler.Method.Name, action);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception caught invoking handler {Handler} for action '{Action}'.", handler.Method.Name, action);
                    exceptions.Add(ex);
                }
            }

            if (exceptions.Any())
            {
                _logger.LogError("AggregateException throwing due to {Count} exceptions when triggering action '{Action}'.", exceptions.Count, action);
                throw new AggregateException(exceptions);
            }
        }
    }
}
