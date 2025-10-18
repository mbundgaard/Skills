using Micros.Ops.Extensibility;
using Micros.OpsUI.Controls;
using Micros.PosCore.Extensibility;
using Micros.PosCore.Extensibility.Networking;
using YourNamespace.Contracts;
using YourNamespace.Contracts.Clients;
using YourNamespace.Contracts.Logging;
using YourNamespace.Dependency;
using YourNamespace.Helpers;
using System;

namespace YourNamespace
{
    /// <summary>
    /// Handles network messages for non-CAPS workstations.
    /// Returns "Not supported" by default - override if network communication is needed.
    /// </summary>
    public class SimphonyExtensionApplicationService : ExtensionApplicationService
    {
        public override ApplicationResponse ProcessMessage(NetworkMessage message) => new() { Success = false, ErrorText = "Not supported" };
    }

    /// <summary>
    /// Factory for creating the Simphony Extension Application instance.
    /// Required by Simphony extensibility framework.
    /// </summary>
    public class ApplicationFactory : IExtensibilityAssemblyFactory
    {
        public ExtensibilityAssemblyBase Create(IExecutionContext context) => new SimphonyExtensibilityApplication(context);
        public void Destroy(ExtensibilityAssemblyBase app) => app.Destroy();
    }

    /// <summary>
    /// Main extension application class - placed at root of extension project.
    /// The ONLY part that changes between extensions is the Event Registration section.
    /// </summary>
    public class SimphonyExtensibilityApplication : OpsExtensibilityApplication
    {
        private readonly ILogManager _logger;

        public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
        {
            // 1. Register execution context
            DependencyManager.RegisterByInstance(new OpsExtensibilityEnvironment(ExecutionContext.CurrentExecutionContext));

            // 2. Install all dependencies
            DependencyManager.Install<Dependencies>();

            // 3. Resolve logger
            _logger = DependencyManager.Resolve<ILogManager>();

            _logger.LogInfo($"Instantiating {VersionHelper.NameAndVersion}");

            try
            {
                // ============================================================
                // EVENT REGISTRATION - This is the ONLY section that changes
                // ============================================================

                // Example 1: Hosted Service Pattern (start/stop background service)
                OpsReadyEvent += (s, a) =>
                {
                    DependencyManager.Resolve<YourBackgroundService>().Start(true);
                    return EventProcessingInstruction.Continue;
                };

                OpsExitEvent += (s, a) =>
                {
                    DependencyManager.Resolve<YourBackgroundService>().Stop(true);
                    return EventProcessingInstruction.Continue;
                };

                // Example 2: Transaction Event Pattern (for POS integration)
                // CheckTotalEvent += (s, a) =>
                // {
                //     DependencyManager.Resolve<ITransactionProcessor>().ProcessCheckTotal(a);
                //     return EventProcessingInstruction.Continue;
                // };

                // Example 3: Tender Event Pattern (for payment processing)
                // TenderEvent += (s, a) =>
                // {
                //     DependencyManager.Resolve<IPaymentProcessor>().ProcessTender(a);
                //     return EventProcessingInstruction.Continue;
                // };

                // ============================================================
                // END EVENT REGISTRATION
                // ============================================================
            }
            catch (Exception e)
            {
                _logger.LogException($"Error registering callbacks", e);
            }

            _logger.LogInfo($"Instantiation completed");
        }

        /// <summary>
        /// Handles button click events from Simphony UI.
        /// Uses SimphonyOpsCommandArguments to parse Script/Function/Argument from button configuration.
        /// </summary>
        public override string CallFunc(object sender, string dummy1, object dummy2, out object oRet)
        {
            try
            {
                var args = SimphonyOpsCommandArguments.Parse(((Button)sender).OpsCommandArguments);
                _logger.LogInfo($"Received call (script: {args.Script}, function: {args.Function}, argument: {args.Argument}");

                var script = DependencyManager.ResolveSafe<IScript>(args.Script) ?? throw new Exception($"Script {args.Script} was not found");

                script.Execute(args.Function, args.Argument);
            }
            catch (Exception e)
            {
                var first = ExceptionHelper.GetFirstException(e);
                _logger.LogException("Error executing script", first);

                DependencyManager.Resolve<IOpsContextClient>().ShowError(first.Message, e);
            }

            oRet = null;
            return string.Empty;
        }
    }
}
