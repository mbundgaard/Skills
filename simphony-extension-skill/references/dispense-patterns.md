# Simple Integration and Direct Posting Patterns

Reference guide for implementing simple, focused integrations with direct POS posting operations in Simphony Extension Applications.

**Source:** SspLiquidDispenseSystem project analysis (44 files, 2 scripts, low complexity)

---

## 1. Minimal Configuration Pattern

### Problem
Simple integrations don't need complex nested configuration structures. Common pitfalls:
- Over-engineering configuration for simple needs
- Deep nesting when flat structure suffices
- Configuration files larger than the business logic
- Difficult to understand simple requirements

Need a pattern that:
- Keeps configuration simple and flat
- Only includes what's needed
- Easy to read and modify
- Minimal lines of code

### Solution: Flat Configuration with Essential Properties Only

**Minimal configuration structure:**
```csharp
public class Config
{
    // Database connection
    public string DatabaseConnectionString { get; set; }

    // Posting configuration
    public int DispenseTenderMediaId { get; set; }
    public decimal DefaultDispenseAmount { get; set; }

    // Optional settings
    public bool EnableLogging { get; set; }
}
```

**Configuration example (JSON - 10 lines):**
```json
{
  "DatabaseConnectionString": "Server=localhost;Database=Dispense;Trusted_Connection=true;",
  "DispenseTenderMediaId": 9999,
  "DefaultDispenseAmount": 1.00,
  "EnableLogging": true
}
```

**Usage in script:**
```csharp
public class DispensePosting : IScript
{
    private readonly IDatabaseClient _databaseClient;
    private readonly IOpsContextClient _opsContext;
    private readonly ILogManager _logger;
    private readonly Config _config;

    public DispensePosting(
        IDatabaseClient databaseClient,
        IOpsContextClient opsContext,
        ILogManager logger,
        IConfigurationClient configClient)
    {
        _databaseClient = databaseClient;
        _opsContext = opsContext;
        _logger = logger;
        _config = configClient.ReadConfig();
    }

    public void Execute(string functionName, string argument)
    {
        // Direct access to flat config properties
        var dispenses = _databaseClient.GetPendingDispenses(_config.DatabaseConnectionString);

        foreach (var dispense in dispenses)
        {
            _opsContext.PostCheckDetail(
                CheckDetailType.TenderMedia,
                _config.DispenseTenderMediaId,
                dispense.Amount,
                $"Dispense {dispense.DispenseId}"
            );
        }
    }
}
```

**Benefits:**
- Easy to understand (no nesting to navigate)
- Quick to modify (all properties at top level)
- Minimal lines (10-20 lines typical)
- No mapping complexity
- Self-documenting (property names are clear)

**When to use:**
- Simple integrations (1-3 operations)
- Single domain (one type of operation)
- Few configuration options (< 10 properties)
- No complex mappings needed
- Direct posting operations

**Anti-pattern to avoid:**
```csharp
// DON'T over-engineer for simple cases
public class Config
{
    public DatabaseConfig Database { get; set; }
    public PostingConfig Posting { get; set; }
    public LoggingConfig Logging { get; set; }
}

public class DatabaseConfig
{
    public string ConnectionString { get; set; }
}

public class PostingConfig
{
    public TenderMediaConfig TenderMedia { get; set; }
}

public class TenderMediaConfig
{
    public int DispenseTenderMediaId { get; set; }
}
// This is excessive for simple needs!
```

---

## 2. Direct Posting Operation Pattern

### Problem
Many integrations simply need to post transactions directly to the current check:
- Adding menu items
- Posting tender media
- Applying discounts
- Adding service charges

These operations should be:
- Simple and direct
- Minimal code
- Error handling only
- No complex transformations

### Solution: Direct IOpsContextClient Posting with Minimal Logic

**Direct posting script:**
```csharp
public class DispensePosting : IScript
{
    private readonly IDatabaseClient _databaseClient;
    private readonly IOpsContextClient _opsContext;
    private readonly ILogManager _logger;
    private readonly Config _config;

    public DispensePosting(
        IDatabaseClient databaseClient,
        IOpsContextClient opsContext,
        ILogManager logger,
        IConfigurationClient configClient)
    {
        _databaseClient = databaseClient;
        _opsContext = opsContext;
        _logger = logger;
        _config = configClient.ReadConfig();
    }

    public void Execute(string functionName, string argument)
    {
        try
        {
            // 1. Get data from external source
            var dispenses = _databaseClient.GetPendingDispenses();

            if (!dispenses.Any())
            {
                _logger.LogInfo("No pending dispenses found");
                return;
            }

            // 2. Post each dispense directly to check
            foreach (var dispense in dispenses)
            {
                _logger.LogInfo($"Posting dispense {dispense.DispenseId} for ${dispense.Amount:F2}");

                // Direct posting - no transformation needed
                _opsContext.PostCheckDetail(
                    CheckDetailType.TenderMedia,
                    _config.DispenseTenderMediaId,
                    dispense.Amount,
                    $"Dispense {dispense.DispenseId}"
                );

                // 3. Mark as posted
                _databaseClient.MarkDispenseAsPosted(dispense.DispenseId);

                _logger.LogInfo($"Dispense {dispense.DispenseId} posted successfully");
            }
        }
        catch (Exception e)
        {
            _logger.LogException("Error posting dispenses", e);
            _opsContext.ShowError("Unable to post dispenses");
            throw;
        }
    }
}
```

**IOpsContextClient posting methods:**
```csharp
public interface IOpsContextClient
{
    /// <summary>
    /// Post a detail line to the current check
    /// </summary>
    void PostCheckDetail(CheckDetailType type, int objectNumber, decimal amount, string comment);

    /// <summary>
    /// Get current check information
    /// </summary>
    CheckInfo GetCheckInfo();

    /// <summary>
    /// Show error message to user
    /// </summary>
    void ShowError(string message);

    /// <summary>
    /// Show information message to user
    /// </summary>
    void ShowMessage(string message);
}

public enum CheckDetailType
{
    MenuItem = 1,
    Discount = 2,
    ServiceCharge = 3,
    TenderMedia = 4
}

public class CheckInfo
{
    public long CheckNumber { get; set; }
    public decimal CheckTotal { get; set; }
    public decimal Tax { get; set; }
    public decimal SubTotal { get; set; }
}
```

**Posting examples:**

**Post menu item:**
```csharp
// Add a $5.99 beverage to the check
_opsContext.PostCheckDetail(
    CheckDetailType.MenuItem,
    menuItemNumber: 1001,
    amount: 5.99m,
    comment: "Large Soda"
);
```

**Post discount:**
```csharp
// Apply a $2.00 discount
_opsContext.PostCheckDetail(
    CheckDetailType.Discount,
    discountNumber: 100,
    amount: -2.00m,  // Negative for discount
    comment: "Loyalty Discount"
);
```

**Post service charge:**
```csharp
// Add 18% gratuity
var checkInfo = _opsContext.GetCheckInfo();
var gratuityAmount = checkInfo.SubTotal * 0.18m;

_opsContext.PostCheckDetail(
    CheckDetailType.ServiceCharge,
    serviceChargeNumber: 5,
    amount: gratuityAmount,
    comment: "18% Gratuity"
);
```

**Post tender media:**
```csharp
// Post gift card payment
_opsContext.PostCheckDetail(
    CheckDetailType.TenderMedia,
    tenderMediaNumber: 50,
    amount: 25.00m,
    comment: "Gift Card"
);
```

**Benefits:**
- Simple, direct API
- No transformation needed
- Immediate effect on check
- Built-in Simphony validation
- Transaction safety (Simphony manages)

**When to use:**
- Simple posting operations
- Direct integration with external systems
- Real-time posting (during check)
- No complex business rules
- Straightforward data flow

---

## 3. Single-Purpose Script Pattern

### Problem
Simple integrations often have:
- One primary operation
- No multi-function routing needed
- No complex switch statements
- Single responsibility

Traditional IScript interface with functionName parameter adds unnecessary complexity:
```csharp
public void Execute(string functionName, string argument)
{
    // functionName is always empty or unused
    // Why have this parameter?
    ProcessMessage();
}
```

### Solution: Single-Purpose Scripts with Object Argument

**Simplified IScript interface:**
```csharp
public interface IScript
{
    void Execute(object argument);
}
```

**AbstractScript for auto-dispatch:**
```csharp
public abstract class AbstractScript : IScript
{
    public void Execute(object argument)
    {
        // Find all public methods (excluding Object and AbstractScript methods)
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
```

**Example single-purpose script:**
```csharp
public class DispensePosting : AbstractScript
{
    private readonly IDatabaseClient _databaseClient;
    private readonly IOpsContextClient _opsContext;
    private readonly ILogManager _logger;
    private readonly Config _config;

    public DispensePosting(
        IDatabaseClient databaseClient,
        IOpsContextClient opsContext,
        ILogManager logger,
        IConfigurationClient configClient)
    {
        _databaseClient = databaseClient;
        _opsContext = opsContext;
        _logger = logger;
        _config = configClient.ReadConfig();
    }

    // This is the ONLY public method - automatically dispatched by AbstractScript
    public void ProcessMessage()
    {
        try
        {
            var dispenses = _databaseClient.GetPendingDispenses();

            foreach (var dispense in dispenses)
            {
                _opsContext.PostCheckDetail(
                    CheckDetailType.TenderMedia,
                    _config.DispenseTenderMediaId,
                    dispense.Amount,
                    $"Dispense {dispense.DispenseId}"
                );

                _databaseClient.MarkDispenseAsPosted(dispense.DispenseId);
            }
        }
        catch (Exception e)
        {
            _logger.LogException("Error processing dispenses", e);
            throw;
        }
    }

    // Private methods are allowed (not dispatched)
    private decimal CalculateAmount(Dispense dispense)
    {
        // Helper logic
        return dispense.Amount;
    }
}
```

**Another example with no parameters:**
```csharp
public class Version : AbstractScript
{
    private readonly IOpsContextClient _opsContext;

    public Version(IOpsContextClient opsContext)
    {
        _opsContext = opsContext;
    }

    // Single public method, no parameters
    public void ShowVersion()
    {
        var version = VersionHelper.NameAndVersion;
        var buildDate = VersionHelper.BuildDateTime;

        _opsContext.ShowMessage(
            $"{version}\n" +
            $"Build: {buildDate:yyyy-MM-dd HH:mm:ss}\n" +
            $"Workstation: {Environment.MachineName}"
        );
    }
}
```

**Benefits:**
- No boilerplate (no Execute implementation)
- Single responsibility enforced
- Clean, focused methods
- Automatic parameter matching
- Less code to maintain

**When to use:**
- Single-purpose scripts (one operation each)
- Simple integrations (< 5 scripts total)
- No multi-function routing needed
- Clear, focused operations

**Comparison with full IScript:**

| Aspect | Full IScript | Single-Purpose (Object Argument) |
|--------|--------------|--------------------------------|
| Lines of code | More (switch statements) | Less (auto-dispatch) |
| Functions per script | Multiple | One |
| Complexity | Higher | Lower |
| Boilerplate | More | Less |
| Best for | 15+ scripts, multi-function | 1-10 scripts, single-function |

---

## 4. Inline Event Registration Pattern

### Problem
Simple integrations with 1-5 events don't need:
- External configuration files
- Hybrid array registration
- Complex event management

Just need direct, simple registration in constructor.

### Solution: Direct Inline Event Registration

**Simple inline registration:**
```csharp
public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
{
    DependencyManager.RegisterByInstance(new OpsExtensibilityEnvironment(ExecutionContext.CurrentExecutionContext));
    DependencyManager.Install<SimphonyDependencies>();

    DependencyManager.Resolve<Status>().WorkstationId = OpsContext.WorkstationID;

    _logger = DependencyManager.Resolve<ILogManager>();
    _logger.LogInfo($"Instantiating {VersionHelper.NameAndVersion}");

    // Direct inline event registration
    var eventHelper = DependencyManager.Resolve<SimphonyEventHelper>();

    eventHelper.Register(this, "ProcessMessageEvent", nameof(DispensePosting));
    eventHelper.Register(this, "CustomEvent1", nameof(DispensePosting));
}
```

**With two events:**
```csharp
public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
{
    // ... initialization

    var eventHelper = DependencyManager.Resolve<SimphonyEventHelper>();

    // Event 1: Process dispenses when message arrives
    eventHelper.Register(this, "ProcessMessageEvent", nameof(DispensePosting));

    // Event 2: Void dispense on check void
    eventHelper.Register(this, "VoidCheckEvent", nameof(VoidDispense));
}
```

**Benefits:**
- Clear and direct (no indirection)
- Easy to understand (visible in constructor)
- No external files to manage
- Compile-time safety with nameof()
- Minimal overhead

**When to use:**
- 1-5 events total
- Simple event handling
- No runtime configuration needed
- Single-domain operations
- Direct event-to-script mapping

**Comparison with other approaches:**

| Approach | Event Count | Code Lines | Flexibility | Best For |
|----------|-------------|------------|-------------|----------|
| **Inline** | 1-5 | ~2 per event | Low (code change required) | Simple integrations |
| **Hybrid Array** | 5-15 | ~1 per event + setup | Medium (in code, compile-time safe) | Medium complexity |
| **Configuration** | 15+ | External file | High (runtime changeable) | Complex, multi-environment |

---

## 5. Simple Database Integration Pattern

### Problem
Simple integrations need basic database operations:
- Read pending records
- Update processed status
- Insert audit records
- No complex queries
- No ORM needed

### Solution: Simple Database Client with Basic Operations

**IDatabaseClient interface:**
```csharp
public interface IDatabaseClient
{
    // Read operations
    Dispense[] GetPendingDispenses();
    Dispense GetDispenseById(int dispenseId);

    // Update operations
    void MarkDispenseAsPosted(int dispenseId);

    // Audit operations
    void LogDispensePosting(int dispenseId, long checkNumber, DateTime postTime);
}
```

**Simple database client implementation:**
```csharp
public class SimpleDatabaseClient : IDatabaseClient
{
    private readonly Config _config;
    private readonly ILogManager _logger;

    public SimpleDatabaseClient(IConfigurationClient configClient, ILogManager logger)
    {
        _config = configClient.ReadConfig();
        _logger = logger;
    }

    public Dispense[] GetPendingDispenses()
    {
        using (var connection = new SqlConnection(_config.DatabaseConnectionString))
        {
            connection.Open();

            using (var command = new SqlCommand(
                "SELECT DispenseId, Amount, CreateTime FROM Dispenses WHERE Posted = 0 ORDER BY CreateTime",
                connection))
            {
                var dispenses = new List<Dispense>();

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dispenses.Add(new Dispense
                        {
                            DispenseId = reader.GetInt32(0),
                            Amount = reader.GetDecimal(1),
                            CreateTime = reader.GetDateTime(2)
                        });
                    }
                }

                _logger.LogInfo($"Found {dispenses.Count} pending dispenses");

                return dispenses.ToArray();
            }
        }
    }

    public void MarkDispenseAsPosted(int dispenseId)
    {
        using (var connection = new SqlConnection(_config.DatabaseConnectionString))
        {
            connection.Open();

            using (var command = new SqlCommand(
                "UPDATE Dispenses SET Posted = 1, PostTime = @PostTime WHERE DispenseId = @DispenseId",
                connection))
            {
                command.Parameters.AddWithValue("@PostTime", DateTime.Now);
                command.Parameters.AddWithValue("@DispenseId", dispenseId);

                command.ExecuteNonQuery();
            }
        }
    }

    public void LogDispensePosting(int dispenseId, long checkNumber, DateTime postTime)
    {
        using (var connection = new SqlConnection(_config.DatabaseConnectionString))
        {
            connection.Open();

            using (var command = new SqlCommand(
                "INSERT INTO DispenseAudit (DispenseId, CheckNumber, PostTime) VALUES (@DispenseId, @CheckNumber, @PostTime)",
                connection))
            {
                command.Parameters.AddWithValue("@DispenseId", dispenseId);
                command.Parameters.AddWithValue("@CheckNumber", checkNumber);
                command.Parameters.AddWithValue("@PostTime", postTime);

                command.ExecuteNonQuery();
            }
        }
    }
}
```

**Data model:**
```csharp
public class Dispense
{
    public int DispenseId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreateTime { get; set; }
    public bool Posted { get; set; }
    public DateTime? PostTime { get; set; }
}
```

**Benefits:**
- Simple, direct SQL
- No ORM complexity
- Easy to understand
- Parameterized queries (SQL injection safe)
- Connection management in methods

**When to use:**
- Simple CRUD operations
- Few database tables (1-3)
- Straightforward queries
- No complex joins
- Basic data access needs

---

## Summary

### When to Use These Patterns

**Use simple integration patterns when building:**
- Direct posting operations (tender media, menu items, discounts)
- Single-domain integrations
- External system polling (check for pending records, post to POS)
- Simple event-driven operations
- Basic database integrations

### Pattern Combination Recommendations

**Typical Simple Integration:**
1. Minimal configuration (flat, 10-20 lines)
2. Single-purpose scripts with AbstractScript
3. Direct posting via IOpsContextClient
4. Inline event registration (1-5 events)
5. Simple database client (basic queries)

### Integration with Universal Patterns

These domain patterns build on universal Simphony Extension Application patterns:
- Use **Interface-First Design** for IDatabaseClient
- Use **Custom DI Container** for dependency registration
- Use **Logging Framework** for operation tracking
- Use **Configuration Management** for minimal settings
- Use **Object Argument IScript** for single-purpose scripts

### Key Takeaways

1. **Keep it simple** - Don't over-engineer for simple needs
2. **Flat configuration suffices** - No nesting for 1-3 operations
3. **Direct posting is powerful** - IOpsContextClient provides simple API
4. **Single-purpose scripts are cleaner** - One operation per script with AbstractScript
5. **Inline events for few events** - 1-5 events don't need configuration
6. **Basic SQL is fine** - Simple database client without ORM

### Anti-Patterns to Avoid

**DON'T over-engineer:**
- ❌ Complex nested configuration for simple settings
- ❌ Multi-function scripts when single-purpose suffices
- ❌ Hybrid/configuration event registration for 2 events
- ❌ ORM for simple CRUD operations
- ❌ Transformation pipelines for direct posting
- ❌ Workflow coordinators for single-step operations

**DO keep it simple:**
- ✅ Flat configuration (10-20 lines)
- ✅ Single-purpose scripts
- ✅ Inline event registration
- ✅ Direct database client
- ✅ Direct posting operations
- ✅ Minimal abstraction

---

**Source Project:** SspLiquidDispenseSystem (44 files, 2 scripts, low complexity)
**Domain Focus:** Simple integration and direct posting operations
**Pattern Confidence:** High (validated in production environment)
**Key Principle:** Simplicity over sophistication for focused integrations
