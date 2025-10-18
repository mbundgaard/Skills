# Configuration Patterns for Simphony Extension Applications

## Overview

Configuration management is a **Universal Mandatory Pattern** in all Simphony Extension Applications. This guide explains the Interface + 3 Implementations pattern used for flexible, testable configuration management.

## Core Pattern: Interface + 3 Implementations

Every extension uses this exact pattern:

1. **IConfigurationClient** - Interface defining configuration contract
2. **SimphonyConfigurationClient** - Production implementation (reads from Simphony DataStore)
3. **FileConfigurationClient** - Fallback implementation (reads from local XML file)
4. **StubConfigurationClient** - Testing implementation (creates default config in code)

### Why Three Implementations?

- **SimphonyConfigurationClient**: Production environments with full Simphony DataStore access
- **FileConfigurationClient**: Non-CAPS workstations, offline scenarios, or quick fixes
- **StubConfigurationClient**: Unit testing, development without Simphony, rapid prototyping

## Configuration Entity Structure

### Basic Configuration Entity

```csharp
public class YourConfig
{
    // Always track when configuration was read
    public DateTime ReadTime { get; set; } = DateTime.Now;

    // Use default initializers to prevent null references
    public Event[] Events { get; set; } = new Event[0];
    public Timer[] Timers { get; set; } = new Timer[0];

    // Simple properties
    public string OutputFolder { get; set; }
    public int SomeNumber { get; set; }
    public bool EnableFeature { get; set; }
}
```

### Critical Guidelines

1. **Always use default initializers** on collections: `= new Event[0]`
   - Prevents `NullReferenceException` when config is not fully populated
   - Allows safe iteration without null checks

2. **Track ReadTime** for diagnostics
   - Helps troubleshoot stale configuration issues
   - Useful in logs to know when config was last loaded

3. **Use XmlAttribute for compact XML**
   ```csharp
   public class Timer
   {
       [XmlAttribute]
       public string Cron { get; set; }

       [XmlAttribute]
       public string Script { get; set; }
   }
   ```
   Produces: `<Timer Cron="0 * * * *" Script="MyScript" />`

4. **Nested classes for hierarchical configuration**
   ```csharp
   public class PaymentConfig
   {
       public Provider[] Providers { get; set; } = new Provider[0];
   }

   public class Provider
   {
       [XmlAttribute]
       public string Name { get; set; }

       public Credentials Credentials { get; set; }
   }
   ```

## Common Configuration Entities

### Event Entity (Event-to-Script Mapping)

Maps Simphony events to Script classes and functions:

```csharp
public class Event
{
    [XmlAttribute]
    public string Name { get; set; }        // "OpsReady", "CheckTotal", etc.

    [XmlAttribute]
    public string Script { get; set; }      // Script class name

    [XmlAttribute]
    public string Function { get; set; }    // Method to call
}
```

**XML Example:**
```xml
<Event Name="OpsReady" Script="InitializationScript" Function="Initialize" />
<Event Name="CheckTotal" Script="ValidationScript" Function="ValidateCheck" />
```

### Timer Entity (Scheduled Task Configuration)

Defines cron-based scheduled execution:

```csharp
public class Timer
{
    [XmlAttribute]
    public string Cron { get; set; }        // Cron expression

    [XmlAttribute]
    public string Script { get; set; }      // Script class name

    [XmlAttribute]
    public string Function { get; set; }    // Method to call

    [XmlAttribute]
    public string Arguments { get; set; }   // Optional arguments

    [XmlAttribute]
    public bool RandomMin { get; set; }     // Add random 0-59s delay
}
```

**XML Example:**
```xml
<Timer Cron="0 3 * * *" Script="ExportScript" Function="DailyExport" RandomMin="true" />
<Timer Cron="*/5 * * * *" Script="SyncScript" Function="SyncData" />
```

**Cron Examples:**
- `* * * * *` - Every minute
- `0 * * * *` - Every hour at minute 0
- `0 3 * * *` - Daily at 3:00 AM
- `*/5 * * * *` - Every 5 minutes

## Implementation Details

### SimphonyConfigurationClient (Production)

Reads configuration from Simphony Extension Application Content:

```csharp
public YourConfig ReadConfig()
{
    var extApp = _environment.DataStore.ReadExtensionApplicationByName(
        _environment.OpsContext.RvcID,
        _environment.ApplicationName
    );

    // Iterate hierarchical content (higher zones first)
    foreach (var extAppContent in extApp.ContentList.OrderByDescending(x => x.HierStrucID))
    {
        var unicodeData = _environment.DataStore
            .ReadExtensionApplicationContentByID(extAppContent.ExtensionApplicationContentID)
            .ContentData
            .DataBlob;

        if (unicodeData == null) continue;

        var stringData = Encoding.Unicode.GetString(unicodeData);

        if (stringData.StartsWith("<YourConfig>"))
        {
            var config = XmlSerializer.Deserialize<YourConfig>(stringData);

            // Write backup for debugging
            WriteBackupXml(config);

            return config;
        }
    }

    return new YourConfig(); // Never return null
}
```

**Key Features:**
- Hierarchical configuration support (Enterprise → Property → RVC)
- Automatic backup to disk for troubleshooting
- Thread-safe file operations
- Binary blob → Unicode → XML deserialization

### FileConfigurationClient (Fallback)

Reads from local XML file:

```csharp
public YourConfig ReadConfig()
{
    var filename = Path.Combine(_status.AppDir, $"{_status.AppName}.xml");

    if (!File.Exists(filename))
        throw new Exception($"Configuration file not found: {filename}");

    var data = File.ReadAllText(filename);
    return XmlSerializer.Deserialize<YourConfig>(data);
}
```

**Use Cases:**
- Non-CAPS workstations without DataStore access
- Quick configuration changes without Simphony configuration
- Emergency fallback when DataStore is unavailable

### StubConfigurationClient (Testing)

Creates default configuration in code:

```csharp
public YourConfig ReadConfig()
{
    var config = new YourConfig
    {
        OutputFolder = @"C:\Micros\Export",
        EnableFeature = true,

        Timers = new[]
        {
            new Timer
            {
                Cron = "0 * * * *",
                Script = "TestScript",
                Function = "Run"
            }
        }
    };

    // Write to disk for inspection
    var filename = Path.Combine(_status.AppDir, $"{_status.AppName}.xml");
    File.WriteAllText(filename, XmlSerializer.Serialize(config));

    return config;
}
```

**Use Cases:**
- Unit testing without Simphony
- Development environment setup
- Documentation generation
- CI/CD pipelines

## Dependency Injection Registration

Register configuration clients conditionally based on environment:

```csharp
public class Dependencies : IDependencyInstaller
{
    public void Install(IDependencyManager manager)
    {
        #if DEBUG
            // Use stub configuration in debug builds
            manager.Register<IConfigurationClient, StubConfigurationClient>();
        #else
            // Use Simphony configuration in release builds
            manager.Register<IConfigurationClient, SimphonyConfigurationClient>();

            // Register file-based as fallback (optional)
            manager.Register<IConfigurationClient, FileConfigurationClient>("file");
        #endif
    }
}
```

### Advanced: Fallback Strategy

Use Simphony with file-based fallback:

```csharp
public class ConfigurationManager
{
    private readonly IConfigurationClient _simphonyClient;
    private readonly IConfigurationClient _fileClient;

    public ConfigurationManager(
        [Dependency("simphony")] IConfigurationClient simphonyClient,
        [Dependency("file")] IConfigurationClient fileClient)
    {
        _simphonyClient = simphonyClient;
        _fileClient = fileClient;
    }

    public YourConfig ReadConfig()
    {
        try
        {
            return _simphonyClient.ReadConfig();
        }
        catch
        {
            // Fall back to file-based configuration
            return _fileClient.ReadConfig();
        }
    }
}
```

## Hierarchical/Zoned Configuration

Simphony supports hierarchical configuration at different levels:

1. **Enterprise** (HierStrucID highest) - Global settings
2. **Property** - Property-specific overrides
3. **RVC** (HierStrucID lowest) - Revenue center specific

**Pattern:**
```csharp
// OrderByDescending ensures higher zones checked first
foreach (var content in extApp.ContentList.OrderByDescending(x => x.HierStrucID))
{
    // Check for configuration in this zone
    if (foundConfiguration)
        return config; // Use first match (most specific)
}
```

## Configuration XML Examples

### Simple Configuration

```xml
<YourConfig>
  <ReadTime>2025-01-15T10:30:00</ReadTime>
  <OutputFolder>C:\Micros\Export</OutputFolder>
  <EnableFeature>true</EnableFeature>
</YourConfig>
```

### Complex Configuration with Events and Timers

```xml
<YourConfig>
  <ReadTime>2025-01-15T10:30:00</ReadTime>

  <Events>
    <Event Name="OpsReady" Script="InitScript" Function="Initialize" />
    <Event Name="CheckTotal" Script="ValidationScript" Function="Validate" />
  </Events>

  <Timers>
    <Timer Cron="0 3 * * *" Script="ExportScript" Function="DailyExport" RandomMin="true" />
    <Timer Cron="*/15 * * * *" Script="SyncScript" Function="Sync" />
  </Timers>

  <OutputFolder>C:\Micros\Export</OutputFolder>
  <EndOfDay>03:00:00</EndOfDay>
</YourConfig>
```

### Domain-Specific Configuration Examples

#### Payment Extension
```xml
<PaymentConfig>
  <Providers>
    <Provider Name="Stripe" Endpoint="https://api.stripe.com" TenderMediaNumber="50">
      <ApiKey>sk_live_xxx</ApiKey>
    </Provider>
    <Provider Name="PayPal" Endpoint="https://api.paypal.com" TenderMediaNumber="51">
      <ApiKey>xxx</ApiKey>
    </Provider>
  </Providers>
</PaymentConfig>
```

#### Export Extension
```xml
<ExportConfig>
  <OutputFolder>C:\Exports</OutputFolder>
  <EndOfDay>03:00:00</EndOfDay>

  <Mappings>
    <Mapping MenuItemNumber="1001" ExternalCode="BURGER_001" Category="Food" />
    <Mapping MenuItemNumber="2001" ExternalCode="DRINK_001" Category="Beverage" />
  </Mappings>
</ExportConfig>
```

#### Loyalty Extension
```xml
<LoyaltyConfig>
  <ApiEndpoint>https://loyalty.example.com/api</ApiEndpoint>
  <ApiKey>xxx</ApiKey>
  <TenderMediaNumber>60</TenderMediaNumber>
  <PointsPerDollar>10</PointsPerDollar>

  <TierThresholds>
    <Tier Name="Bronze" MinPoints="0" Discount="0.05" />
    <Tier Name="Silver" MinPoints="1000" Discount="0.10" />
    <Tier Name="Gold" MinPoints="5000" Discount="0.15" />
  </TierThresholds>
</LoyaltyConfig>
```

## Best Practices

### 1. Never Return Null
```csharp
// ❌ BAD
public YourConfig ReadConfig()
{
    return null; // Can cause NullReferenceException
}

// ✅ GOOD
public YourConfig ReadConfig()
{
    return new YourConfig(); // Return empty config
}
```

### 2. Always Use Default Initializers
```csharp
// ❌ BAD
public Event[] Events { get; set; }

// ✅ GOOD
public Event[] Events { get; set; } = new Event[0];
```

### 3. Validate Configuration After Load
```csharp
public YourConfig ReadConfig()
{
    var config = /* read from source */;

    // Validate critical settings
    if (string.IsNullOrEmpty(config.OutputFolder))
        throw new Exception("OutputFolder is required");

    if (config.Timers.Any(t => string.IsNullOrEmpty(t.Cron)))
        throw new Exception("All timers must have Cron expression");

    return config;
}
```

### 4. Log Configuration Details
```csharp
public YourConfig ReadConfig()
{
    var config = /* read from source */;

    _logger.LogInfo($"Configuration loaded: ReadTime={config.ReadTime}");
    _logger.LogInfo($"Events configured: {config.Events.Length}");
    _logger.LogInfo($"Timers configured: {config.Timers.Length}");

    return config;
}
```

### 5. Handle Missing Configuration Gracefully
```csharp
public YourConfig ReadConfig()
{
    try
    {
        return /* read from Simphony */;
    }
    catch (Exception ex)
    {
        _logger.LogWarning($"Configuration not found in Simphony: {ex.Message}");
        _logger.LogInfo("Using default configuration");
        return new YourConfig(); // Safe default
    }
}
```

## Anti-Patterns to Avoid

### ❌ DON'T: Hard-code configuration
```csharp
// BAD - inflexible, requires recompilation
public class ExportScript
{
    private const string OutputFolder = @"C:\Export";
}
```

### ✅ DO: Use configuration
```csharp
// GOOD - configurable without recompilation
public class ExportScript
{
    private readonly YourConfig _config;

    public ExportScript(IConfigurationClient configClient)
    {
        _config = configClient.ReadConfig();
    }
}
```

### ❌ DON'T: Forget null checks on collections
```csharp
// BAD - can throw NullReferenceException
foreach (var timer in config.Timers)
{
    // ...
}
```

### ✅ DO: Use default initializers
```csharp
// GOOD - safe iteration
public Timer[] Timers { get; set; } = new Timer[0];

foreach (var timer in config.Timers) // Always safe
{
    // ...
}
```

### ❌ DON'T: Mix configuration with logic
```csharp
// BAD - hard to test, tightly coupled
public class PaymentProcessor
{
    public void Process()
    {
        var config = new SimphonyConfigurationClient().ReadConfig();
        // ...
    }
}
```

### ✅ DO: Inject configuration client
```csharp
// GOOD - testable, loosely coupled
public class PaymentProcessor
{
    private readonly YourConfig _config;

    public PaymentProcessor(IConfigurationClient configClient)
    {
        _config = configClient.ReadConfig();
    }
}
```

## Testing Strategies

### Unit Testing with Stub

```csharp
[Test]
public void TestExportWithStubConfiguration()
{
    // Arrange
    var stubConfig = new StubConfigurationClient(appStatus);
    var config = stubConfig.ReadConfig();

    // Assert configuration is valid
    Assert.IsNotNull(config);
    Assert.IsNotEmpty(config.OutputFolder);

    // Use in test
    var exporter = new Exporter(stubConfig);
    exporter.Export();
}
```

### Integration Testing with File

```csharp
[Test]
public void TestWithCustomConfiguration()
{
    // Arrange - create test configuration
    var testConfig = new YourConfig
    {
        OutputFolder = @"C:\TestOutput",
        Timers = new[] { /* test timers */ }
    };

    var filename = Path.Combine(testDir, "test.xml");
    File.WriteAllText(filename, XmlSerializer.Serialize(testConfig));

    var fileConfig = new FileConfigurationClient(testAppStatus);

    // Act & Assert
    var loadedConfig = fileConfig.ReadConfig();
    Assert.AreEqual(testConfig.OutputFolder, loadedConfig.OutputFolder);
}
```

## Summary

The Configuration Pattern provides:
- ✅ Flexible deployment options (Simphony/File/Stub)
- ✅ Testability without Simphony dependencies
- ✅ XML-based human-readable configuration
- ✅ Hierarchical/zoned configuration support
- ✅ Type-safe configuration entities
- ✅ Automatic backup for troubleshooting

Always use this pattern for extension configuration - it's battle-tested across all 6 production projects analyzed.
