# Multi-Domain Operations Helper Patterns

Reference guide for implementing complex, multi-domain orchestration and operations helper functionality in Simphony Extension Applications.

**Source:** SimphonyOpsHelper project analysis (230 files, 24 scripts, high complexity)

---

## 1. Multi-Domain Orchestration Pattern

### Problem
Large POS operations require coordination across multiple domains:
- Check processing workflows
- Inventory management
- Reporting and analytics
- External system integrations
- Employee operations
- Configuration management
- Database operations

Each domain has its own logic, but operations often span multiple domains. Need a pattern that:
- Organizes code by domain
- Supports cross-domain operations
- Maintains single responsibility per script
- Enables reusable domain logic

### Solution: Domain-Specific Scripts with Cross-Domain Clients

**Domain organization:**
```
Scripts/
├── CheckProcessing/
│   ├── BeginCheckScript.cs
│   ├── ProcessCheckScript.cs
│   ├── CloseCheckScript.cs
│   └── VoidCheckScript.cs
├── Inventory/
│   ├── UpdateInventoryScript.cs
│   ├── DecrementStockScript.cs
│   └── InventoryReportScript.cs
├── Reporting/
│   ├── DailySalesReportScript.cs
│   ├── EmployeeReportScript.cs
│   └── CustomReportScript.cs
├── Integration/
│   ├── ExportToAccountingScript.cs
│   ├── SyncInventoryScript.cs
│   └── SendToKitchenScript.cs
└── Admin/
    ├── ConfigurationScript.cs
    ├── DatabaseMaintenanceScript.cs
    └── VersionScript.cs
```

**Cross-domain client abstraction:**
```csharp
// Check processing domain
public interface ICheckProcessingClient
{
    CheckData GetCheckData(long checkNumber);
    void ProcessDiscount(long checkNumber, int discountId, decimal amount);
    void ApplyServiceCharge(long checkNumber, int serviceChargeId, decimal amount);
    void UpdateCheckStatus(long checkNumber, CheckStatus status);
}

// Inventory domain
public interface IInventoryClient
{
    void DecrementStock(int menuItemId, decimal quantity);
    void IncrementStock(int menuItemId, decimal quantity);
    InventoryLevel GetInventoryLevel(int menuItemId);
    void UpdateReorderPoint(int menuItemId, int reorderPoint);
}

// Reporting domain
public interface IReportingClient
{
    ReportData GenerateReport(ReportType type, DateTime startDate, DateTime endDate);
    void EmailReport(string recipientEmail, ReportData report);
    void PrintReport(int printerNumber, ReportData report);
}

// External integration domain
public interface IIntegrationClient
{
    void SendToExternalSystem(string systemName, object data);
    IntegrationStatus GetIntegrationStatus(string systemName);
    void RetryFailedIntegrations();
}
```

**Script using multiple domains:**
```csharp
public class ProcessCheckWithInventoryScript : IScript
{
    private readonly ICheckProcessingClient _checkClient;
    private readonly IInventoryClient _inventoryClient;
    private readonly IReportingClient _reportingClient;
    private readonly ILogManager _logger;
    private readonly Config _config;

    public ProcessCheckWithInventoryScript(
        ICheckProcessingClient checkClient,
        IInventoryClient inventoryClient,
        IReportingClient reportingClient,
        ILogManager logger,
        IConfigurationClient configClient)
    {
        _checkClient = checkClient;
        _inventoryClient = inventoryClient;
        _reportingClient = reportingClient;
        _logger = logger;
        _config = configClient.ReadConfig();
    }

    public void Execute(string functionName, string argument)
    {
        try
        {
            long checkNumber = long.Parse(argument);

            _logger.LogInfo($"Processing check {checkNumber} with inventory update");

            // Domain 1: Get check data
            var checkData = _checkClient.GetCheckData(checkNumber);

            // Domain 2: Update inventory for each menu item
            foreach (var menuItem in checkData.MenuItems)
            {
                if (_config.TrackInventoryForMenuItem(menuItem.MenuItemNumber))
                {
                    _inventoryClient.DecrementStock(menuItem.MenuItemNumber, menuItem.Quantity);

                    // Check reorder point
                    var inventoryLevel = _inventoryClient.GetInventoryLevel(menuItem.MenuItemNumber);
                    if (inventoryLevel.Quantity <= inventoryLevel.ReorderPoint)
                    {
                        _logger.LogWarn($"Menu item {menuItem.MenuItemNumber} below reorder point");

                        // Domain 3: Generate low stock report
                        if (_config.EmailLowStockAlerts)
                        {
                            var report = _reportingClient.GenerateReport(ReportType.LowStock, DateTime.Now, DateTime.Now);
                            _reportingClient.EmailReport(_config.InventoryManagerEmail, report);
                        }
                    }
                }
            }

            // Domain 1: Update check status
            _checkClient.UpdateCheckStatus(checkNumber, CheckStatus.Processed);

            _logger.LogInfo($"Check {checkNumber} processed successfully");
        }
        catch (Exception e)
        {
            _logger.LogException("Error processing check with inventory", e);
            throw;
        }
    }
}
```

**Benefits:**
- Clear domain separation
- Reusable domain clients
- Single Responsibility Principle per script
- Easy to test individual domains
- Flexible composition of cross-domain operations

**When to use:**
- Large, complex POS operations
- Multiple business domains
- Cross-domain workflows
- Reusable domain logic across scripts

---

## 2. Configuration-Driven Event Registration Pattern

### Problem
Large applications with 15+ events face challenges:
- Too many inline event registrations clutter constructor
- Hard to enable/disable events without code changes
- Different environments may need different events
- Event configuration should be external for flexibility
- Need runtime configurability without recompilation

### Solution: Event Configuration in External Config with Dynamic Registration

**Configuration structure:**
```csharp
public class Config
{
    public Event[] Events { get; set; }
    // ... other configuration
}

public class Event
{
    public string Name { get; set; }
    public string Script { get; set; }
    public string Function { get; set; }
    public bool Enabled { get; set; }
}
```

**Configuration example (JSON):**
```json
{
  "Events": [
    {
      "Name": "BeginCheckPreviewEvent",
      "Script": "BeginCheckScript",
      "Function": "VerifyCheck",
      "Enabled": true
    },
    {
      "Name": "ProcessCheckEvent",
      "Script": "ProcessCheckScript",
      "Function": "ProcessWithInventory",
      "Enabled": true
    },
    {
      "Name": "CloseCheckEvent",
      "Script": "CloseCheckScript",
      "Function": "ExportCheck",
      "Enabled": true
    },
    {
      "Name": "VoidCheckEvent",
      "Script": "VoidCheckScript",
      "Function": "",
      "Enabled": true
    },
    {
      "Name": "ServiceChargePreviewEvent",
      "Script": "DiscountScript",
      "Function": "ApplyDiscount",
      "Enabled": false
    }
    // ... 15+ more events
  ]
}
```

**Dynamic event registration:**
```csharp
public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
{
    DependencyManager.RegisterByInstance(new OpsExtensibilityEnvironment(ExecutionContext.CurrentExecutionContext));
    DependencyManager.Install<SimphonyDependencies>();

    DependencyManager.Resolve<Status>().WorkstationId = OpsContext.WorkstationID;

    _logger = DependencyManager.Resolve<ILogManager>();
    _logger.LogInfo($"Instantiating {VersionHelper.NameAndVersion}");

    // Load configuration
    var config = DependencyManager.Resolve<IConfigurationClient>().ReadConfig();

    // Register only enabled events from configuration
    var eventHelper = DependencyManager.Resolve<SimphonyEventHelper>();

    foreach (var eventConfig in config.Events.Where(x => x.Enabled))
    {
        _logger.LogInfo($"Registering event: {eventConfig.Name} -> {eventConfig.Script}.{eventConfig.Function}");

        eventHelper.Register(this, eventConfig.Name, eventConfig.Script, eventConfig.Function);
    }
}
```

**SimphonyEventHelper with comprehensive event support:**
```csharp
public class SimphonyEventHelper
{
    private readonly ILogManager _logger;

    public SimphonyEventHelper(ILogManager logger)
    {
        _logger = logger;
    }

    public void Register(OpsExtensibilityApplication app, string eventName, string scriptName, string functionName = "")
    {
        switch (eventName)
        {
            // Check lifecycle events
            case "BeginCheckPreviewEvent":
                app.BeginCheckPreviewEvent += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            case "ProcessCheckEvent":
                app.ProcessCheckEvent += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            case "CloseCheckEvent":
                app.CloseCheckEvent += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            case "VoidCheckEvent":
                app.VoidCheckEvent += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            // Service charge events
            case "ServiceChargePreviewEvent":
                app.ServiceChargePreviewEvent += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            case "ServiceChargeCalculationEvent":
                app.ServiceChargeCalculationEvent += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            // Tender events
            case "TenderMediaPreviewEvent":
                app.TenderMediaPreviewEvent += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            case "TenderMediaVoidEvent":
                app.TenderMediaVoidEvent += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            // Menu item events
            case "MenuItemPreviewEvent":
                app.MenuItemPreviewEvent += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            case "MenuItemVoidEvent":
                app.MenuItemVoidEvent += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            // Discount events
            case "DiscountPreviewEvent":
                app.DiscountPreviewEvent += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            // Employee events
            case "EmployeeSignInEvent":
                app.EmployeeSignInEvent += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            case "EmployeeSignOutEvent":
                app.EmployeeSignOutEvent += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            // Custom events (varies by Simphony version)
            case "CustomEvent1":
                app.CustomEvent1 += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            case "CustomEvent2":
                app.CustomEvent2 += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            // ... more events as needed (20+ total)

            default:
                throw new Exception($"Unknown event: {eventName}");
        }
    }

    private void HandleEvent(string scriptName, string functionName, EventArgs args)
    {
        try
        {
            var script = DependencyManager.Resolve<IScript>(scriptName);

            _logger.LogDebug($"Executing {scriptName}.{functionName ?? "Execute"}");

            script.Execute(functionName, SerializeArgs(args));
        }
        catch (Exception e)
        {
            var first = ExceptionHelper.GetFirstException(e);
            _logger.LogException($"Error handling event in script {scriptName}", first);
        }
    }

    private string SerializeArgs(EventArgs args)
    {
        // Serialize event arguments if needed
        // Most scripts don't use the serialized args
        return string.Empty;
    }
}
```

**Benefits:**
- External configuration of events
- Enable/disable events without recompilation
- Different event sets per environment
- Centralized event management
- Easy to add new events
- Configuration-driven behavior

**When to use:**
- 15+ events
- Need runtime configurability
- Different environments (dev, test, prod) need different events
- Frequent event additions/removals
- Complex event workflows

**Comparison with other approaches:**

| Approach | Event Count | Runtime Config | Compile-Time Safety | Best For |
|----------|-------------|----------------|---------------------|----------|
| **Inline** | 1-5 | No | Yes | Simple projects |
| **Hybrid Array** | 5-15 | No | Yes | Medium projects |
| **Configuration-Driven** | 15+ | Yes | No | Complex projects, multiple environments |

---

## 3. Complex Event Workflow Pattern

### Problem
Business workflows often require:
- Multi-step processing triggered by single event
- Conditional logic based on check state
- Error handling at each step
- Rollback capability on failure
- Audit trail of each step
- Validation before processing

Traditional event handlers become difficult to maintain when workflows are complex.

### Solution: Workflow Coordinator with Step-Based Processing

**Workflow coordinator:**
```csharp
public interface IWorkflowStep
{
    string Name { get; }
    bool ShouldExecute(WorkflowContext context);
    StepResult Execute(WorkflowContext context);
    void Rollback(WorkflowContext context);
}

public class WorkflowContext
{
    public long CheckNumber { get; set; }
    public CheckData CheckData { get; set; }
    public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

    public T GetData<T>(string key)
    {
        return Data.ContainsKey(key) ? (T)Data[key] : default(T);
    }

    public void SetData(string key, object value)
    {
        Data[key] = value;
    }
}

public class StepResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public bool ShouldContinue { get; set; } = true;
}
```

**Workflow coordinator:**
```csharp
public class WorkflowCoordinator
{
    private readonly ILogManager _logger;
    private readonly List<IWorkflowStep> _steps;

    public WorkflowCoordinator(ILogManager logger)
    {
        _logger = logger;
        _steps = new List<IWorkflowStep>();
    }

    public void AddStep(IWorkflowStep step)
    {
        _steps.Add(step);
    }

    public bool ExecuteWorkflow(WorkflowContext context)
    {
        var executedSteps = new List<IWorkflowStep>();

        try
        {
            foreach (var step in _steps)
            {
                if (!step.ShouldExecute(context))
                {
                    _logger.LogDebug($"Skipping step: {step.Name}");
                    continue;
                }

                _logger.LogInfo($"Executing workflow step: {step.Name}");

                var result = step.Execute(context);

                if (!result.Success)
                {
                    _logger.LogError($"Step {step.Name} failed: {result.ErrorMessage}");

                    // Rollback executed steps in reverse order
                    RollbackSteps(executedSteps, context);

                    return false;
                }

                executedSteps.Add(step);

                if (!result.ShouldContinue)
                {
                    _logger.LogInfo($"Workflow terminated by step: {step.Name}");
                    break;
                }
            }

            _logger.LogInfo("Workflow completed successfully");
            return true;
        }
        catch (Exception e)
        {
            _logger.LogException("Workflow execution error", e);

            // Rollback on exception
            RollbackSteps(executedSteps, context);

            return false;
        }
    }

    private void RollbackSteps(List<IWorkflowStep> steps, WorkflowContext context)
    {
        _logger.LogWarn($"Rolling back {steps.Count} workflow steps");

        // Rollback in reverse order
        for (int i = steps.Count - 1; i >= 0; i--)
        {
            try
            {
                _logger.LogInfo($"Rolling back step: {steps[i].Name}");
                steps[i].Rollback(context);
            }
            catch (Exception e)
            {
                _logger.LogException($"Error rolling back step {steps[i].Name}", e);
            }
        }
    }
}
```

**Example workflow steps:**
```csharp
public class ValidateCheckStep : IWorkflowStep
{
    private readonly ICheckProcessingClient _checkClient;
    private readonly ILogManager _logger;

    public string Name => "ValidateCheck";

    public bool ShouldExecute(WorkflowContext context) => true;

    public StepResult Execute(WorkflowContext context)
    {
        var checkData = _checkClient.GetCheckData(context.CheckNumber);

        if (checkData == null)
        {
            return new StepResult
            {
                Success = false,
                ErrorMessage = "Check not found"
            };
        }

        if (checkData.Total <= 0)
        {
            return new StepResult
            {
                Success = false,
                ErrorMessage = "Check total must be greater than zero"
            };
        }

        context.CheckData = checkData;
        return new StepResult { Success = true };
    }

    public void Rollback(WorkflowContext context)
    {
        // No rollback needed for validation
    }
}

public class UpdateInventoryStep : IWorkflowStep
{
    private readonly IInventoryClient _inventoryClient;
    private readonly Config _config;
    private readonly ILogManager _logger;

    public string Name => "UpdateInventory";

    public bool ShouldExecute(WorkflowContext context)
    {
        // Only execute if inventory tracking is enabled
        return _config.EnableInventoryTracking;
    }

    public StepResult Execute(WorkflowContext context)
    {
        var inventoryUpdates = new List<InventoryUpdate>();

        foreach (var menuItem in context.CheckData.MenuItems)
        {
            if (_config.TrackInventoryForMenuItem(menuItem.MenuItemNumber))
            {
                _inventoryClient.DecrementStock(menuItem.MenuItemNumber, menuItem.Quantity);

                inventoryUpdates.Add(new InventoryUpdate
                {
                    MenuItemId = menuItem.MenuItemNumber,
                    Quantity = menuItem.Quantity
                });
            }
        }

        // Store updates in context for potential rollback
        context.SetData("InventoryUpdates", inventoryUpdates);

        return new StepResult { Success = true };
    }

    public void Rollback(WorkflowContext context)
    {
        var updates = context.GetData<List<InventoryUpdate>>("InventoryUpdates");

        if (updates != null)
        {
            _logger.LogInfo($"Rolling back {updates.Count} inventory updates");

            foreach (var update in updates)
            {
                _inventoryClient.IncrementStock(update.MenuItemId, update.Quantity);
            }
        }
    }
}

public class ExportToAccountingStep : IWorkflowStep
{
    private readonly IIntegrationClient _integrationClient;
    private readonly Config _config;

    public string Name => "ExportToAccounting";

    public bool ShouldExecute(WorkflowContext context)
    {
        return _config.EnableAccountingExport;
    }

    public StepResult Execute(WorkflowContext context)
    {
        var exportData = new
        {
            CheckNumber = context.CheckData.CheckNumber,
            Total = context.CheckData.Total,
            Tax = context.CheckData.Tax,
            Items = context.CheckData.MenuItems
        };

        _integrationClient.SendToExternalSystem("Accounting", exportData);

        return new StepResult { Success = true };
    }

    public void Rollback(WorkflowContext context)
    {
        // Send reversal to accounting system
        var reversalData = new
        {
            CheckNumber = context.CheckData.CheckNumber,
            ReversalReason = "Workflow rollback"
        };

        _integrationClient.SendToExternalSystem("Accounting", reversalData);
    }
}
```

**Using workflow in script:**
```csharp
public class ProcessCheckWorkflowScript : IScript
{
    private readonly WorkflowCoordinator _workflow;
    private readonly ILogManager _logger;

    public ProcessCheckWorkflowScript(ILogManager logger)
    {
        _logger = logger;
        _workflow = new WorkflowCoordinator(logger);

        // Configure workflow steps
        _workflow.AddStep(DependencyManager.Resolve<ValidateCheckStep>());
        _workflow.AddStep(DependencyManager.Resolve<UpdateInventoryStep>());
        _workflow.AddStep(DependencyManager.Resolve<ApplyLoyaltyDiscountStep>());
        _workflow.AddStep(DependencyManager.Resolve<ExportToAccountingStep>());
        _workflow.AddStep(DependencyManager.Resolve<SendToKitchenStep>());
    }

    public void Execute(string functionName, string argument)
    {
        long checkNumber = long.Parse(argument);

        var context = new WorkflowContext
        {
            CheckNumber = checkNumber
        };

        var success = _workflow.ExecuteWorkflow(context);

        if (!success)
        {
            _logger.LogError($"Workflow failed for check {checkNumber}");
            throw new Exception("Check processing workflow failed");
        }

        _logger.LogInfo($"Workflow completed for check {checkNumber}");
    }
}
```

**Benefits:**
- Clear workflow visualization
- Automatic rollback on failure
- Conditional step execution
- Reusable workflow steps
- Audit trail via logging
- Easy to test individual steps

**When to use:**
- Multi-step business processes
- Need for transactional behavior
- Complex conditional logic
- Rollback requirements
- Multiple integration points
- Error-prone operations

---

## 4. Database Helper Pattern

### Problem
Complex operations require sophisticated database queries:
- Dynamic SQL generation
- Parameterized queries for safety
- Connection management
- Transaction support
- Query result mapping to objects
- Bulk operations

Need abstraction that provides:
- SQL injection protection
- Type-safe parameters
- Reusable query patterns
- Transaction management

### Solution: Database Service with Fluent Query Builder

**SqlQuery builder:**
```csharp
public class SqlQuery
{
    private StringBuilder _sql;
    private List<SqlParameter> _parameters;

    public SqlQuery()
    {
        _sql = new StringBuilder();
        _parameters = new List<SqlParameter>();
    }

    public SqlQuery Select(params string[] columns)
    {
        _sql.Append("SELECT ");
        _sql.Append(string.Join(", ", columns));
        return this;
    }

    public SqlQuery From(string table)
    {
        _sql.Append($" FROM {table}");
        return this;
    }

    public SqlQuery Where(string condition, object value = null)
    {
        if (_sql.ToString().Contains("WHERE"))
        {
            _sql.Append($" AND {condition}");
        }
        else
        {
            _sql.Append($" WHERE {condition}");
        }

        if (value != null)
        {
            var paramName = $"@p{_parameters.Count}";
            _parameters.Add(new SqlParameter
            {
                Name = paramName,
                Value = value
            });
        }

        return this;
    }

    public SqlQuery OrderBy(string column, bool descending = false)
    {
        _sql.Append($" ORDER BY {column}");

        if (descending)
        {
            _sql.Append(" DESC");
        }

        return this;
    }

    public SqlQuery Top(int count)
    {
        _sql.Insert(_sql.ToString().IndexOf("SELECT") + 7, $"TOP {count} ");
        return this;
    }

    public string ToSql()
    {
        return _sql.ToString();
    }

    public List<SqlParameter> Parameters => _parameters;
}

public class SqlParameter
{
    public string Name { get; set; }
    public object Value { get; set; }
}
```

**DatabaseService implementation:**
```csharp
public class DatabaseService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogManager _logger;

    public DatabaseService(IDbConnectionFactory connectionFactory, ILogManager logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public IEnumerable<T> Query<T>(SqlQuery query) where T : class, new()
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = query.ToSql();

                foreach (var param in query.Parameters)
                {
                    var sqlParam = command.CreateParameter();
                    sqlParam.ParameterName = param.Name;
                    sqlParam.Value = param.Value ?? DBNull.Value;
                    command.Parameters.Add(sqlParam);
                }

                _logger.LogDebug($"Executing query: {command.CommandText}");

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return MapToEntity<T>(reader);
                    }
                }
            }
        }
    }

    public int Execute(SqlQuery query)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = query.ToSql();

                foreach (var param in query.Parameters)
                {
                    var sqlParam = command.CreateParameter();
                    sqlParam.ParameterName = param.Name;
                    sqlParam.Value = param.Value ?? DBNull.Value;
                    command.Parameters.Add(sqlParam);
                }

                _logger.LogDebug($"Executing command: {command.CommandText}");

                return command.ExecuteNonQuery();
            }
        }
    }

    private T MapToEntity<T>(IDataReader reader) where T : class, new()
    {
        var entity = new T();
        var properties = typeof(T).GetProperties();

        foreach (var prop in properties)
        {
            try
            {
                var ordinal = reader.GetOrdinal(prop.Name);
                var value = reader.GetValue(ordinal);

                if (value != DBNull.Value)
                {
                    prop.SetValue(entity, value);
                }
            }
            catch
            {
                // Property not in result set, skip
            }
        }

        return entity;
    }
}
```

**Usage in scripts:**
```csharp
public class ReportScript : IScript
{
    private readonly DatabaseService _databaseService;

    public void Execute(string functionName, string argument)
    {
        // Build query using fluent API
        var query = new SqlQuery()
            .Select("CheckNumber", "Total", "CloseTime", "EmployeeName")
            .From("Checks")
            .Where("CloseTime >= @p0", DateTime.Today)
            .Where("CloseTime < @p1", DateTime.Today.AddDays(1))
            .OrderBy("CloseTime", descending: true)
            .Top(100);

        // Execute query and get results
        var checks = _databaseService.Query<CheckSummary>(query).ToList();

        // Process results...
    }
}

public class CheckSummary
{
    public long CheckNumber { get; set; }
    public decimal Total { get; set; }
    public DateTime CloseTime { get; set; }
    public string EmployeeName { get; set; }
}
```

**Benefits:**
- SQL injection protection via parameters
- Fluent, readable query construction
- Type-safe result mapping
- Reusable query patterns
- Automatic connection management

**When to use:**
- Complex database queries
- Dynamic query building
- Need for SQL injection protection
- Type-safe data access
- Bulk data operations

---

## Summary

### When to Use These Patterns

**Use multi-domain ops patterns when building:**
- Large, complex POS operations
- Multi-domain workflows
- Event-driven orchestration
- Integration-heavy systems
- Complex reporting requirements

### Pattern Combination Recommendations

**Basic Operations Helper:**
1. Domain-specific clients for separation
2. Configuration-driven events for 15+ events
3. Database service for complex queries

**Advanced Operations Helper:**
Add workflow coordinator for multi-step processes, cross-domain orchestration, and rollback capability.

### Integration with Universal Patterns

These domain patterns build on universal Simphony Extension Application patterns:
- Use **Interface-First Design** for all domain clients
- Use **Custom DI Container** for client registration
- Use **Logging Framework** for audit trails and debugging
- Use **Configuration Management** for event registration and workflow settings
- Use **Full IScript Interface** for complex multi-function scripts

### Key Takeaways

1. **Domain separation enables growth** - Organize by business domain, not technical layer
2. **Configuration-driven events scale** - 15+ events need external configuration
3. **Workflows provide structure** - Multi-step processes need coordinator pattern
4. **Database helpers reduce errors** - Fluent query builders prevent SQL injection
5. **Cross-domain clients enable reuse** - Share domain logic across scripts
6. **Rollback capability is essential** - Complex workflows need transactional behavior
