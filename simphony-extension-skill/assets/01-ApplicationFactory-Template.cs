// SimphonyExtensibilityApplication.cs
// Universal Mandatory Pattern - 100% consistent across all 5 projects
// Location: Root of project

using System;
using System.Linq;
using Micros.Ops.Extensibility;
using Micros.OpsUI.Controls;
using Micros.PosCore.Extensibility;
using YourNamespace.Contracts;
using YourNamespace.Contracts.Clients;
using YourNamespace.Contracts.Logging;
using YourNamespace.Dependency;
using YourNamespace.Entities;
using YourNamespace.Helpers;

namespace YourNamespace
{
    /// <summary>
    /// Factory for creating the extension application instance.
    /// Required by Simphony POS to instantiate your extension.
    /// </summary>
    public class ApplicationFactory : IExtensibilityAssemblyFactory
    {
        public ExtensibilityAssemblyBase Create(IExecutionContext context)
            => new SimphonyExtensibilityApplication(context);

        public void Destroy(ExtensibilityAssemblyBase app)
            => app.Destroy();
    }

    /// <summary>
    /// Main entry point for the Simphony Extension Application.
    /// Handles initialization, script routing, and event registration.
    /// </summary>
    public class SimphonyExtensibilityApplication : OpsExtensibilityApplication
    {
        private readonly ILogManager _logger;

        public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
        {
            try
            {
                // Step 1: Register the Simphony execution environment
                DependencyManager.RegisterByInstance(new OpsExtensibilityEnvironment(ExecutionContext.CurrentExecutionContext));

                // Step 2: Install all dependencies (clients, scripts, services, etc.)
                DependencyManager.Install<SimphonyDependencies>();

                // Step 3: Set workstation tracking
                DependencyManager.Resolve<Status>().WorkstationId = OpsContext.WorkstationID;

                // Step 4: Get logger
                _logger = DependencyManager.Resolve<ILogManager>();
                _logger.LogInfo($"Instantiating {VersionHelper.NameAndVersion}");

                // Step 5: Check if configuration exists (optional - remove if config not required)
                var config = DependencyManager.Resolve<IConfigurationClient>().ReadConfig();
                if (config == null)
                {
                    _logger.LogInfo("No configuration found - extension will not be fully initialized");
                    // Decide: return early or continue with defaults
                    // return;
                }

                // Step 6: Register event callbacks
                _logger.LogInfo("Registering event callbacks");
                DependencyManager.Resolve<SimphonyEventHelper>().RegisterCallBacks(this);

                // Step 7: Start background services (optional - if using IService pattern)
                // _logger.LogInfo("Starting background services");
                // DependencyManager.ResolveAll<IService>().ToList().ForEach(x => x.Start());

                _logger.LogInfo("Extension application initialized successfully");
            }
            catch (Exception e)
            {
                // Use GetFirstException to unwrap AggregateException/TargetInvocationException
                var first = ExceptionHelper.GetFirstException(e);

                // Log to all configured loggers
                _logger?.LogException("Error instantiating extension application", first);

                // Show error to user in Simphony UI
                OpsContext.ShowException(first, "Extension Initialization Error");
            }
        }

        /// <summary>
        /// Called by Simphony when a button with this extension's command is clicked.
        /// Parses arguments and routes to appropriate script.
        /// </summary>
        public override string CallFunc(object sender, string dummy1, object dummy2, out object oRet)
        {
            try
            {
                // Parse Simphony command arguments
                var args = SimphonyOpsCommandArguments.Parse(((Button)sender).OpsCommandArguments);

                _logger.LogInfo($"Received extensibility call: Script={args.Script}, Function={args.Function}, Argument={args.Argument}");

                // Resolve script from DI container
                var script = DependencyManager.ResolveOrDefault<IScript>(args.Script)
                    ?? throw new Exception($"{args.Script} is not a valid script name");

                // Execute script
                // Note: args.Function is used in IScript variants with functionName parameter
                // For object argument variant, pass args.Argument directly
                script.Execute(args.Function, args.Argument);
            }
            catch (Exception e)
            {
                // Unwrap exception for better error messages
                var first = ExceptionHelper.GetFirstException(e);

                // Log exception
                _logger.LogException("Error executing script", first);

                // Show user-friendly error
                OpsContext.ShowError(first.Message);
            }

            // Always return empty string and null output
            oRet = null;
            return string.Empty;
        }
    }

    /// <summary>
    /// Optional: Network service support for cross-workstation communication.
    /// Only implement if you need CAPS/non-CAPS distributed operations.
    /// Example: MunerisTimeKeeping uses this for network time-keeping.
    /// </summary>
    /*
    public class SimphonyExtensionApplicationService : ExtensionApplicationService
    {
        private readonly List<INetworkService> _networkServices;

        public SimphonyExtensionApplicationService()
        {
            DependencyManager.Install<SimphonyDependencies>();
            DependencyManager.Resolve<Status>().ServiceHostId = ServiceHostID;

            _networkServices = DependencyManager.ResolveAll<INetworkService>().ToList();

            DependencyManager.Resolve<ILogManager>().LogInfo("Extension application network service started");
            DependencyManager.Resolve<Status>().ExtensionApplicationServiceStarted = true;
        }

        public override ApplicationResponse ProcessMessage(NetworkMessage message)
        {
            var service = _networkServices.FirstOrDefault(x => x.Name == message.Command);

            if (service == null)
                return new ApplicationResponse { ErrorText = $"No service found with name {message.Command}" };

            var response = service.ProcessMessage(message.DataAsUnicode);
            return new ApplicationResponse { Success = true, DataAsUnicode = response };
        }
    }
    */
}

// ============================================================================
// USAGE NOTES
// ============================================================================

/*
 * SIMPHONY COMMAND CONFIGURATION:
 *
 * In Simphony EMC, configure button with this format:
 * OpsCommandArguments: "Script=MyScriptName;Function=MyFunctionName;Argument=MyArgument"
 *
 * Example:
 * - Script=Version;Function=;Argument=
 * - Script=ClockInOut;Function=ClockIn;Argument=
 * - Script=Loyalty;Function=;Argument=CustomerId
 *
 * The CallFunc method parses these arguments and routes to the appropriate script.
 */

/*
 * INITIALIZATION SEQUENCE:
 *
 * 1. Simphony loads extension DLL
 * 2. Calls ApplicationFactory.Create()
 * 3. SimphonyExtensibilityApplication constructor runs:
 *    a. Registers OpsExtensibilityEnvironment
 *    b. Installs dependencies via SimphonyDependencies
 *    c. Sets Status.WorkstationId
 *    d. Resolves ILogManager
 *    e. Reads configuration (optional)
 *    f. Registers event callbacks
 *    g. Starts background services (optional)
 * 4. Extension is ready to handle button clicks (CallFunc)
 */

/*
 * ERROR HANDLING STRATEGY:
 *
 * - Constructor errors: Log and show to user, extension may be partially initialized
 * - CallFunc errors: Log and show to user, continue running
 * - Always use ExceptionHelper.GetFirstException() for better error messages
 * - Always log before showing error to user
 * - Never let exceptions crash Simphony POS
 */

/*
 * CUSTOMIZATION POINTS:
 *
 * 1. Configuration check: Remove if your extension doesn't use configuration
 * 2. Event registration: Customize which events you register
 * 3. Background services: Uncomment if using IService pattern
 * 4. Network services: Uncomment if using cross-workstation communication
 * 5. Script execution: Adapt based on IScript variant you choose
 */
