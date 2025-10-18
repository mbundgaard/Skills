# Script Implementation Patterns for Simphony Extension Applications

## Overview

Scripts are the **business logic layer** in Simphony Extension Applications. They contain all user-facing functionality and event handling logic. This guide explains the universal script pattern based on IScript interface and AbstractScript base class.

## Core Pattern: IScript + AbstractScript

Every extension uses this exact pattern:

1. **IScript** - Universal interface all scripts implement
2. **AbstractScript** - Base class providing automatic method routing and configuration caching
3. **Concrete Scripts** - Your business logic inheriting from AbstractScript

### Why AbstractScript Instead of IScript Directly?

AbstractScript provides critical functionality automatically:
- **Automatic method routing** - No manual switch statements needed
- **Configuration caching** - 10-minute cache with auto-refresh
- **Dual entry points** - Button clicks and Simphony events
- **Flexible parameters** - Supports 0, 1, or 2 parameter methods

**Never implement IScript directly** - always inherit from AbstractScript.

## IScript Interface

```csharp
public interface IScript
{
    void Execute(string functionName, string argument);
}
```

**Simple by design:**
- Called from `SimphonyExtensibilityApplication.CallFunc` when buttons are clicked
- `functionName` comes from button OpsCommandArguments
- `argument` comes from button OpsCommandArguments
- AbstractScript handles the routing logic

## AbstractScript Base Class

### Key Features

```csharp
public abstract class AbstractScript : IScript
{
    private static YourConfig _config;

    // Entry point for button clicks
    public void Execute(string function, string argument) => Invoke(function, argument);

    // Entry point for Simphony events
    public void Event(string function, object argument) => Invoke(function, argument);

    // Automatic method routing via reflection
    private void Invoke(string function, object argument) { /* ... */ }

    // Cached configuration with 10-minute auto-refresh
    protected YourConfig Config { get { /* ... */ } }

    // Force configuration reload
    protected void RefreshConfig() { /* ... */ }
}
```

### Automatic Method Routing

AbstractScript uses reflection to route function calls:

**Single Method Script:**
```csharp
public class SimpleScript : AbstractScript
{
    public void DoSomething()
    {
        // This method is called regardless of function name
        // Works for single-purpose scripts
    }
}
```

**Multi-Method Script:**
```csharp
public class MultiScript : AbstractScript
{
    public void FunctionOne()
    {
        // Called when functionName is "FunctionOne" (case-insensitive)
    }

    public void FunctionTwo()
    {
        // Called when functionName is "FunctionTwo" (case-insensitive)
    }

    private void HelperMethod()
    {
        // Private methods are ignored - use for helpers
    }
}
```

**Routing Logic:**
1. If script has **one public method** → calls it (ignores function name)
2. If script has **multiple public methods** → matches by name (case-insensitive)
3. **Private methods** are ignored (safe to use as helpers)

### Parameter Flexibility

AbstractScript supports methods with 0, 1, or 2 parameters:

```csharp
// No parameters
public void NoParams()
{
    // Called as Execute("NoParams", anyArgument)
}

// One parameter (string or object)
public void OneParam(string argument)
{
    // Called as Execute("OneParam", "someValue")
}

// Two parameters (function name + argument)
public void TwoParams(string function, object argument)
{
    // Receives both function name and argument
    // Useful when you need to know which function was called
}
```

### Configuration Caching

Access configuration via the protected `Config` property:

```csharp
public class MyScript : AbstractScript
{
    public void UseConfig()
    {
        // Configuration is cached for 10 minutes
        var setting = Config.SomeSetting;
        var enabled = Config.EnableFeature;

        // Cache is automatically refreshed if older than 10 minutes
    }

    public void ForceRefresh()
    {
        // Force immediate reload when needed
        RefreshConfig();

        var newSetting = Config.SomeSetting;
    }
}
```

**Benefits:**
- Prevents excessive configuration reads
- Automatic refresh when stale
- Manual refresh when needed
- Shared across all script instances (static)

## Script Implementation Patterns

### 1. Simple Single-Function Script

**Use for:** Single-purpose operations (show version, refresh data, etc.)

```csharp
public class Version : AbstractScript
{
    private readonly IOpsContextClient _opsContextClient;

    public Version(IOpsContextClient opsContextClient)
    {
        _opsContextClient = opsContextClient;
    }

    // Only one public method - called regardless of function name
    public void ShowVersion()
    {
        _opsContextClient.ShowMessage(VersionHelper.NameAndVersion);
    }
}
```

**Button Configuration:**
- Script: "Version"
- Function: "ShowVersion" (or anything - only one method)
- Argument: (not used)

### 2. Multi-Function Script

**Use for:** Related operations grouped in one script (CRUD operations, menu actions)

```csharp
public class CustomerScript : AbstractScript
{
    private readonly IOpsContextClient _opsContextClient;
    private readonly ICustomerClient _customerClient;

    public CustomerScript(IOpsContextClient opsContextClient, ICustomerClient customerClient)
    {
        _opsContextClient = opsContextClient;
        _customerClient = customerClient;
    }

    public void CreateCustomer()
    {
        var name = _opsContextClient.GetTextInput("Enter customer name:");
        if (name == null) return;

        _customerClient.Create(name);
        _opsContextClient.ShowMessage("Customer created");
    }

    public void SearchCustomer()
    {
        var search = _opsContextClient.GetTextInput("Search customer:");
        if (search == null) return;

        var results = _customerClient.Search(search);
        // Display results...
    }

    public void DeleteCustomer()
    {
        // Implementation...
    }

    // Private helpers
    private void ValidateCustomer(string name)
    {
        // Helper method - not callable from buttons
    }
}
```

**Button Configuration:**
- Script: "CustomerScript"
- Function: "CreateCustomer" | "SearchCustomer" | "DeleteCustomer"
- Argument: (varies)

### 3. Event Handler Script

**Use for:** Handling Simphony events (CheckTotal, Tender, BeginCheck, etc.)

```csharp
public class ValidationScript : AbstractScript
{
    private readonly IOpsContextClient _opsContextClient;
    private readonly ILogManager _logger;

    public ValidationScript(IOpsContextClient opsContextClient, ILogManager logger)
    {
        _opsContextClient = opsContextClient;
        _logger = logger;
    }

    // Called from CheckTotal event handler
    public void ValidateCheck(OpsCheckTotalArgs args)
    {
        _logger.LogInfo("Validating check");

        if (!IsCheckValid())
        {
            args.AbortOperation = true;
            args.Message = "Check validation failed";
            return;
        }

        _logger.LogInfo("Check validated successfully");
    }

    // Called from Tender event handler
    public void ValidateTender(OpsTenderMediaPreviewArgs args)
    {
        _logger.LogInfo($"Validating tender: {args.ObjectNumber}");

        if (args.Amount < Config.MinimumTenderAmount)
        {
            args.AbortOperation = true;
            args.Message = $"Minimum tender amount is {Config.MinimumTenderAmount:C}";
        }
    }

    private bool IsCheckValid()
    {
        // Validation logic
        return _opsContextClient.CheckIsOpen();
    }
}
```

**Event Registration:**
```csharp
CheckTotalEvent += (s, a) =>
{
    var script = DependencyManager.Resolve<ValidationScript>();
    script.Event("ValidateCheck", a); // Use Event() method for typed args
    return EventProcessingInstruction.Continue;
};
```

**Key Difference:**
- Use `Event()` method instead of `Execute()` for typed event arguments
- Event args are `object` type (not `string`)
- Can abort operations via `args.AbortOperation = true`

### 4. Script with Configuration Access

**Use for:** Operations that depend on configuration settings

```csharp
public class ExportScript : AbstractScript
{
    private readonly ILogManager _logger;
    private readonly IExportClient _exportClient;

    public ExportScript(ILogManager logger, IExportClient exportClient)
    {
        _logger = logger;
        _exportClient = exportClient;
    }

    public void DailyExport()
    {
        _logger.LogInfo("Starting daily export");

        // Access configuration via Config property
        var outputFolder = Config.OutputFolder;
        var exportFormat = Config.ExportFormat;

        _logger.LogInfo($"Exporting to: {outputFolder}");

        _exportClient.Export(outputFolder, exportFormat);

        _logger.LogInfo("Export completed");
    }

    public void ReloadConfig()
    {
        // Force configuration refresh
        RefreshConfig();
        _logger.LogInfo($"Configuration reloaded at: {Config.ReadTime}");
    }
}
```

**Configuration is cached:**
- First access reads from database
- Cached for 10 minutes
- Auto-refreshes when stale
- Manual refresh via `RefreshConfig()`

### 5. Script with Complex Business Logic

**Use for:** Complex operations with multiple steps, validations, and error handling

```csharp
public class OrderScript : AbstractScript
{
    private readonly IOpsContextClient _opsContextClient;
    private readonly ILogManager _logger;
    private readonly IOrderClient _orderClient;

    public OrderScript(
        IOpsContextClient opsContextClient,
        ILogManager logger,
        IOrderClient orderClient)
    {
        _opsContextClient = opsContextClient;
        _logger = logger;
        _orderClient = orderClient;
    }

    public void PlaceOrder()
    {
        _logger.LogInfo("PlaceOrder started");

        try
        {
            // Step 1: Validate prerequisites
            if (!ValidatePrerequisites())
            {
                _opsContextClient.ShowError("Prerequisites not met");
                return;
            }

            // Step 2: Get customer selection
            var customer = SelectCustomer();
            if (customer == null)
            {
                _logger.LogInfo("User cancelled customer selection");
                return;
            }

            // Step 3: Get order details
            var orderDetails = GetOrderDetails();
            if (orderDetails == null)
            {
                _logger.LogInfo("User cancelled order details");
                return;
            }

            // Step 4: Confirm order
            if (!ConfirmOrder(customer, orderDetails))
            {
                _logger.LogInfo("User cancelled order confirmation");
                return;
            }

            // Step 5: Submit order
            var orderId = _orderClient.SubmitOrder(customer, orderDetails);

            // Step 6: Show confirmation
            _opsContextClient.ShowMessage($"Order {orderId} placed successfully");

            _logger.LogInfo($"Order {orderId} completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogException("Error placing order", ex);
            _opsContextClient.ShowError($"Order failed: {ex.Message}");
        }
    }

    private bool ValidatePrerequisites()
    {
        // Check configuration
        if (string.IsNullOrEmpty(Config.OrderApiEndpoint))
        {
            _logger.LogError("Order API endpoint not configured");
            return false;
        }

        // Check check is open
        if (!_opsContextClient.CheckIsOpen())
        {
            _logger.LogError("No check is open");
            return false;
        }

        return true;
    }

    private Customer SelectCustomer()
    {
        var customers = _orderClient.GetCustomers();
        var names = customers.Select(c => c.Name).ToArray();

        var selection = _opsContextClient.SelectFromList(
            "Select Customer",
            "Choose a customer:",
            names
        );

        return selection.HasValue ? customers[selection.Value] : null;
    }

    private OrderDetails GetOrderDetails()
    {
        // Get order details from user
        return new OrderDetails();
    }

    private bool ConfirmOrder(Customer customer, OrderDetails details)
    {
        return _opsContextClient.AskQuestion(
            $"Place order for {customer.Name}?"
        );
    }
}
```

**Pattern Elements:**
- Try-catch for error handling
- Step-by-step operation flow
- User cancellation support
- Configuration access
- Logging at each step
- Clear error messages

## Dependency Injection in Scripts

### Constructor Injection

Scripts receive dependencies via constructor:

```csharp
public class MyScript : AbstractScript
{
    private readonly IOpsContextClient _opsContextClient;
    private readonly ILogManager _logger;
    private readonly ICustomerClient _customerClient;
    private readonly IConfigurationClient _configClient;

    public MyScript(
        IOpsContextClient opsContextClient,
        ILogManager logger,
        ICustomerClient customerClient,
        IConfigurationClient configClient)
    {
        _opsContextClient = opsContextClient;
        _logger = logger;
        _customerClient = customerClient;
        _configClient = configClient;
    }

    // Use dependencies in methods...
}
```

### Common Dependencies

**Always inject:**
- `IOpsContextClient` - For UI and POS interaction
- `ILogManager` - For logging

**Often inject:**
- `IConfigurationClient` - If you need config outside of Config property
- Domain-specific clients (ICustomerClient, IOrderClient, etc.)
- External system clients (IPaymentClient, ILoyaltyClient, etc.)

### Registration in DI Container

```csharp
public class Dependencies : IDependencyInstaller
{
    public void Install(IDependencyManager manager)
    {
        // Register scripts with named registration
        manager.Register<IScript, Version>("Version");
        manager.Register<IScript, CustomerScript>("CustomerScript");
        manager.Register<IScript, OrderScript>("OrderScript");

        // Or register without interface (for event handlers)
        manager.Register<ValidationScript>();
    }
}
```

## Button Configuration

### OpsCommandArguments Format

Buttons in Simphony pass arguments using OpsCommandArguments:

```
Script:MyScript,Function:MyFunction,Argument:MyArgument
```

**Parsed by SimphonyOpsCommandArguments:**
```csharp
var args = SimphonyOpsCommandArguments.Parse(button.OpsCommandArguments);
// args.Script = "MyScript"
// args.Function = "MyFunction"
// args.Argument = "MyArgument"
```

### Example Button Configurations

**Version Button:**
```
Script:Version,Function:ShowVersion
```

**Customer Create:**
```
Script:CustomerScript,Function:CreateCustomer
```

**Export with Argument:**
```
Script:ExportScript,Function:Export,Argument:Daily
```

**Menu Item with Number:**
```
Script:MenuScript,Function:AddItem,Argument:1001
```

## Event-Driven Scripts

### Event Handler Pattern

Scripts can be called from Simphony event handlers:

```csharp
// In SimphonyExtensibilityApplication constructor
CheckTotalEvent += (s, a) =>
{
    var script = DependencyManager.Resolve<ValidationScript>();
    script.Event("ValidateCheck", a);
    return EventProcessingInstruction.Continue;
};
```

### Configuration-Driven Events

Use Event entities from configuration:

```xml
<Events>
  <Event Name="CheckTotal" Script="ValidationScript" Function="ValidateCheck" />
  <Event Name="Tender" Script="ValidationScript" Function="ValidateTender" />
</Events>
```

**Registration:**
```csharp
foreach (var eventConfig in Config.Events)
{
    RegisterEvent(eventConfig.Name, eventConfig.Script, eventConfig.Function);
}
```

### Common Event Args Types

- `OpsBeginCheckPreviewArgs` - CheckOpen event
- `OpsCheckTotalArgs` - CheckTotal event
- `OpsTenderMediaPreviewArgs` - Tender event
- `OpsServiceTotalArgs` - ServiceTotal event
- `OpsReadyArgs` - OpsReady event
- `OpsExitArgs` - OpsExit event

**All event args support:**
- `AbortOperation` - Set to true to cancel operation
- `Message` or `Error` - Error message to display
- Additional event-specific properties

## Version Script Pattern

### Always Include Version Script

**Why:**
- Troubleshooting deployment issues
- Verify correct version is loaded
- Quick sanity check in production

**Standard Implementation:**
```csharp
public class Version : AbstractScript
{
    private readonly IOpsContextClient _opsContextClient;

    public Version(IOpsContextClient opsContextClient)
    {
        _opsContextClient = opsContextClient;
    }

    public void ShowVersion()
    {
        _opsContextClient.ShowMessage(VersionHelper.NameAndVersion);
    }
}
```

**Button Setup:**
```
Script:Version,Function:ShowVersion
```

**Best Practice:**
- Create a button on every page template
- Place in bottom corner for easy access
- Use consistent naming across all extensions

## Best Practices

### 1. One Script Per Domain Concept

```csharp
// ✅ GOOD - Focused scripts
public class CustomerScript : AbstractScript { }
public class OrderScript : AbstractScript { }
public class PaymentScript : AbstractScript { }

// ❌ BAD - God script doing everything
public class MasterScript : AbstractScript
{
    public void DoEverything() { }
}
```

### 2. Use Dependency Injection

```csharp
// ✅ GOOD - Dependencies injected
public class MyScript : AbstractScript
{
    private readonly IOpsContextClient _opsContextClient;

    public MyScript(IOpsContextClient opsContextClient)
    {
        _opsContextClient = opsContextClient;
    }
}

// ❌ BAD - Direct instantiation
public class MyScript : AbstractScript
{
    public void DoSomething()
    {
        var client = new SimphonyOpsContextClient(...); // Don't do this
    }
}
```

### 3. Always Log Operations

```csharp
// ✅ GOOD - Comprehensive logging
public void PlaceOrder()
{
    _logger.LogInfo("PlaceOrder started");

    try
    {
        // Operation logic
        _logger.LogInfo("Order placed successfully");
    }
    catch (Exception ex)
    {
        _logger.LogException("Error placing order", ex);
        throw;
    }
}

// ❌ BAD - No logging
public void PlaceOrder()
{
    // No visibility into what happened
}
```

### 4. Handle User Cancellation

```csharp
// ✅ GOOD - Check for cancellation
public void GetInput()
{
    var input = _opsContextClient.GetTextInput("Enter value:");
    if (input == null)
    {
        _logger.LogInfo("User cancelled input");
        return;
    }

    // Process input...
}

// ❌ BAD - Assume user always enters something
public void GetInput()
{
    var input = _opsContextClient.GetTextInput("Enter value:");
    ProcessInput(input); // Could be null!
}
```

### 5. Use Configuration, Not Hard-Coded Values

```csharp
// ✅ GOOD - Configuration-driven
public void Export()
{
    var folder = Config.OutputFolder;
    var format = Config.ExportFormat;
    _exportClient.Export(folder, format);
}

// ❌ BAD - Hard-coded values
public void Export()
{
    _exportClient.Export(@"C:\Export", "XML");
}
```

### 6. Validate Prerequisites

```csharp
// ✅ GOOD - Validate before proceeding
public void ProcessCheck()
{
    if (!_opsContextClient.CheckIsOpen())
    {
        _opsContextClient.ShowError("No check is open");
        return;
    }

    // Process check...
}

// ❌ BAD - Assume check is open
public void ProcessCheck()
{
    var checkInfo = _opsContextClient.GetCheckInfo(); // Throws if no check
}
```

### 7. Provide Clear Error Messages

```csharp
// ✅ GOOD - User-friendly error messages
catch (Exception ex)
{
    _logger.LogException("Error processing payment", ex);
    _opsContextClient.ShowError("Payment processing failed. Please try again.");
}

// ❌ BAD - Technical error to user
catch (Exception ex)
{
    _opsContextClient.ShowError(ex.ToString()); // Too technical for users
}
```

## Anti-Patterns to Avoid

### ❌ DON'T: Implement IScript Directly

```csharp
// BAD - Missing automatic routing and config caching
public class MyScript : IScript
{
    public void Execute(string functionName, string argument)
    {
        // Manual routing needed
        switch (functionName)
        {
            case "Function1": Function1(); break;
            // ...
        }
    }
}
```

### ✅ DO: Inherit from AbstractScript

```csharp
// GOOD - Automatic routing and config caching
public class MyScript : AbstractScript
{
    public void Function1() { }
    public void Function2() { }
}
```

### ❌ DON'T: Read Configuration Repeatedly

```csharp
// BAD - Reads configuration every time
public void DoSomething()
{
    var config = DependencyManager.Resolve<IConfigurationClient>().ReadConfig();
    var setting = config.SomeSetting;
}
```

### ✅ DO: Use Config Property

```csharp
// GOOD - Uses cached configuration
public void DoSomething()
{
    var setting = Config.SomeSetting;
}
```

### ❌ DON'T: Mix Concerns in One Script

```csharp
// BAD - Customer, order, and payment all in one
public class EverythingScript : AbstractScript
{
    public void CreateCustomer() { }
    public void PlaceOrder() { }
    public void ProcessPayment() { }
}
```

### ✅ DO: Separate by Domain

```csharp
// GOOD - Each script has single responsibility
public class CustomerScript : AbstractScript
{
    public void CreateCustomer() { }
    public void SearchCustomer() { }
}

public class OrderScript : AbstractScript
{
    public void PlaceOrder() { }
}

public class PaymentScript : AbstractScript
{
    public void ProcessPayment() { }
}
```

## Testing Strategies

### Unit Testing Scripts

```csharp
[Test]
public void TestCustomerCreation()
{
    // Arrange
    var mockOpsContext = new Mock<IOpsContextClient>();
    var mockLogger = new Mock<ILogManager>();
    var mockCustomerClient = new Mock<ICustomerClient>();

    mockOpsContext.Setup(x => x.GetTextInput(It.IsAny<string>()))
        .Returns("John Doe");

    var script = new CustomerScript(
        mockOpsContext.Object,
        mockLogger.Object,
        mockCustomerClient.Object
    );

    // Act
    script.Execute("CreateCustomer", null);

    // Assert
    mockCustomerClient.Verify(x => x.Create("John Doe"), Times.Once);
    mockOpsContext.Verify(x => x.ShowMessage(It.IsAny<string>()), Times.Once);
}
```

### Integration Testing

```csharp
[Test]
public void TestScriptWithRealConfiguration()
{
    // Arrange
    var stubConfig = new StubConfigurationClient(appStatus);
    var config = stubConfig.ReadConfig();

    // Register in DI
    DependencyManager.RegisterByInstance<IConfigurationClient>(stubConfig);

    var script = DependencyManager.Resolve<ExportScript>();

    // Act
    script.DailyExport();

    // Assert
    Assert.IsTrue(File.Exists(Path.Combine(config.OutputFolder, "export.xml")));
}
```

## Summary

The Script Pattern provides:
- ✅ Automatic method routing via reflection
- ✅ Configuration caching with auto-refresh
- ✅ Dual entry points (button clicks + events)
- ✅ Flexible parameter support
- ✅ Dependency injection support
- ✅ Consistent error handling
- ✅ Testability

**Always:**
- Inherit from AbstractScript
- Include Version script
- Log operations
- Handle user cancellation
- Validate prerequisites
- Use dependency injection
- Access Config via property

**Never:**
- Implement IScript directly
- Read configuration repeatedly
- Mix unrelated concerns
- Hard-code configuration values
- Ignore error handling
- Skip logging
