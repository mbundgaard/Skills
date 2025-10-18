# Scale-Appropriate Pattern Templates

**Decision Guide: Choose template based on your project metrics**

## Project Size Classification

| Size | Scripts | Events | Config Lines | Example Projects |
|------|---------|--------|--------------|------------------|
| **Simple** | 2-5 | 1-5 | 10-20 (flat) | Dispense integration |
| **Medium** | 5-15 | 5-15 | 50-100 (nested) | Loyalty, Export, Timekeeping |
| **Complex** | 15+ | 15+ | 200+ (deep) | Multi-domain operations |

---

## Simple Project Template (2-5 Scripts)

**Use when:** Focused integration with minimal business logic
**Example:** Dispense posting, simple inventory operations

### IScript Interface: Simplified Variant
```csharp
// Contracts/IScript.cs
public interface IScript
{
    void Execute(string functionName, string argument);
}
```

### Script Implementation
```csharp
// Scripts/DispensePosting.cs
public class DispensePosting : IScript
{
    private readonly ILogManager _logger;
    private readonly IDatabaseClient _databaseClient;
    private readonly IConfigurationClient _configurationClient;

    public DispensePosting(ILogManager logger, IDatabaseClient databaseClient, IConfigurationClient configurationClient)
    {
        _logger = logger;
        _databaseClient = databaseClient;
        _configurationClient = configurationClient;
    }

    public void Execute(string functionName, string argument)
    {
        _logger.LogInfo("Processing dispense posting");

        var config = _configurationClient.ReadConfig();
        var dispenses = _databaseClient.GetPendingDispenses();

        foreach (var dispense in dispenses)
        {
            ProcessDispense(dispense, config);
        }

        _logger.LogInfo($"Processed {dispenses.Count()} dispenses");
    }

    private void ProcessDispense(Dispense dispense, Config config)
    {
        // Business logic
    }
}
```

### Configuration: Minimal Flat Structure
```csharp
// Entities/Config.cs
public class Config
{
    public string DatabaseConnectionString { get; set; }
    public int DispenseTenderId { get; set; }
    public decimal DispenseAmount { get; set; }
}
```

### Event Registration: Inline (1-5 events)
```csharp
// In SimphonyExtensibilityApplication constructor
public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
{
    // ... initialization

    var eventHelper = DependencyManager.Resolve<SimphonyEventHelper>();

    // Inline registration for few events
    SimphonyEventHelper.Register(this, "DispensePostedEvent", nameof(DispensePosting));
    SimphonyEventHelper.Register(this, "DispenseVoidEvent", nameof(DispensePosting));
}
```

### Dependencies Registration
```csharp
// Dependency/SimphonyDependencies.cs
public class SimphonyDependencies : AbstractDependencyInstaller
{
    public override void Install()
    {
        // Core
        DependencyManager.RegisterByInstance(new Status());
        DependencyManager.RegisterByType<ILogManager, LogManager>();
        DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<ConsoleLogger>());
        DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<FileLogger>());

        // Clients
        DependencyManager.RegisterByType<IConfigurationClient, SimphonyConfigurationClient>();
        DependencyManager.RegisterByType<IOpsContextClient, SimphonyOpsContextClient>();
        DependencyManager.RegisterByType<IDatabaseClient, DatabaseClient>();

        // Scripts (only 2-3)
        DependencyManager.RegisterByType<IScript, DispensePosting>(nameof(DispensePosting));
        DependencyManager.RegisterByType<IScript, Version>(nameof(Version));
    }
}
```

### File Structure
```
SimpleProject/
├── Clients/
│   ├── Configuration/
│   ├── Database/
│   └── OpsContext/
├── Contracts/
│   ├── Clients/
│   ├── Logging/
│   └── IScript.cs
├── Dependency/
├── Entities/
│   ├── Config.cs (10-20 lines)
│   ├── Status.cs
│   └── WorkstationInfo.cs
├── Helpers/
├── Logging/
├── Scripts/
│   ├── DispensePosting.cs
│   └── Version.cs
├── Serializers/
└── SimphonyExtensibilityApplication.cs
```

---

## Medium Project Template (5-15 Scripts)

**Use when:** Multi-feature integration with moderate complexity
**Example:** Loyalty, time-keeping, export processing

### IScript Interface: Simplified Variant
```csharp
// Contracts/IScript.cs
public interface IScript
{
    void Execute(string functionName, string argument);
}
```

### Script Implementation (Multi-Function)
```csharp
// Scripts/StoredValue.cs
public class StoredValue : IScript
{
    private readonly ILogManager _logger;
    private readonly IOpsContextClient _opsContext;
    private readonly IStoredValueClient _storedValueClient;

    public StoredValue(ILogManager logger, IOpsContextClient opsContext, IStoredValueClient storedValueClient)
    {
        _logger = logger;
        _opsContext = opsContext;
        _storedValueClient = storedValueClient;
    }

    public void Execute(string functionName, string argument)
    {
        _logger.LogInfo($"StoredValue: {functionName}");

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

            case "Delete":
                DeleteAccount(argument);
                break;

            default:
                throw new Exception($"Function {functionName} is not supported");
        }
    }

    private void IssueCard(string cardNumber)
    {
        var checkInfo = _opsContext.GetCheckInfo();
        if (checkInfo == null)
            throw new Exception("No open check - cannot issue card");

        var account = _storedValueClient.IssueCard(cardNumber, checkInfo.CheckTotalDue);
        _opsContext.ShowMessage($"Card {cardNumber} issued with ${account.Balance}");
    }

    private void GetBalance(string cardNumber)
    {
        var account = _storedValueClient.GetAccount(cardNumber);
        _opsContext.ShowMessage($"Card {cardNumber}: ${account.Balance}");
    }

    private void RedeemCard(string cardNumber)
    {
        var checkInfo = _opsContext.GetCheckInfo();
        var account = _storedValueClient.Redeem(cardNumber, checkInfo.CheckTotalDue);
        _opsContext.PostCheckDetail(CheckDetailType.Payment, account.TenderId, -account.AmountRedeemed, "Stored Value");
    }

    private void DeleteAccount(string cardNumber)
    {
        _storedValueClient.DeleteAccount(cardNumber);
        _opsContext.ShowMessage($"Card {cardNumber} deleted");
    }
}
```

### Configuration: Medium Complexity (Nested)
```csharp
// Entities/Config.cs
public class Config
{
    // API configuration
    public string ApiUrl { get; set; }
    public string ApiUsername { get; set; }
    public string ApiPassword { get; set; }

    // Tender configuration
    public int StoredValueTender { get; set; }
    public int StoredValueServiceCharge { get; set; }

    // Email settings
    public string EmailRecipients { get; set; }
    public string SendGridApiKey { get; set; }
}
```

### Event Registration: Hybrid Array (5-15 events)
```csharp
// In SimphonyExtensibilityApplication constructor
public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
{
    // ... initialization

    // ⭐ Hybrid approach - array + loop
    var staticEvents = new[]
    {
        new Event { Name = "BeginCheckPreviewEvent", Script = nameof(VerifyContractName) },
        new Event { Name = "ProcessCheckEvent", Script = nameof(ProcessCheck) },
        new Event { Name = "CloseCheckEvent", Script = nameof(ExportCheck) },
        new Event { Name = "VoidCheckEvent", Script = nameof(VoidCheck) },
        new Event { Name = "CustomEvent1", Script = nameof(CustomHandler) },
        new Event { Name = "CustomEvent2", Script = nameof(CustomHandler) },
        new Event { Name = "CustomEvent3", Script = nameof(CustomHandler) },
        new Event { Name = "CustomEvent4", Script = nameof(CustomHandler) },
    };

    var eventHelper = DependencyManager.Resolve<SimphonyEventHelper>();
    foreach (var evt in staticEvents)
    {
        eventHelper.Register(this, evt.Name, evt.Script);
    }
}

// Supporting entity
public class Event
{
    public string Name { get; set; }
    public string Script { get; set; }
}
```

### IEventHandler Pattern (Optional - Medium projects)
```csharp
// Contracts/IEventHandler.cs
public interface IEventHandler
{
    EventProcessingResult ProcessEvent(AbstractEventArgs eventArgs);
}

// EventHandlers/StoredValueVoidTransaction.cs
public class StoredValueVoidTransaction : IEventHandler
{
    private readonly ILogManager _logger;
    private readonly IStoredValueClient _storedValueClient;

    public StoredValueVoidTransaction(ILogManager logger, IStoredValueClient storedValueClient)
    {
        _logger = logger;
        _storedValueClient = storedValueClient;
    }

    public EventProcessingResult ProcessEvent(AbstractEventArgs eventArgs)
    {
        _logger.LogInfo("Processing void transaction");

        if (eventArgs is OpsTenderMediaVoidArgs tenderArgs)
        {
            // Reverse stored value transaction
            _storedValueClient.ReverseTransaction(tenderArgs.TransactionId);
        }

        return new EventProcessingResult();
    }
}

// Registration in SimphonyDependencies
DependencyManager.RegisterByType<IEventHandler, StoredValueVoidTransaction>(nameof(StoredValueVoidTransaction));

// Registration in SimphonyExtensibilityApplication
SimphonyEventHelper.RegisterServiceChargePreviewEvent(this, nameof(StoredValueVoidTransaction));
SimphonyEventHelper.RegisterTenderMediaVoidEvent(this, nameof(StoredValueVoidTransaction));
```

### IService Pattern (Optional - Medium projects with background processing)
```csharp
// Contracts/IService.cs
public interface IService
{
    void Start();
}

// Services/NewOrdersService.cs
public class NewOrdersService : IService
{
    private readonly ILogManager _logger;
    private readonly IDatabaseClient _databaseClient;
    private readonly ILoyaltyClient _loyaltyClient;
    private Timer _runTimer;

    public NewOrdersService(ILogManager logger, IDatabaseClient databaseClient, ILoyaltyClient loyaltyClient)
    {
        _logger = logger;
        _databaseClient = databaseClient;
        _loyaltyClient = loyaltyClient;
    }

    public void Start()
    {
#if DEBUG
        _logger.LogInfo("Starting service (runs immediately in debug)");
        _runTimer = new Timer(Run, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(15));
#else
        _logger.LogInfo("Starting service (first run in 30 sec)");
        _runTimer = new Timer(Run, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(15));
#endif
    }

    private void Run(object dummy)
    {
        try
        {
            _logger.LogInfo("Checking for new orders");
            var newOrders = _databaseClient.GetOrdersNotExported();

            foreach (var order in newOrders)
            {
                ProcessOrder(order);
            }
        }
        catch (Exception e)
        {
            _logger.LogException("Error running service", e);
        }
    }

    private void ProcessOrder(Order order)
    {
        // Business logic
    }
}

// Registration in SimphonyDependencies
DependencyManager.RegisterByType<IService, NewOrdersService>(nameof(NewOrdersService));

// Start in SimphonyExtensibilityApplication
DependencyManager.ResolveAll<IService>().ToList().ForEach(x => x.Start());
```

### File Structure
```
MediumProject/
├── Clients/
│   ├── Configuration/
│   ├── Database/
│   ├── Email/
│   ├── LoyaltyClient/
│   ├── OpsContext/
│   └── StoredValue/
├── Contracts/
│   ├── Clients/
│   ├── Logging/
│   ├── IScript.cs
│   ├── IEventHandler.cs
│   └── IService.cs
├── Dependency/
├── Entities/
│   ├── Args/
│   ├── Config.cs (50-100 lines)
│   └── [Domain entities]
├── EventHandlers/
│   └── StoredValueVoidTransaction.cs
├── Helpers/
├── Logging/
├── Scripts/
│   ├── Admin.cs
│   ├── Loyalty.cs
│   ├── StoredValue.cs
│   └── Version.cs
├── Serializers/
├── Services/
│   └── NewOrdersService.cs
└── SimphonyExtensibilityApplication.cs
```

---

## Complex Project Template (15+ Scripts)

**Use when:** Multi-domain operations with extensive business logic
**Example:** Comprehensive POS operations helper

### IScript Interface: Full Variant (Execute + Event)
```csharp
// Contracts/IScript.cs
public interface IScript
{
    void Execute(string functionName, string argument);
    void Event();
}
```

### Script Implementation (Event-Driven)
```csharp
// Scripts/CheckProcessor.cs
public class CheckProcessor : IScript
{
    private readonly ILogManager _logger;
    private readonly IOpsContextClient _opsContext;
    private readonly IConfigurationClient _configurationClient;

    public CheckProcessor(ILogManager logger, IOpsContextClient opsContext, IConfigurationClient configurationClient)
    {
        _logger = logger;
        _opsContext = opsContext;
        _configurationClient = configurationClient;
    }

    public void Execute(string functionName, string argument)
    {
        _logger.LogInfo($"CheckProcessor.Execute: {functionName}");

        switch (functionName)
        {
            case "BeginCheck":
                BeginCheck(argument);
                break;

            case "ProcessCheck":
                ProcessCheck(argument);
                break;

            case "CloseCheck":
                CloseCheck(argument);
                break;

            case "VoidCheck":
                VoidCheck(argument);
                break;

            default:
                throw new Exception($"Unknown function: {functionName}");
        }
    }

    public void Event()
    {
        _logger.LogInfo("CheckProcessor.Event");

        var checkInfo = _opsContext.GetCheckInfo();
        var config = _configurationClient.ReadConfig();

        // Event-driven processing
        ProcessCheckEvent(checkInfo, config);
    }

    private void BeginCheck(string argument) { /* ... */ }
    private void ProcessCheck(string argument) { /* ... */ }
    private void CloseCheck(string argument) { /* ... */ }
    private void VoidCheck(string argument) { /* ... */ }
    private void ProcessCheckEvent(CheckInfo checkInfo, Config config) { /* ... */ }
}
```

### Configuration: Complex Nested Structure
```csharp
// Entities/Config.cs
public class Config
{
    // Property configuration
    public int PropertyNumber { get; set; }
    public string PropertyName { get; set; }

    // Event configuration (configuration-driven)
    public Event[] Events { get; set; }

    // Export processor configuration
    public ExportProcessor[] ExportProcessors { get; set; }

    // Menu item mappings
    public MenuItemMapping[] MenuItemMappings { get; set; }

    // Discount mappings
    public DiscountMapping[] DiscountMappings { get; set; }

    // Tender media mappings
    public TenderMapping[] TenderMappings { get; set; }

    // Report configuration
    public ReportConfig[] ReportConfigs { get; set; }

    // Workflow configuration
    public WorkflowConfig Workflow { get; set; }
}

// Nested configuration classes
public class Event
{
    public string Name { get; set; }
    public string Script { get; set; }
    public string Function { get; set; }
}

public class ExportProcessor
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string ExportUrl { get; set; }
    public Dictionary<string, string> Settings { get; set; }
}

public class MenuItemMapping
{
    public int MenuItemId { get; set; }
    public string ExternalId { get; set; }
    public string Category { get; set; }
}

// ... more nested classes (200+ lines total)
```

### Event Registration: Configuration-Driven (15+ events)
```csharp
// In SimphonyExtensibilityApplication constructor
public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
{
    // ... initialization

    var config = DependencyManager.Resolve<IConfigurationClient>().ReadConfig();
    var eventHelper = DependencyManager.Resolve<SimphonyEventHelper>();

    // Configuration-driven event registration
    foreach (var eventConfig in config.Events)
    {
        eventHelper.Register(this, eventConfig.Name, eventConfig.Script, eventConfig.Function);
    }
}
```

### Dependencies Registration (24 scripts)
```csharp
// Dependency/SimphonyDependencies.cs
public class SimphonyDependencies : AbstractDependencyInstaller
{
    public override void Install()
    {
        // Core
        DependencyManager.RegisterByInstance(new Status());
        DependencyManager.RegisterByType<ILogManager, LogManager>();
        DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<ConsoleLogger>());
        DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<FileLogger>());
        DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<EGatewayLogger>());

        // Clients
        DependencyManager.RegisterByType<IConfigurationClient, SimphonyConfigurationClient>();
        DependencyManager.RegisterByType<IOpsContextClient, SimphonyOpsContextClient>();
        DependencyManager.RegisterByType<IDatabaseClient, SimphonyDatabaseClient>();

        // Factories
        DependencyManager.RegisterByType<IDbConnectionFactory, SimphonyDbConnectionFactory>();

        // Serializers (multiple named)
        DependencyManager.RegisterByType<ISerializer, JsonSerializer>("json");
        DependencyManager.RegisterByType<ISerializer, XmlSerializer>("xml");
        DependencyManager.RegisterByType<ISerializer, Base64Serializer>("base64");

        // Scripts (24 scripts - organized by domain)
        // Check processing
        DependencyManager.RegisterByType<IScript, CheckProcessor>(nameof(CheckProcessor));
        DependencyManager.RegisterByType<IScript, BeginCheck>(nameof(BeginCheck));
        DependencyManager.RegisterByType<IScript, ProcessCheck>(nameof(ProcessCheck));
        // ... 21 more scripts organized by domain
    }
}
```

### File Structure
```
ComplexProject/
├── Clients/
│   ├── Configuration/
│   ├── Database/
│   ├── OpsContext/
│   └── [10+ client types]
├── Contracts/
│   ├── Clients/ (15+ interfaces)
│   ├── Factories/
│   ├── Logging/
│   └── IScript.cs
├── Dependency/
├── Entities/
│   ├── Args/
│   ├── Config.cs (400+ lines)
│   └── [50+ entity files]
├── Factories/
├── Helpers/
├── Logging/
├── Scripts/
│   └── [24 script files organized by domain]
├── Serializers/
│   ├── JsonSerializer.cs
│   ├── XmlSerializer.cs
│   └── Base64Serializer.cs
└── SimphonyExtensibilityApplication.cs
```

---

## Decision Matrix

### Choose Simple Template When:
- ✅ 2-5 scripts
- ✅ 1-5 events
- ✅ Single focused domain (dispense, inventory count, etc.)
- ✅ Flat configuration (10-20 lines)
- ✅ No background processing
- ✅ No separate event handlers

### Choose Medium Template When:
- ✅ 5-15 scripts
- ✅ 5-15 events
- ✅ Multiple related features (loyalty + stored value, time-keeping + reporting)
- ✅ Moderate configuration (50-100 lines, 1-2 nesting)
- ✅ May need background services
- ✅ May benefit from IEventHandler pattern

### Choose Complex Template When:
- ✅ 15+ scripts
- ✅ 15+ events
- ✅ Multi-domain operations (check + inventory + reporting + exports)
- ✅ Complex configuration (200+ lines, 3-4 nesting)
- ✅ Configuration-driven behavior
- ✅ Event handling in scripts (Event() method)

---

## Migration Path

### Simple → Medium:
1. Keep simplified IScript interface
2. Add IEventHandler for separate event logic
3. Add IService for background processing if needed
4. Switch from inline to hybrid array event registration
5. Expand configuration to nested structure

### Medium → Complex:
1. Switch to full IScript interface (add Event() method)
2. Move to configuration-driven event registration
3. Organize scripts by domain (folders or namespaces)
4. Deep nest configuration (3-4 levels)
5. Add multiple serializers with named registration

---

## Common Patterns Across All Sizes

**Always include:**
- ExceptionHelper.GetFirstException()
- File-based or compiler directive debug control
- Multi-target logging (Console, File, optionally EGateway)
- Version script
- Status tracking
- WorkstationInfo for audit trails
- Build automation (PostBuildEvent)

**Scale appropriately:**
- IScript interface variant
- Event registration approach
- Configuration complexity
- Number of clients
- Background services
