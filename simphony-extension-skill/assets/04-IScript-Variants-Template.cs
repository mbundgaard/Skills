// IScript Interface Variants
// Scale-Appropriate Pattern - Choose based on script count and complexity
// Location: Contracts/IScript.cs
//
// DECISION TREE:
// - 15+ scripts with event handling → Full Variant
// - 5-15 scripts with multi-function → Simplified Variant
// - 1-10 single-purpose scripts → Object Argument Variant

using System;
using System.Linq;
using YourNamespace.Contracts.Clients;
using YourNamespace.Contracts.Logging;

namespace YourNamespace.Contracts
{
    // ========================================================================
    // VARIANT 1: FULL INTERFACE (Execute + Event)
    // ========================================================================
    // Use when: 15+ scripts OR scripts need to handle events directly
    // Example project: SimphonyOpsHelper (24 scripts, 20+ events)
    // Complexity: HIGH

    /// <summary>
    /// Full IScript interface with Execute and Event methods.
    /// Supports multi-function routing and event handling in scripts.
    /// </summary>
    public interface IScript_FullVariant
    {
        /// <summary>
        /// Execute a named function with argument.
        /// </summary>
        /// <param name="functionName">Function to execute (from Simphony command)</param>
        /// <param name="argument">Argument for function (from Simphony command)</param>
        void Execute(string functionName, string argument);

        /// <summary>
        /// Handle events that are registered to this script.
        /// Called by SimphonyEventHelper when events occur.
        /// </summary>
        void Event();
    }

    /// <summary>
    /// Example implementation of Full Variant.
    /// </summary>
    public class MyScript_FullExample : IScript_FullVariant
    {
        private readonly ILogManager _logger;
        private readonly IOpsContextClient _opsContext;

        public MyScript_FullExample(ILogManager logger, IOpsContextClient opsContext)
        {
            _logger = logger;
            _opsContext = opsContext;
        }

        public void Execute(string functionName, string argument)
        {
            // Route to appropriate function based on functionName
            switch (functionName)
            {
                case "FunctionA":
                    DoFunctionA(argument);
                    break;

                case "FunctionB":
                    DoFunctionB(argument);
                    break;

                case "FunctionC":
                    DoFunctionC();
                    break;

                default:
                    throw new Exception($"Unknown function: {functionName}");
            }
        }

        public void Event()
        {
            // Handle events that are registered to this script
            _logger.LogInfo("Event occurred");

            // Access event data from OpsContext
            var checkInfo = _opsContext.GetCheckInfo();

            // Process event
            // ...
        }

        private void DoFunctionA(string argument)
        {
            _logger.LogInfo($"Executing FunctionA with argument: {argument}");
            // Business logic
        }

        private void DoFunctionB(string argument)
        {
            _logger.LogInfo($"Executing FunctionB with argument: {argument}");
            // Business logic
        }

        private void DoFunctionC()
        {
            _logger.LogInfo("Executing FunctionC");
            // Business logic
        }
    }

    // ========================================================================
    // VARIANT 2: SIMPLIFIED INTERFACE (Execute only)
    // ========================================================================
    // Use when: 5-15 scripts, no event handling in scripts
    // Example projects: SspLiquidDispenseSystem (2 scripts), SspSaviaExport (11 scripts), SovinoLoyalty (4 scripts)
    // Complexity: MEDIUM
    // MOST COMMON VARIANT (3 of 5 projects use this)

    /// <summary>
    /// Simplified IScript interface with only Execute method.
    /// Event handling done via separate IEventHandler pattern if needed.
    /// </summary>
    public interface IScript_SimplifiedVariant
    {
        /// <summary>
        /// Execute a named function with argument.
        /// </summary>
        /// <param name="functionName">Function to execute (from Simphony command)</param>
        /// <param name="argument">Argument for function (from Simphony command)</param>
        void Execute(string functionName, string argument);
    }

    /// <summary>
    /// Example implementation of Simplified Variant.
    /// </summary>
    public class MyScript_SimplifiedExample : IScript_SimplifiedVariant
    {
        private readonly ILogManager _logger;
        private readonly IConfigurationClient _config;

        public MyScript_SimplifiedExample(ILogManager logger, IConfigurationClient config)
        {
            _logger = logger;
            _config = config;
        }

        public void Execute(string functionName, string argument)
        {
            _logger.LogInfo($"Executing {functionName} with argument: {argument}");

            // For simple scripts with one function, functionName might be empty
            if (string.IsNullOrEmpty(functionName))
            {
                DoMainFunction(argument);
                return;
            }

            // For scripts with multiple functions, route based on functionName
            switch (functionName)
            {
                case "Issue":
                    IssueCard(argument);
                    break;

                case "Balance":
                    GetBalance(argument);
                    break;

                case "Redeem":
                    RedeemCard(argument);
                    break;

                default:
                    throw new Exception($"Function {functionName} is not supported");
            }
        }

        private void DoMainFunction(string argument)
        {
            // Main business logic
        }

        private void IssueCard(string cardNumber)
        {
            _logger.LogInfo($"Issuing card: {cardNumber}");
            // Issue card logic
        }

        private void GetBalance(string cardNumber)
        {
            _logger.LogInfo($"Getting balance for card: {cardNumber}");
            // Balance logic
        }

        private void RedeemCard(string cardNumber)
        {
            _logger.LogInfo($"Redeeming card: {cardNumber}");
            // Redemption logic
        }
    }

    // ========================================================================
    // VARIANT 3: OBJECT ARGUMENT (Execute with object)
    // ========================================================================
    // Use when: 1-10 single-purpose scripts (one function per script)
    // Example project: MunerisTimeKeeping (6 scripts)
    // Complexity: LOW (with AbstractScript auto-dispatch)
    // MODERN VARIANT (reduces boilerplate)

    /// <summary>
    /// Object argument IScript interface.
    /// Each script has single responsibility (one function).
    /// Used with AbstractScript for auto-dispatch.
    /// </summary>
    public interface IScript_ObjectVariant
    {
        /// <summary>
        /// Execute script with object argument.
        /// AbstractScript will auto-dispatch to single public method.
        /// </summary>
        /// <param name="argument">Argument (can be null, string, or typed object)</param>
        void Execute(object argument);
    }

    /// <summary>
    /// Abstract base class for object variant scripts.
    /// Automatically dispatches to single public method via reflection.
    /// </summary>
    public abstract class AbstractScript : IScript_ObjectVariant
    {
        public void Execute(object argument)
        {
            // Find all public methods (excluding object and AbstractScript methods)
            var methods = GetType().GetMethods()
                .Where(x => x.DeclaringType != typeof(object) && x.DeclaringType != typeof(AbstractScript))
                .ToList();

            // Enforce single-method pattern
            if (methods.Count != 1)
                throw new Exception("Script must contain only one function");

            // Auto-dispatch based on parameter count
            switch (methods[0].GetParameters().Length)
            {
                case 0:
                    // No parameters - just invoke
                    methods[0].Invoke(this, new object[0]);
                    break;

                case 1:
                    // One parameter - pass argument
                    methods[0].Invoke(this, new[] { argument });
                    break;

                default:
                    throw new Exception("Too many parameters in method");
            }
        }
    }

    /// <summary>
    /// Example implementation of Object Variant.
    /// Clean, focused, single-purpose script.
    /// </summary>
    public class MyScript_ObjectExample : AbstractScript
    {
        private readonly ILogManager _logger;
        private readonly IOpsContextClient _opsContext;

        public MyScript_ObjectExample(ILogManager logger, IOpsContextClient opsContext)
        {
            _logger = logger;
            _opsContext = opsContext;
        }

        // This is the ONLY public method - will be auto-dispatched
        public void ProcessClockIn()
        {
            _logger.LogInfo("Processing clock-in");

            var employee = _opsContext.GetEmployeeNumber();

            // Business logic here
            _logger.LogInfo($"Employee {employee} clocked in");
        }

        // Private methods are allowed (not dispatched)
        private void HelperMethod()
        {
            // Helper logic
        }
    }

    /// <summary>
    /// Example with argument.
    /// </summary>
    public class MyScript_ObjectWithArgExample : AbstractScript
    {
        private readonly ILogManager _logger;

        public MyScript_ObjectWithArgExample(ILogManager logger)
        {
            _logger = logger;
        }

        // Single public method with parameter - argument auto-passed
        public void PrintReport(string reportId)
        {
            _logger.LogInfo($"Printing report: {reportId}");

            // Business logic
        }
    }

    /// <summary>
    /// Example with no parameters.
    /// </summary>
    public class MyScript_ObjectNoArgsExample : AbstractScript
    {
        private readonly ILogManager _logger;

        public MyScript_ObjectNoArgsExample(ILogManager logger)
        {
            _logger = logger;
        }

        // Single public method with no parameters
        public void ShowVersion()
        {
            _logger.LogInfo("Showing version");

            // Business logic
        }
    }
}

// ============================================================================
// DECISION GUIDE
// ============================================================================

/*
 * WHICH VARIANT SHOULD YOU USE?
 *
 * COUNT YOUR SCRIPTS AND FUNCTIONS:
 *
 * 1. How many scripts will your project have?
 * 2. How many functions per script?
 * 3. Do scripts need to handle events directly?
 *
 * DECISION TREE:
 *
 * IF scripts need to handle events directly (Event() method)
 *     → Use FULL VARIANT
 *     Example: Event-driven complex operations
 *
 * ELSE IF 1-10 scripts AND single-purpose (one function each)
 *     → Use OBJECT ARGUMENT VARIANT with AbstractScript
 *     Example: Time-keeping with simple scripts (clock in, clock out, etc.)
 *     Benefits: Cleanest code, enforces single responsibility
 *
 * ELSE IF 2-15 scripts with multiple functions
 *     → Use SIMPLIFIED VARIANT
 *     Example: Loyalty, stored value, admin scripts
 *     Benefits: Balance of simplicity and flexibility
 *
 * ELSE (15+ scripts)
 *     → Use FULL VARIANT (even if no events)
 *     Example: Large multi-domain operations
 *     Benefits: Handles complexity
 *
 * DEFAULT RECOMMENDATION: SIMPLIFIED VARIANT
 * - Most common (3 of 5 projects)
 * - Good balance
 * - Easy to understand
 */

// ============================================================================
// PROS AND CONS
// ============================================================================

/*
 * FULL VARIANT (Execute + Event):
 *
 * PROS:
 * - Supports event handling in scripts
 * - Multi-function routing built-in
 * - Proven for complex projects
 *
 * CONS:
 * - More boilerplate
 * - Event() method often unused
 * - Mixing concerns (scripts AND event handlers)
 *
 * WHEN TO USE:
 * - 15+ scripts
 * - Scripts must handle events directly
 * - Legacy/existing pattern
 */

/*
 * SIMPLIFIED VARIANT (Execute only):
 *
 * PROS:
 * - Clean interface
 * - Multi-function routing
 * - Event handling separate (IEventHandler pattern)
 * - Most widely used (3/5 projects)
 *
 * CONS:
 * - Switch statement boilerplate
 * - Function name string matching
 *
 * WHEN TO USE:
 * - 2-15 scripts
 * - Multiple functions per script
 * - DEFAULT CHOICE for most projects
 */

/*
 * OBJECT ARGUMENT VARIANT (Execute with AbstractScript):
 *
 * PROS:
 * - Cleanest code
 * - Enforces single responsibility
 * - No switch statement boilerplate
 * - Reflection auto-dispatch
 *
 * CONS:
 * - Requires AbstractScript base class
 * - Only one function per script
 * - Reflection overhead (minimal)
 *
 * WHEN TO USE:
 * - 1-10 scripts
 * - Single-purpose scripts
 * - Modern, clean codebase
 */

// ============================================================================
// MIGRATION GUIDE
// ============================================================================

/*
 * MIGRATING FROM FULL TO SIMPLIFIED:
 *
 * 1. Remove Event() method from IScript interface
 * 2. Remove Event() implementations from all scripts
 * 3. Create IEventHandler implementations for event logic
 * 4. Register event handlers in SimphonyDependencies
 * 5. Update event registration to use IEventHandler
 *
 * BENEFITS:
 * - Cleaner separation of concerns
 * - Easier to test event handling
 * - Follows single responsibility principle
 */

/*
 * MIGRATING FROM SIMPLIFIED TO OBJECT ARGUMENT:
 *
 * 1. Change IScript to Execute(object argument)
 * 2. Create AbstractScript base class
 * 3. Have scripts inherit from AbstractScript
 * 4. Refactor multi-function scripts into single-purpose scripts
 * 5. Update CallFunc to pass argument directly
 *
 * BENEFITS:
 * - Cleaner code
 * - Enforced single responsibility
 * - Less boilerplate
 *
 * CHALLENGE:
 * - Need more scripts (one per function)
 * - May not be worth it for existing projects
 */

// ============================================================================
// SIMPHONY COMMAND EXAMPLES
// ============================================================================

/*
 * FULL VARIANT commands:
 * OpsCommandArguments: "Script=MyScript;Function=FunctionA;Argument=value1"
 * OpsCommandArguments: "Script=MyScript;Function=FunctionB;Argument="
 * OpsCommandArguments: "Script=MyScript;Function=;Argument=" (calls Event)
 *
 * SIMPLIFIED VARIANT commands:
 * OpsCommandArguments: "Script=StoredValue;Function=Issue;Argument=12345"
 * OpsCommandArguments: "Script=StoredValue;Function=Balance;Argument=12345"
 * OpsCommandArguments: "Script=Loyalty;Function=;Argument=CustomerId"
 *
 * OBJECT ARGUMENT VARIANT commands:
 * OpsCommandArguments: "Script=ClockIn;Function=;Argument="
 * OpsCommandArguments: "Script=PrintReport;Function=;Argument=ReportA"
 *
 * Note: Function parameter may be ignored in Object Argument variant
 */
