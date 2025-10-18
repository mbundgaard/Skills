using YourNamespace.Contracts.Clients;
using YourNamespace.Contracts.Logging;
using YourNamespace.Helpers;

namespace YourNamespace.Scripts
{
    // ========================================================================
    // EXAMPLE 1: Simple Single-Function Script
    // ========================================================================
    /// <summary>
    /// Simple script with one function.
    /// AbstractScript automatically routes to the single public method.
    /// </summary>
    public class SimpleScript : AbstractScript
    {
        private readonly IOpsContextClient _opsContextClient;
        private readonly ILogManager _logger;

        public SimpleScript(IOpsContextClient opsContextClient, ILogManager logger)
        {
            _opsContextClient = opsContextClient;
            _logger = logger;
        }

        /// <summary>
        /// This is the only public method, so it's called regardless of function name.
        /// </summary>
        public void DoSomething()
        {
            _logger.LogInfo("SimpleScript.DoSomething called");
            _opsContextClient.ShowMessage("Hello from SimpleScript!");
        }
    }

    // ========================================================================
    // EXAMPLE 2: Multi-Function Script
    // ========================================================================
    /// <summary>
    /// Script with multiple functions.
    /// AbstractScript routes based on function name (case-insensitive).
    /// </summary>
    public class MultiScript : AbstractScript
    {
        private readonly IOpsContextClient _opsContextClient;
        private readonly ILogManager _logger;

        public MultiScript(IOpsContextClient opsContextClient, ILogManager logger)
        {
            _opsContextClient = opsContextClient;
            _logger = logger;
        }

        /// <summary>
        /// Called when function name is "FunctionOne" (case-insensitive).
        /// </summary>
        public void FunctionOne()
        {
            _logger.LogInfo("FunctionOne called");
            _opsContextClient.ShowMessage("Function One executed");
        }

        /// <summary>
        /// Called when function name is "FunctionTwo" (case-insensitive).
        /// </summary>
        public void FunctionTwo()
        {
            _logger.LogInfo("FunctionTwo called");
            _opsContextClient.ShowMessage("Function Two executed");
        }

        /// <summary>
        /// Private methods are ignored by AbstractScript routing.
        /// Use for helper/utility methods.
        /// </summary>
        private void HelperMethod()
        {
            // Not callable from buttons - helper method only
        }
    }

    // ========================================================================
    // EXAMPLE 3: Script with String Arguments
    // ========================================================================
    /// <summary>
    /// Script that accepts string arguments from button configuration.
    /// Arguments are passed from OpsCommandArguments.
    /// </summary>
    public class ArgumentScript : AbstractScript
    {
        private readonly IOpsContextClient _opsContextClient;
        private readonly ILogManager _logger;

        public ArgumentScript(IOpsContextClient opsContextClient, ILogManager logger)
        {
            _opsContextClient = opsContextClient;
            _logger = logger;
        }

        /// <summary>
        /// Method with string argument.
        /// AbstractScript passes the argument from button configuration.
        /// </summary>
        public void ProcessWithArgument(string argument)
        {
            _logger.LogInfo($"ProcessWithArgument called with: {argument}");
            _opsContextClient.ShowMessage($"Received argument: {argument}");
        }
    }

    // ========================================================================
    // EXAMPLE 4: Event Handler Script
    // ========================================================================
    /// <summary>
    /// Script that handles Simphony events with typed arguments.
    /// Called from event handlers, not button clicks.
    /// </summary>
    public class EventHandlerScript : AbstractScript
    {
        private readonly IOpsContextClient _opsContextClient;
        private readonly ILogManager _logger;

        public EventHandlerScript(IOpsContextClient opsContextClient, ILogManager logger)
        {
            _opsContextClient = opsContextClient;
            _logger = logger;
        }

        /// <summary>
        /// Handle BeginCheck event.
        /// Receives typed event args and can abort operation.
        /// </summary>
        public void OnCheckOpen(OpsBeginCheckPreviewArgs args)
        {
            _logger.LogInfo("Check opening");

            // Validation logic
            if (SomeConditionFails())
            {
                args.AbortOperation = true;
                args.Error = "Cannot open check: validation failed";
                return;
            }

            _logger.LogInfo("Check opened successfully");
        }

        /// <summary>
        /// Handle Tender event.
        /// Can modify or abort tender operation.
        /// </summary>
        public void OnTender(OpsTenderMediaPreviewArgs args)
        {
            _logger.LogInfo($"Tender: {args.ObjectNumber}, Amount: {args.Amount}");

            // Example: Validate minimum tender amount
            if (args.Amount < 1.00m)
            {
                args.AbortOperation = true;
                args.Message = "Minimum tender amount is $1.00";
            }
        }

        private bool SomeConditionFails()
        {
            // Your validation logic here
            return false;
        }
    }

    // ========================================================================
    // EXAMPLE 5: Script with Configuration Access
    // ========================================================================
    /// <summary>
    /// Script that accesses configuration via Config property.
    /// Configuration is automatically cached by AbstractScript.
    /// </summary>
    public class ConfigScript : AbstractScript
    {
        private readonly IOpsContextClient _opsContextClient;
        private readonly ILogManager _logger;

        public ConfigScript(IOpsContextClient opsContextClient, ILogManager logger)
        {
            _opsContextClient = opsContextClient;
            _logger = logger;
        }

        /// <summary>
        /// Access configuration via protected Config property from AbstractScript.
        /// Configuration is cached for 10 minutes.
        /// </summary>
        public void UseConfiguration()
        {
            // Access configuration (automatically cached)
            var outputFolder = Config.OutputFolder;
            var enableFeature = Config.EnableFeature;

            _logger.LogInfo($"Output folder: {outputFolder}");
            _logger.LogInfo($"Feature enabled: {enableFeature}");

            _opsContextClient.ShowMessage($"Configuration loaded at: {Config.ReadTime}");
        }

        /// <summary>
        /// Force configuration refresh when needed.
        /// </summary>
        public void ReloadConfiguration()
        {
            RefreshConfig(); // Force immediate reload
            _opsContextClient.ShowMessage("Configuration refreshed");
        }
    }

    // ========================================================================
    // EXAMPLE 6: Script with Complex Logic
    // ========================================================================
    /// <summary>
    /// Script with complex business logic and multiple dependencies.
    /// Demonstrates full pattern usage.
    /// </summary>
    public class ComplexScript : AbstractScript
    {
        private readonly IOpsContextClient _opsContextClient;
        private readonly ILogManager _logger;
        private readonly IConfigurationClient _configClient;

        public ComplexScript(
            IOpsContextClient opsContextClient,
            ILogManager logger,
            IConfigurationClient configClient)
        {
            _opsContextClient = opsContextClient;
            _logger = logger;
            _configClient = configClient;
        }

        /// <summary>
        /// Complex operation with multiple steps.
        /// </summary>
        public void ComplexOperation()
        {
            _logger.LogInfo("Starting complex operation");

            try
            {
                // Step 1: Validate prerequisites
                if (!ValidatePrerequisites())
                {
                    _opsContextClient.ShowError("Prerequisites not met");
                    return;
                }

                // Step 2: Get user input
                var input = GetUserInput();
                if (input == null) return; // User cancelled

                // Step 3: Process input
                var result = ProcessInput(input);

                // Step 4: Show result
                _opsContextClient.ShowMessage($"Operation completed: {result}");

                _logger.LogInfo($"Complex operation completed: {result}");
            }
            catch (System.Exception ex)
            {
                _logger.LogException("Error in complex operation", ex);
                _opsContextClient.ShowError($"Error: {ex.Message}");
            }
        }

        private bool ValidatePrerequisites()
        {
            // Check configuration
            if (string.IsNullOrEmpty(Config.OutputFolder))
                return false;

            // Check system state
            if (!_opsContextClient.CheckIsOpen())
                return false;

            return true;
        }

        private string GetUserInput()
        {
            return _opsContextClient.GetTextInput("Enter value:");
        }

        private string ProcessInput(string input)
        {
            // Business logic here
            return input.ToUpper();
        }
    }
}
