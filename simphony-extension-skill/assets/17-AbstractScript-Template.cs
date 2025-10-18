using System;
using System.Linq;
using YourNamespace.Contracts;
using YourNamespace.Contracts.Clients;
using YourNamespace.Dependency;
using YourNamespace.Entities.Configuration;

namespace YourNamespace.Scripts
{
    /// <summary>
    /// Base class for all scripts - provides automatic method routing and configuration caching.
    /// Place in: Scripts/AbstractScript.cs
    ///
    /// Features:
    /// 1. Automatic method routing via reflection (no manual switch statements needed)
    /// 2. Configuration caching with 10-minute auto-refresh
    /// 3. Dual entry points: Execute (button clicks) and Event (Simphony events)
    /// 4. Flexible parameter support (0, 1, or 2 parameters)
    ///
    /// Usage:
    /// - All concrete scripts should inherit from this class
    /// - Create public methods for each function your script supports
    /// - Access configuration via the protected Config property
    /// </summary>
    public abstract class AbstractScript : IScript
    {
        private static YourConfig _config;

        /// <summary>
        /// Entry point for button clicks (string argument).
        /// Called from SimphonyExtensibilityApplication.CallFunc.
        /// </summary>
        public void Execute(string function, string argument) => Invoke(function, argument);

        /// <summary>
        /// Entry point for Simphony events (typed argument).
        /// Called from event handlers with typed event args.
        /// </summary>
        public void Event(string function, object argument) => Invoke(function, argument);

        /// <summary>
        /// Automatic method routing via reflection.
        /// Routes to public methods based on function name (case-insensitive).
        ///
        /// Routing logic:
        /// - If script has only 1 public method: calls it regardless of function name
        /// - If script has multiple public methods: matches by name (case-insensitive)
        /// - Supports methods with 0, 1, or 2 parameters
        /// </summary>
        private void Invoke(string function, object argument)
        {
            // Get all public methods (excluding object and AbstractScript methods)
            var methods = GetType()
                .GetMethods()
                .Where(x => x.DeclaringType != typeof(object) && x.DeclaringType != typeof(AbstractScript))
                .ToList();

            if (methods.Count == 0)
                throw new Exception("No public methods found in script");

            // If only one method, call it; otherwise find by name
            var method = methods.Count > 1
                ? methods.FirstOrDefault(x => string.Equals(x.Name, function, StringComparison.CurrentCultureIgnoreCase))
                : methods[0];

            if (method == null)
                throw new Exception($"Method {function} was not found in script");

            // Invoke with appropriate number of parameters
            switch (method.GetParameters().Length)
            {
                case 0:
                    method.Invoke(this, new object[0]);
                    break;
                case 1:
                    method.Invoke(this, new[] { argument });
                    break;
                case 2:
                    method.Invoke(this, new[] { function, argument });
                    break;
                default:
                    throw new Exception("Too many parameters in method");
            }
        }

        /// <summary>
        /// Cached configuration with automatic refresh.
        /// Configuration is cached for 10 minutes to avoid excessive reads.
        /// Access this property from your script methods to read configuration.
        /// </summary>
        protected YourConfig Config
        {
            get
            {
                // Refresh if null or older than 10 minutes
                if (_config == null || _config.ReadTime < DateTime.Now.AddMinutes(-10))
                    _config = DependencyManager.Resolve<IConfigurationClient>().ReadConfig();

                return _config;
            }
        }

        /// <summary>
        /// Force configuration refresh.
        /// Call this when you know configuration has changed and need immediate update.
        /// </summary>
        protected void RefreshConfig()
        {
            _config = null;
            var c = Config; // Trigger reload
        }
    }
}
