// Essential Helper Classes
// Universal Mandatory Patterns
// Location: Helpers/

using System;
using System.Linq;
using System.Reflection;
using Micros.OpsUI.Controls;

namespace YourNamespace.Helpers
{
    // ========================================================================
    // EXCEPTION HELPER - GetFirstException (RECOMMENDED)
    // ========================================================================
    // Location: Helpers/ExceptionHelper.cs
    // Validated: Projects 1, 3, 4 use this (2/3 = best practice)
    // Do NOT use GetRootException (Project 5 outlier)

    /// <summary>
    /// Unwraps common wrapper exceptions to get meaningful error messages.
    /// Unwraps: AggregateException, TargetInvocationException
    /// Preserves: All other exception types (important context)
    /// </summary>
    public static class ExceptionHelper
    {
        /// <summary>
        /// Recursively unwraps AggregateException and TargetInvocationException.
        /// Returns the first meaningful exception for better error messages.
        /// </summary>
        /// <param name="e">Exception to unwrap</param>
        /// <returns>First non-wrapper exception</returns>
        /// <example>
        /// try
        /// {
        ///     // Code that throws
        /// }
        /// catch (Exception e)
        /// {
        ///     var first = ExceptionHelper.GetFirstException(e);
        ///     _logger.LogException("Operation failed", first);
        ///     OpsContext.ShowError(first.Message);
        /// }
        /// </example>
        public static Exception GetFirstException(Exception e)
        {
            // Unwrap AggregateException (from parallel/async operations)
            if (e is AggregateException aggregateException && aggregateException.InnerExceptions.Any())
                return GetFirstException(aggregateException.InnerExceptions.First());

            // Unwrap TargetInvocationException (from reflection)
            if (e is TargetInvocationException targetInvocationException && targetInvocationException.InnerException != null)
                return GetFirstException(targetInvocationException.InnerException);

            // Return actual exception
            return e;
        }
    }

    // ========================================================================
    // SIMPHONY OPS COMMAND ARGUMENTS PARSER
    // ========================================================================
    // Location: Helpers/SimphonyOpsCommandArguments.cs

    /// <summary>
    /// Parses Simphony button OpsCommandArguments string.
    /// Format: "Script=ScriptName;Function=FunctionName;Argument=Value"
    /// </summary>
    public class SimphonyOpsCommandArguments
    {
        public string Script { get; set; }
        public string Function { get; set; }
        public string Argument { get; set; }

        /// <summary>
        /// Parses OpsCommandArguments from Simphony button.
        /// </summary>
        /// <param name="opsCommandArguments">Raw command string from button</param>
        /// <returns>Parsed arguments</returns>
        /// <example>
        /// // Button configured with: "Script=Loyalty;Function=;Argument=CustomerId"
        /// var args = SimphonyOpsCommandArguments.Parse(buttonArgs);
        /// // args.Script = "Loyalty"
        /// // args.Function = ""
        /// // args.Argument = "CustomerId"
        /// </example>
        public static SimphonyOpsCommandArguments Parse(string opsCommandArguments)
        {
            var result = new SimphonyOpsCommandArguments();

            if (string.IsNullOrEmpty(opsCommandArguments))
                return result;

            var parts = opsCommandArguments.Split(';');

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                var keyValue = part.Split('=');
                if (keyValue.Length != 2)
                    continue;

                var key = keyValue[0].Trim();
                var value = keyValue[1].Trim();

                switch (key)
                {
                    case "Script":
                        result.Script = value;
                        break;
                    case "Function":
                        result.Function = value;
                        break;
                    case "Argument":
                        result.Argument = value;
                        break;
                }
            }

            return result;
        }
    }

    // ========================================================================
    // SIMPHONY EVENT HELPER
    // ========================================================================
    // Location: Helpers/SimphonyEventHelper.cs

    using Micros.PosCore.Extensibility;
    using YourNamespace.Contracts;
    using YourNamespace.Contracts.Clients;
    using YourNamespace.Contracts.Logging;
    using YourNamespace.Dependency;
    using YourNamespace.Entities;

    /// <summary>
    /// Helper for registering and routing Simphony POS events.
    /// Provides methods for common event types.
    /// </summary>
    public class SimphonyEventHelper
    {
        /// <summary>
        /// Registers all event callbacks.
        /// Called from SimphonyExtensibilityApplication constructor.
        /// </summary>
        public void RegisterCallBacks(OpsExtensibilityApplication application)
        {
            // This method is called to register events
            // Implementation depends on event registration approach:
            // 1. Inline (simple projects)
            // 2. Hybrid array (medium projects)
            // 3. Configuration-driven (complex projects)

            // See event registration examples below
        }

        /// <summary>
        /// Register a single event with inline approach.
        /// </summary>
        public static void Register(OpsExtensibilityApplication app, string eventName, string scriptName, string functionName = "")
        {
            var logger = DependencyManager.Resolve<ILogManager>();

            switch (eventName)
            {
                case "BeginCheckPreviewEvent":
                    app.BeginCheckPreviewEvent += (sender, args) =>
                        ExecuteScript(scriptName, functionName, args, logger);
                    break;

                case "ProcessCheckEvent":
                    app.ProcessCheckEvent += (sender, args) =>
                        ExecuteScript(scriptName, functionName, args, logger);
                    break;

                case "CloseCheckEvent":
                    app.CloseCheckEvent += (sender, args) =>
                        ExecuteScript(scriptName, functionName, args, logger);
                    break;

                // Add more events as needed
                default:
                    throw new Exception($"Unknown event: {eventName}");
            }
        }

        /// <summary>
        /// Convenience method for ServiceChargePreviewEvent.
        /// </summary>
        public static void RegisterServiceChargePreviewEvent(OpsExtensibilityApplication app, string handlerName)
        {
            app.ServiceChargePreviewEvent += (sender, args) =>
            {
                var handler = DependencyManager.Resolve<IEventHandler>(handlerName);
                var eventArgs = new OpsServiceChargePreviewArgs(args);
                handler.ProcessEvent(eventArgs);
            };
        }

        /// <summary>
        /// Convenience method for TenderMediaVoidEvent.
        /// </summary>
        public static void RegisterTenderMediaVoidEvent(OpsExtensibilityApplication app, string handlerName)
        {
            app.TenderMediaVoidEvent += (sender, args) =>
            {
                var handler = DependencyManager.Resolve<IEventHandler>(handlerName);
                var eventArgs = new OpsTenderMediaVoidArgs(args);
                handler.ProcessEvent(eventArgs);
            };
        }

        private static void ExecuteScript(string scriptName, string functionName, object args, ILogManager logger)
        {
            try
            {
                var script = DependencyManager.Resolve<IScript>(scriptName);

                // For IScript with Event() method
                // script.Event();

                // For IScript with Execute(functionName, argument)
                script.Execute(functionName, SerializeArgs(args));
            }
            catch (Exception e)
            {
                var first = ExceptionHelper.GetFirstException(e);
                logger.LogException($"Error handling event in script {scriptName}", first);
            }
        }

        private static string SerializeArgs(object args)
        {
            // Serialize event arguments to string if needed
            // Or return empty string if not used
            return string.Empty;
        }
    }

    // ========================================================================
    // VERSION HELPER
    // ========================================================================
    // Location: Helpers/VersionHelper.cs

    /// <summary>
    /// Provides version and build information.
    /// </summary>
    public static class VersionHelper
    {
        /// <summary>
        /// Gets the assembly version.
        /// </summary>
        public static string Version
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        /// <summary>
        /// Gets the assembly name.
        /// </summary>
        public static string Name
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                return assembly.GetName().Name;
            }
        }

        /// <summary>
        /// Gets the integration version (name + version).
        /// </summary>
        public static string IntegrationVersion => $"{Name} v{Version}";

        /// <summary>
        /// Gets name and version formatted for display.
        /// </summary>
        public static string NameAndVersion => $"{Name} {Version}";

        /// <summary>
        /// Gets build date/time from assembly.
        /// </summary>
        public static DateTime BuildDateTime
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var buildDate = new FileInfo(assembly.Location).LastWriteTime;
                return buildDate;
            }
        }
    }
}

// ============================================================================
// ENTITIES
// ============================================================================

namespace YourNamespace.Entities
{
    // ========================================================================
    // STATUS
    // ========================================================================
    // Location: Entities/Status.cs

    /// <summary>
    /// Tracks application status and workstation information.
    /// Registered as singleton in DependencyManager.
    /// </summary>
    public class Status
    {
        /// <summary>
        /// Simphony workstation ID.
        /// Set in SimphonyExtensibilityApplication constructor.
        /// </summary>
        public int WorkstationId { get; set; }

        /// <summary>
        /// Service host ID for network services (optional).
        /// </summary>
        public int? ServiceHostId { get; set; }

        /// <summary>
        /// Indicates if ExtensionApplicationService started (optional).
        /// </summary>
        public bool ExtensionApplicationServiceStarted { get; set; }
    }

    // ========================================================================
    // WORKSTATION INFO
    // ========================================================================
    // Location: Entities/WorkstationInfo.cs

    /// <summary>
    /// Workstation information for audit trails.
    /// Passed to operations that need to track where they occurred.
    /// </summary>
    public class WorkstationInfo
    {
        /// <summary>
        /// Workstation hostname.
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// Application version.
        /// </summary>
        public string AppVersion { get; set; }

        /// <summary>
        /// Workstation ID.
        /// </summary>
        public int WorkstationId { get; set; }

        /// <summary>
        /// Creates WorkstationInfo from current environment.
        /// </summary>
        public static WorkstationInfo Current()
        {
            return new WorkstationInfo
            {
                Hostname = Environment.MachineName,
                AppVersion = VersionHelper.Version,
                WorkstationId = DependencyManager.Resolve<Status>().WorkstationId
            };
        }
    }
}

// ============================================================================
// USAGE EXAMPLES
// ============================================================================

/*
 * EVENT REGISTRATION PATTERNS:
 *
 * ===== INLINE (1-5 events) - Simple projects =====
 * public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
 * {
 *     // ... initialization
 *
 *     var eventHelper = DependencyManager.Resolve<SimphonyEventHelper>();
 *     eventHelper.Register(this, "ProcessCheckEvent", nameof(MyScript));
 *     eventHelper.Register(this, "CloseCheckEvent", nameof(MyScript));
 * }
 *
 * ===== HYBRID ARRAY (5-15 events) - Medium projects =====
 * public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
 * {
 *     // ... initialization
 *
 *     var events = new[]
 *     {
 *         new Event { Name = "BeginCheckPreviewEvent", Script = nameof(VerifyScript) },
 *         new Event { Name = "ProcessCheckEvent", Script = nameof(ProcessScript) },
 *         new Event { Name = "CloseCheckEvent", Script = nameof(CloseScript) },
 *         // ... 5-15 events
 *     };
 *
 *     var eventHelper = DependencyManager.Resolve<SimphonyEventHelper>();
 *     foreach (var evt in events)
 *         eventHelper.Register(this, evt.Name, evt.Script);
 * }
 *
 * ===== CONFIGURATION-DRIVEN (15+ events) - Complex projects =====
 * public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
 * {
 *     // ... initialization
 *
 *     var config = DependencyManager.Resolve<IConfigurationClient>().ReadConfig();
 *     var eventHelper = DependencyManager.Resolve<SimphonyEventHelper>();
 *
 *     foreach (var evt in config.Events)
 *         eventHelper.Register(this, evt.Name, evt.Script, evt.Function);
 * }
 *
 * ===== IEVENTHANDLER PATTERN (Separate event handlers) =====
 * public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
 * {
 *     // ... initialization
 *
 *     SimphonyEventHelper.RegisterServiceChargePreviewEvent(this, nameof(MyEventHandler));
 *     SimphonyEventHelper.RegisterTenderMediaVoidEvent(this, nameof(MyEventHandler));
 * }
 */

/*
 * VERSION SCRIPT EXAMPLE:
 *
 * public class Version : IScript
 * {
 *     private readonly IOpsContextClient _opsContext;
 *
 *     public Version(IOpsContextClient opsContext)
 *     {
 *         _opsContext = opsContext;
 *     }
 *
 *     public void Execute(string functionName, string argument)
 *     {
 *         var message = $"{VersionHelper.NameAndVersion}\n" +
 *                      $"Build: {VersionHelper.BuildDateTime:yyyy-MM-dd HH:mm:ss}\n" +
 *                      $"Workstation: {Environment.MachineName}";
 *
 *         _opsContext.ShowMessage(message);
 *     }
 * }
 */

/*
 * EXCEPTION HANDLING PATTERN:
 *
 * public override string CallFunc(object sender, string dummy1, object dummy2, out object oRet)
 * {
 *     try
 *     {
 *         var args = SimphonyOpsCommandArguments.Parse(((Button)sender).OpsCommandArguments);
 *         var script = DependencyManager.Resolve<IScript>(args.Script);
 *         script.Execute(args.Function, args.Argument);
 *     }
 *     catch (Exception e)
 *     {
 *         // Unwrap exception for better error message
 *         var first = ExceptionHelper.GetFirstException(e);
 *
 *         // Log exception
 *         _logger.LogException("Error executing script", first);
 *
 *         // Show user-friendly error
 *         OpsContext.ShowError(first.Message);
 *     }
 *
 *     oRet = null;
 *     return string.Empty;
 * }
 */
