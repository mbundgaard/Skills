# Export and Integration Domain Patterns

Reference guide for implementing check export, data transformation, and external system integration in Simphony Extension Applications.

**Source:** Production export integration project analysis (medium-high complexity)

---

## 1. Plugin System Pattern

### Problem
Export systems need to support multiple destination formats and protocols:
- Different export processors for different systems (external APIs, generic JSON, XML)
- Ability to add new export types without modifying core code
- Configuration-driven processor selection
- Multiple processors active simultaneously
- Processor-specific settings and transformations

### Solution: Strategy Pattern with Named Registration and ResolveAll

**IExportProcessor interface:**
```csharp
public interface IExportProcessor
{
    /// <summary>
    /// Processor name for identification and configuration
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Process and export check data
    /// </summary>
    /// <param name="checkData">Transformed check data</param>
    /// <returns>Result indicating success/failure</returns>
    ExportResult ProcessExport(CheckData checkData);
}
```

**Multiple export processor implementations:**

```csharp
public class ExternalApiExportProcessor : IExportProcessor
{
    private readonly ILogManager _logger;
    private readonly HttpClient _httpClient;
    private readonly Config _config;

    public ExternalApiExportProcessor(ILogManager logger, IConfigurationClient configClient)
    {
        _logger = logger;
        _config = configClient.ReadConfig();

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_config.ExternalApiUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public string Name => "ExternalApi";

    public ExportResult ProcessExport(CheckData checkData)
    {
        try
        {
            _logger.LogInfo($"Exporting check {checkData.CheckNumber} to external API");

            // Transform to API-specific format
            var apiData = TransformToApiFormat(checkData);

            // Serialize to JSON
            var json = JsonSerializer.Serialize(apiData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // POST to external API
            var response = _httpClient.PostAsync("/api/checks", content).Result;

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = response.Content.ReadAsStringAsync().Result;
                throw new Exception($"External API error: {response.StatusCode} - {errorBody}");
            }

            _logger.LogInfo($"Successfully exported check {checkData.CheckNumber} to external API");

            return new ExportResult { Success = true };
        }
        catch (Exception e)
        {
            _logger.LogException($"Error exporting to external API", e);
            return new ExportResult
            {
                Success = false,
                ErrorMessage = e.Message
            };
        }
    }

    private ExternalApiCheckData TransformToApiFormat(CheckData checkData)
    {
        // API-specific transformation logic
        return new ExternalApiCheckData
        {
            CheckId = checkData.CheckNumber.ToString(),
            Timestamp = checkData.CloseTime,
            Items = checkData.MenuItems.Select(item => new ExternalApiItem
            {
                ItemCode = item.MenuItemNumber.ToString(),
                Quantity = item.Quantity,
                Price = item.Price
            }).ToArray(),
            // ... more transformations
        };
    }
}

public class GenericJsonExportProcessor : IExportProcessor
{
    private readonly ILogManager _logger;
    private readonly Config _config;

    public string Name => "GenericJson";

    public ExportResult ProcessExport(CheckData checkData)
    {
        try
        {
            _logger.LogInfo($"Exporting check {checkData.CheckNumber} to generic JSON file");

            var exportPath = Path.Combine(_config.ExportDirectory, $"check_{checkData.CheckNumber}_{DateTime.Now:yyyyMMddHHmmss}.json");
            var json = JsonSerializer.Serialize(checkData);

            File.WriteAllText(exportPath, json);

            _logger.LogInfo($"Exported check {checkData.CheckNumber} to {exportPath}");

            return new ExportResult { Success = true };
        }
        catch (Exception e)
        {
            _logger.LogException($"Error exporting to JSON file", e);
            return new ExportResult { Success = false, ErrorMessage = e.Message };
        }
    }
}

public class XmlExportProcessor : IExportProcessor
{
    private readonly ILogManager _logger;
    private readonly ISerializer _xmlSerializer;
    private readonly Config _config;

    public XmlExportProcessor(ILogManager logger, IConfigurationClient configClient)
    {
        _logger = logger;
        _config = configClient.ReadConfig();
        _xmlSerializer = DependencyManager.Resolve<ISerializer>("xml");
    }

    public string Name => "Xml";

    public ExportResult ProcessExport(CheckData checkData)
    {
        try
        {
            var exportPath = Path.Combine(_config.ExportDirectory, $"check_{checkData.CheckNumber}.xml");
            var xml = _xmlSerializer.Serialize(checkData);

            File.WriteAllText(exportPath, xml);

            return new ExportResult { Success = true };
        }
        catch (Exception e)
        {
            _logger.LogException($"Error exporting to XML", e);
            return new ExportResult { Success = false, ErrorMessage = e.Message };
        }
    }
}
```

**Dependency registration:**
```csharp
// In SimphonyDependencies.Install():

// Register all export processors with named registration
DependencyManager.RegisterByType<IExportProcessor, ExternalApiExportProcessor>(nameof(ExternalApiExportProcessor));
DependencyManager.RegisterByType<IExportProcessor, GenericJsonExportProcessor>(nameof(GenericJsonExportProcessor));
DependencyManager.RegisterByType<IExportProcessor, XmlExportProcessor>(nameof(XmlExportProcessor));
```

**Using all registered processors:**
```csharp
public class ExportCheck : IScript
{
    private readonly ILogManager _logger;

    public void Execute(string functionName, string argument)
    {
        try
        {
            // Resolve ALL registered export processors
            var processors = DependencyManager.ResolveAll<IExportProcessor>().ToList();

            _logger.LogInfo($"Found {processors.Count} export processors");

            // Get check data (from argument or OpsContext)
            var checkData = GetCheckData();

            // Execute all processors
            foreach (var processor in processors)
            {
                _logger.LogInfo($"Running export processor: {processor.Name}");

                var result = processor.ProcessExport(checkData);

                if (!result.Success)
                {
                    _logger.LogError($"Processor {processor.Name} failed: {result.ErrorMessage}");
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogException("Error in export check script", e);
            throw;
        }
    }

    private CheckData GetCheckData()
    {
        // Retrieve and transform check data from Simphony
        // (Implementation details in Data Transformation Pattern below)
        return new CheckData();
    }
}
```

**Configuration-driven processor selection:**
```csharp
public class Config
{
    public ExportProcessorConfig[] ExportProcessors { get; set; }
}

public class ExportProcessorConfig
{
    public string Name { get; set; }
    public bool Enabled { get; set; }
    public Dictionary<string, string> Settings { get; set; }
}
```

**Selective execution based on configuration:**
```csharp
public void Execute(string functionName, string argument)
{
    var allProcessors = DependencyManager.ResolveAll<IExportProcessor>().ToList();
    var config = DependencyManager.Resolve<IConfigurationClient>().ReadConfig();

    // Filter processors based on configuration
    var enabledProcessorConfigs = config.ExportProcessors
        .Where(x => x.Enabled)
        .ToList();

    foreach (var processorConfig in enabledProcessorConfigs)
    {
        var processor = allProcessors.FirstOrDefault(x => x.Name == processorConfig.Name);

        if (processor == null)
        {
            _logger.LogWarn($"Configured processor '{processorConfig.Name}' not found");
            continue;
        }

        _logger.LogInfo($"Running enabled processor: {processor.Name}");
        processor.ProcessExport(checkData);
    }
}
```

**Benefits:**
- Add new processors without modifying existing code
- Enable/disable processors via configuration
- Run multiple exports simultaneously
- Processor-specific settings in configuration
- Easy testing of individual processors

**When to use:**
- Multiple export destinations
- Different data formats (JSON, XML, CSV)
- Various protocols (HTTP, FTP, file system)
- Extensible export requirements
- Configuration-driven export control

---

## 2. Data Transformation Pipeline Pattern

### Problem
Exporting check data requires complex transformations:
- Simphony data → standardized internal format → export format
- Menu item mapping (Simphony item ID → external system code)
- Discount transformations
- Tender media mapping
- Price calculations and adjustments
- Custom field mapping per export destination

### Solution: Multi-Stage Transformation Pipeline with Mappers

**CheckData standardized model:**
```csharp
public class CheckData
{
    public long CheckNumber { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime CloseTime { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }

    public Employee Server { get; set; }
    public Table Table { get; set; }

    public List<MenuItemData> MenuItems { get; set; }
    public List<DiscountData> Discounts { get; set; }
    public List<TenderData> Tenders { get; set; }
    public List<ServiceChargeData> ServiceCharges { get; set; }
}

public class MenuItemData
{
    public int MenuItemNumber { get; set; }
    public string MenuItemName { get; set; }
    public string ExternalCode { get; set; }  // Mapped code for external system
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Total { get; set; }
}
```

**Stage 1: Simphony data extraction:**
```csharp
public class CheckDataExtractor
{
    private readonly IOpsContextClient _opsContext;
    private readonly IDatabaseClient _databaseClient;

    public CheckDataExtractor(IOpsContextClient opsContext, IDatabaseClient databaseClient)
    {
        _opsContext = opsContext;
        _databaseClient = databaseClient;
    }

    public CheckData ExtractCheckData(long checkNumber)
    {
        // Get check from Simphony database
        var simphonyCheck = _databaseClient.GetCheck(checkNumber);

        // Extract basic check information
        var checkData = new CheckData
        {
            CheckNumber = simphonyCheck.CheckNumber,
            OpenTime = simphonyCheck.OpenTime,
            CloseTime = simphonyCheck.CloseTime,
            SubTotal = simphonyCheck.SubTotal,
            Tax = simphonyCheck.Tax,
            Total = simphonyCheck.Total,

            Server = new Employee
            {
                EmployeeId = simphonyCheck.EmployeeObjectNum,
                Name = _databaseClient.GetEmployee(simphonyCheck.EmployeeObjectNum)?.Name
            },

            Table = new Table
            {
                TableNumber = simphonyCheck.TableObjectNum,
                Name = _databaseClient.GetTable(simphonyCheck.TableObjectNum)?.Name
            }
        };

        // Extract menu items
        checkData.MenuItems = simphonyCheck.DetailLines
            .Where(x => x.DetailType == DetailType.MenuItem)
            .Select(x => new MenuItemData
            {
                MenuItemNumber = x.ObjectNumber,
                MenuItemName = x.Name,
                Quantity = x.Quantity,
                Price = x.Price,
                Total = x.Total
            })
            .ToList();

        // Extract discounts
        checkData.Discounts = simphonyCheck.DetailLines
            .Where(x => x.DetailType == DetailType.Discount)
            .Select(x => new DiscountData
            {
                DiscountNumber = x.ObjectNumber,
                DiscountName = x.Name,
                Amount = x.Total
            })
            .ToList();

        // Extract tenders
        checkData.Tenders = simphonyCheck.Tenders
            .Select(x => new TenderData
            {
                TenderMediaNumber = x.TenderMediaObjectNum,
                TenderMediaName = _databaseClient.GetTenderMedia(x.TenderMediaObjectNum)?.Name,
                Amount = x.Amount
            })
            .ToList();

        return checkData;
    }
}
```

**Stage 2: Menu item mapping:**
```csharp
public class MenuItemMapper
{
    private readonly Config _config;
    private readonly ILogManager _logger;

    public MenuItemMapper(IConfigurationClient configClient, ILogManager logger)
    {
        _config = configClient.ReadConfig();
        _logger = logger;
    }

    public void MapMenuItems(CheckData checkData)
    {
        foreach (var menuItem in checkData.MenuItems)
        {
            var mapping = _config.MenuItemMappings
                ?.FirstOrDefault(x => x.SimphonyMenuItemNumber == menuItem.MenuItemNumber);

            if (mapping != null)
            {
                menuItem.ExternalCode = mapping.ExternalCode;
                menuItem.MenuItemName = mapping.ExternalName ?? menuItem.MenuItemName;

                _logger.LogDebug($"Mapped menu item {menuItem.MenuItemNumber} to {menuItem.ExternalCode}");
            }
            else
            {
                // Default mapping if not configured
                menuItem.ExternalCode = menuItem.MenuItemNumber.ToString();

                _logger.LogWarn($"No mapping found for menu item {menuItem.MenuItemNumber}, using default");
            }
        }
    }
}
```

**Configuration for mapping:**
```csharp
public class Config
{
    public MenuItemMapping[] MenuItemMappings { get; set; }
    public DiscountMapping[] DiscountMappings { get; set; }
    public TenderMapping[] TenderMappings { get; set; }
}

public class MenuItemMapping
{
    public int SimphonyMenuItemNumber { get; set; }
    public string ExternalCode { get; set; }
    public string ExternalName { get; set; }
    public string Category { get; set; }
}

public class DiscountMapping
{
    public int SimphonyDiscountNumber { get; set; }
    public string ExternalCode { get; set; }
    public string ExternalName { get; set; }
}
```

**Stage 3: Data transformation and enrichment:**
```csharp
public class CheckDataTransformer
{
    private readonly MenuItemMapper _menuItemMapper;
    private readonly DiscountMapper _discountMapper;
    private readonly TenderMapper _tenderMapper;

    public CheckDataTransformer(
        MenuItemMapper menuItemMapper,
        DiscountMapper discountMapper,
        TenderMapper tenderMapper)
    {
        _menuItemMapper = menuItemMapper;
        _discountMapper = discountMapper;
        _tenderMapper = tenderMapper;
    }

    public void Transform(CheckData checkData)
    {
        // Apply menu item mappings
        _menuItemMapper.MapMenuItems(checkData);

        // Apply discount mappings
        _discountMapper.MapDiscounts(checkData);

        // Apply tender mappings
        _tenderMapper.MapTenders(checkData);

        // Apply business rules
        ApplyBusinessRules(checkData);
    }

    private void ApplyBusinessRules(CheckData checkData)
    {
        // Example: Exclude voided items
        checkData.MenuItems = checkData.MenuItems
            .Where(x => x.Quantity > 0)
            .ToList();

        // Example: Combine duplicate items
        checkData.MenuItems = checkData.MenuItems
            .GroupBy(x => new { x.MenuItemNumber, x.Price })
            .Select(g => new MenuItemData
            {
                MenuItemNumber = g.Key.MenuItemNumber,
                MenuItemName = g.First().MenuItemName,
                ExternalCode = g.First().ExternalCode,
                Quantity = g.Sum(x => x.Quantity),
                Price = g.Key.Price,
                Total = g.Sum(x => x.Total)
            })
            .ToList();

        // More business rules...
    }
}
```

**Complete export script with pipeline:**
```csharp
public class ExportCheck : IScript
{
    private readonly CheckDataExtractor _extractor;
    private readonly CheckDataTransformer _transformer;
    private readonly ILogManager _logger;

    public ExportCheck(
        CheckDataExtractor extractor,
        CheckDataTransformer transformer,
        ILogManager logger)
    {
        _extractor = extractor;
        _transformer = transformer;
        _logger = logger;
    }

    public void Execute(string functionName, string argument)
    {
        try
        {
            long checkNumber = long.Parse(argument);

            _logger.LogInfo($"Starting export pipeline for check {checkNumber}");

            // Stage 1: Extract from Simphony
            var checkData = _extractor.ExtractCheckData(checkNumber);

            // Stage 2: Transform and map
            _transformer.Transform(checkData);

            // Stage 3: Export via all processors
            var processors = DependencyManager.ResolveAll<IExportProcessor>().ToList();

            foreach (var processor in processors)
            {
                processor.ProcessExport(checkData);
            }

            _logger.LogInfo($"Export pipeline completed for check {checkNumber}");
        }
        catch (Exception e)
        {
            _logger.LogException("Error in export pipeline", e);
            throw;
        }
    }
}
```

**Benefits:**
- Clean separation of extraction, transformation, export
- Reusable transformation components
- Configuration-driven mappings
- Business rule enforcement
- Easy testing of each stage

**When to use:**
- Complex data transformations
- Multiple export formats from same source
- Menu item/discount/tender mapping needs
- Business rule application during export
- Multi-stage data processing

---

## 3. Hybrid Event Registration Pattern

### Problem
Medium-sized projects (5-15 events) need event registration that is:
- More maintainable than inline registration (avoids repetitive code)
- Simpler than configuration-driven (no external file needed)
- Easy to modify (all events in one place in code)
- Compile-time safe (uses `nameof()` to prevent typos)

### Solution: In-Code Event Array with Loop Registration

**Event array registration:**
```csharp
public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
{
    // ... initialization

    // Define all events in an array
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

    // Register all events in loop
    foreach (var evt in staticEvents)
    {
        eventHelper.Register(this, evt.Name, evt.Script);
    }
}
```

**Event class:**
```csharp
internal class Event
{
    public string Name { get; set; }
    public string Script { get; set; }
    public string Function { get; set; }  // Optional
}
```

**SimphonyEventHelper.Register() method:**
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
        _logger.LogInfo($"Registering event '{eventName}' to script '{scriptName}'");

        switch (eventName)
        {
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

            case "CustomEvent1":
                app.CustomEvent1 += (sender, args) =>
                    HandleEvent(scriptName, functionName, args);
                break;

            // ... more events

            default:
                throw new Exception($"Unknown event: {eventName}");
        }
    }

    private void HandleEvent(string scriptName, string functionName, EventArgs args)
    {
        try
        {
            var script = DependencyManager.Resolve<IScript>(scriptName);

            // For IScript with Execute(functionName, argument)
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
        // Serialize event arguments to string if needed
        // Or return empty string if not used
        return string.Empty;
    }
}
```

**With function routing:**
```csharp
var staticEvents = new[]
{
    new Event { Name = "BeginCheckPreviewEvent", Script = nameof(CheckProcessor), Function = "BeginCheck" },
    new Event { Name = "ProcessCheckEvent", Script = nameof(CheckProcessor), Function = "ProcessCheck" },
    new Event { Name = "CloseCheckEvent", Script = nameof(CheckProcessor), Function = "CloseCheck" },
};

// Script implementation with function routing
public class CheckProcessor : IScript
{
    public void Execute(string functionName, string argument)
    {
        switch (functionName)
        {
            case "BeginCheck":
                BeginCheck();
                break;
            case "ProcessCheck":
                ProcessCheck();
                break;
            case "CloseCheck":
                CloseCheck();
                break;
            default:
                throw new Exception($"Unknown function: {functionName}");
        }
    }

    private void BeginCheck() { /* ... */ }
    private void ProcessCheck() { /* ... */ }
    private void CloseCheck() { /* ... */ }
}
```

**Benefits:**
- More maintainable than inline (all events in one array)
- Simpler than configuration-driven (no external file)
- Compile-time safety (`nameof()` prevents typos)
- Easy to add/remove events
- Good for 5-15 events (sweet spot)

**When to use:**
- 5-15 events (not too many, not too few)
- No need for runtime configuration changes
- Want compile-time safety
- Medium complexity projects

**Comparison with other approaches:**

| Approach | Event Count | Pros | Cons |
|----------|-------------|------|------|
| **Inline** | 1-5 | Simple, direct | Repetitive for many events |
| **Hybrid Array** | 5-15 | Maintainable, compile-time safe | Still in code (not runtime configurable) |
| **Configuration-Driven** | 15+ | Runtime configurable, scalable | External file, no compile-time safety |

---

## 4. Status Client Abstraction Pattern

### Problem
Export operations need to track and update status information:
- Export job status (pending, processing, complete, failed)
- Progress tracking for long-running exports
- Error tracking and retry counts
- Status persistence across application restarts
- Status queries from other workstations

The status tracking should be:
- Abstracted behind an interface
- Support different storage backends (database, file, network)
- Thread-safe for concurrent operations

### Solution: IStatusClient Interface with Multiple Implementations

**IStatusClient interface:**
```csharp
public interface IStatusClient
{
    // Status operations
    ExportStatus GetStatus(string exportId);
    void UpdateStatus(string exportId, ExportStatus status);
    ExportStatus[] GetPendingExports();
    ExportStatus[] GetFailedExports();

    // Cleanup
    void CleanupCompletedExports(TimeSpan olderThan);
}

public class ExportStatus
{
    public string ExportId { get; set; }
    public long CheckNumber { get; set; }
    public ExportState State { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? StartedTime { get; set; }
    public DateTime? CompletedTime { get; set; }
    public string ErrorMessage { get; set; }
    public int RetryCount { get; set; }
}

public enum ExportState
{
    Pending,
    Processing,
    Completed,
    Failed
}
```

**Database implementation:**
```csharp
public class DatabaseStatusClient : IStatusClient
{
    private readonly IDatabaseClient _databaseClient;
    private readonly ILogManager _logger;

    public DatabaseStatusClient(IDatabaseClient databaseClient, ILogManager logger)
    {
        _databaseClient = databaseClient;
        _logger = logger;
    }

    public ExportStatus GetStatus(string exportId)
    {
        return _databaseClient.Query<ExportStatus>(
            "SELECT * FROM ExportStatus WHERE ExportId = @ExportId",
            new { ExportId = exportId }
        ).FirstOrDefault();
    }

    public void UpdateStatus(string exportId, ExportStatus status)
    {
        _logger.LogInfo($"Updating export status: {exportId} -> {status.State}");

        var existing = GetStatus(exportId);

        if (existing == null)
        {
            _databaseClient.Execute(
                @"INSERT INTO ExportStatus (ExportId, CheckNumber, State, CreatedTime, ErrorMessage, RetryCount)
                  VALUES (@ExportId, @CheckNumber, @State, @CreatedTime, @ErrorMessage, @RetryCount)",
                status
            );
        }
        else
        {
            _databaseClient.Execute(
                @"UPDATE ExportStatus
                  SET State = @State,
                      StartedTime = @StartedTime,
                      CompletedTime = @CompletedTime,
                      ErrorMessage = @ErrorMessage,
                      RetryCount = @RetryCount
                  WHERE ExportId = @ExportId",
                status
            );
        }
    }

    public ExportStatus[] GetPendingExports()
    {
        return _databaseClient.Query<ExportStatus>(
            "SELECT * FROM ExportStatus WHERE State = @State ORDER BY CreatedTime",
            new { State = ExportState.Pending }
        ).ToArray();
    }

    public ExportStatus[] GetFailedExports()
    {
        return _databaseClient.Query<ExportStatus>(
            "SELECT * FROM ExportStatus WHERE State = @State AND RetryCount < 3 ORDER BY CreatedTime",
            new { State = ExportState.Failed }
        ).ToArray();
    }

    public void CleanupCompletedExports(TimeSpan olderThan)
    {
        var cutoffDate = DateTime.Now.Subtract(olderThan);

        _databaseClient.Execute(
            "DELETE FROM ExportStatus WHERE State = @State AND CompletedTime < @CutoffDate",
            new { State = ExportState.Completed, CutoffDate = cutoffDate }
        );
    }
}
```

**File-based implementation (for simple scenarios):**
```csharp
public class FileStatusClient : IStatusClient
{
    private readonly string _statusDirectory;
    private readonly ISerializer _serializer;
    private readonly ILogManager _logger;
    private readonly object _lockObject = new object();

    public FileStatusClient(ILogManager logger)
    {
        _logger = logger;
        _serializer = DependencyManager.Resolve<ISerializer>("json");
        _statusDirectory = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "ExportStatus"
        );

        Directory.CreateDirectory(_statusDirectory);
    }

    public ExportStatus GetStatus(string exportId)
    {
        var filePath = GetStatusFilePath(exportId);

        if (!File.Exists(filePath))
            return null;

        lock (_lockObject)
        {
            var json = File.ReadAllText(filePath);
            return _serializer.Deserialize<ExportStatus>(json);
        }
    }

    public void UpdateStatus(string exportId, ExportStatus status)
    {
        var filePath = GetStatusFilePath(exportId);

        lock (_lockObject)
        {
            var json = _serializer.Serialize(status);
            File.WriteAllText(filePath, json);
        }

        _logger.LogDebug($"Updated status file: {filePath}");
    }

    public ExportStatus[] GetPendingExports()
    {
        return GetStatusesByState(ExportState.Pending);
    }

    public ExportStatus[] GetFailedExports()
    {
        return GetStatusesByState(ExportState.Failed)
            .Where(x => x.RetryCount < 3)
            .ToArray();
    }

    public void CleanupCompletedExports(TimeSpan olderThan)
    {
        var cutoffDate = DateTime.Now.Subtract(olderThan);
        var statusFiles = Directory.GetFiles(_statusDirectory, "*.json");

        foreach (var file in statusFiles)
        {
            try
            {
                var status = _serializer.Deserialize<ExportStatus>(File.ReadAllText(file));

                if (status.State == ExportState.Completed && status.CompletedTime < cutoffDate)
                {
                    File.Delete(file);
                    _logger.LogDebug($"Deleted old status file: {file}");
                }
            }
            catch (Exception e)
            {
                _logger.LogException($"Error cleaning up status file: {file}", e);
            }
        }
    }

    private string GetStatusFilePath(string exportId)
    {
        return Path.Combine(_statusDirectory, $"{exportId}.json");
    }

    private ExportStatus[] GetStatusesByState(ExportState state)
    {
        var statusFiles = Directory.GetFiles(_statusDirectory, "*.json");
        var statuses = new List<ExportStatus>();

        foreach (var file in statusFiles)
        {
            try
            {
                var status = _serializer.Deserialize<ExportStatus>(File.ReadAllText(file));

                if (status.State == state)
                {
                    statuses.Add(status);
                }
            }
            catch (Exception e)
            {
                _logger.LogException($"Error reading status file: {file}", e);
            }
        }

        return statuses.OrderBy(x => x.CreatedTime).ToArray();
    }
}
```

**Using status client in export script:**
```csharp
public class ExportCheck : IScript
{
    private readonly IStatusClient _statusClient;
    private readonly CheckDataExtractor _extractor;
    private readonly ILogManager _logger;

    public void Execute(string functionName, string argument)
    {
        var checkNumber = long.Parse(argument);
        var exportId = $"export_{checkNumber}_{DateTime.Now:yyyyMMddHHmmss}";

        // Create pending status
        var status = new ExportStatus
        {
            ExportId = exportId,
            CheckNumber = checkNumber,
            State = ExportState.Pending,
            CreatedTime = DateTime.Now
        };

        _statusClient.UpdateStatus(exportId, status);

        try
        {
            // Update to processing
            status.State = ExportState.Processing;
            status.StartedTime = DateTime.Now;
            _statusClient.UpdateStatus(exportId, status);

            // Perform export
            var checkData = _extractor.ExtractCheckData(checkNumber);
            var processors = DependencyManager.ResolveAll<IExportProcessor>().ToList();

            foreach (var processor in processors)
            {
                processor.ProcessExport(checkData);
            }

            // Update to completed
            status.State = ExportState.Completed;
            status.CompletedTime = DateTime.Now;
            _statusClient.UpdateStatus(exportId, status);

            _logger.LogInfo($"Export {exportId} completed successfully");
        }
        catch (Exception e)
        {
            // Update to failed
            status.State = ExportState.Failed;
            status.ErrorMessage = ExceptionHelper.GetFirstException(e).Message;
            status.RetryCount++;
            status.CompletedTime = DateTime.Now;
            _statusClient.UpdateStatus(exportId, status);

            _logger.LogException($"Export {exportId} failed", e);
            throw;
        }
    }
}
```

**Background retry service:**
```csharp
public class ExportRetryService : IService
{
    private readonly IStatusClient _statusClient;
    private readonly ILogManager _logger;

    public string Name => nameof(ExportRetryService);

    public void Start()
    {
        _logger.LogInfo($"{Name} starting...");
    }

    public bool Execute()
    {
        try
        {
            // Get failed exports that haven't exceeded retry limit
            var failedExports = _statusClient.GetFailedExports();

            foreach (var failedExport in failedExports)
            {
                _logger.LogInfo($"Retrying failed export: {failedExport.ExportId} (attempt {failedExport.RetryCount + 1})");

                try
                {
                    // Retry export
                    var script = DependencyManager.Resolve<IScript>(nameof(ExportCheck));
                    script.Execute("", failedExport.CheckNumber.ToString());
                }
                catch (Exception e)
                {
                    _logger.LogException($"Retry failed for {failedExport.ExportId}", e);
                }
            }

            // Cleanup old completed exports (older than 7 days)
            _statusClient.CleanupCompletedExports(TimeSpan.FromDays(7));

            Thread.Sleep(60000);  // Wait 1 minute before next check
            return true;
        }
        catch (Exception e)
        {
            _logger.LogException($"{Name} error", e);
            Thread.Sleep(60000);
            return true;
        }
    }

    public void Stop()
    {
        _logger.LogInfo($"{Name} stopping...");
    }
}
```

**Benefits:**
- Status tracking across application lifecycle
- Support for multiple storage backends
- Retry logic for failed exports
- Cleanup of old status records
- Thread-safe operations

**When to use:**
- Long-running export operations
- Need for retry logic
- Status monitoring requirements
- Multi-step export processes
- Background export processing

---

## Summary

### When to Use These Patterns

**Use export/integration patterns when building:**
- Check export to external systems
- Data synchronization with third-party platforms
- Multi-format data export (JSON, XML, CSV)
- Integration with accounting, inventory, or reporting systems
- Data transformation and mapping requirements

### Pattern Combination Recommendations

**Basic Export Implementation:**
1. CheckDataExtractor for Simphony data extraction
2. Single export processor for target system
3. Simple event registration (CloseCheckEvent)

**Advanced Export Implementation:**
Add plugin system for multiple processors, transformation pipeline for complex mappings, status tracking for monitoring, and retry services for resilience.

### Integration with Universal Patterns

These domain patterns build on universal Simphony Extension Application patterns:
- Use **Interface-First Design** for IExportProcessor, IStatusClient abstractions
- Use **Custom DI Container** for processor registration with ResolveAll
- Use **Logging Framework** for export tracking and debugging
- Use **Configuration Management** for mappings and processor settings
- Use **Hybrid Event Registration** for 5-15 events

### Key Takeaways

1. **Plugin system enables extensibility** - Add new export formats without modifying core code
2. **Transformation pipeline separates concerns** - Extract, transform, export stages
3. **Hybrid event registration is the sweet spot** - 5-15 events in array with loop
4. **Status tracking enables monitoring** - Track export progress and enable retries
5. **Mapping configuration is essential** - External codes for menu items, discounts, tenders
6. **Error resilience is critical** - Retry logic, status tracking, error logging
