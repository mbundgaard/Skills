# Hosted Background Service Patterns

**Domain:** Autonomous background services, status boards, data export/bridging, external system integration

**Complexity:** Medium

**Source Project:** OrderStatusBoard2 (99 files, ~4,500 LOC)

---

## Overview

The **Hosted Background Service Pattern** represents a fundamentally different architectural approach from traditional POS integration. Instead of hooking into transaction events, this pattern uses Simphony as a runtime host for autonomous background services that bridge Simphony data with external systems.

### Traditional vs Hosted Pattern Comparison

| Aspect | Traditional POS Integration | Hosted Background Service |
|--------|----------------------------|---------------------------|
| **Entry Point** | POS transaction events | Lifecycle events (OpsReady/OpsExit) |
| **Execution Model** | Synchronous event handlers | Asynchronous background services |
| **Data Flow** | POS → Extension → POS | Simphony Data → Extension → External API |
| **User Interaction** | Blocks POS until complete | No POS interaction |
| **Data Source** | Event parameters, API calls | File monitoring, polling |
| **Purpose** | Enhance POS operations | Bridge data to external systems |
| **Lifecycle** | Per-transaction | Continuous while Simphony running |

### When to Use Hosted Background Service Pattern

**✅ USE when:**
- **One-way data flow**: Simphony → External system (no response needed)
- **Continuous processing**: Data posted continuously/periodically
- **File/database sources**: Consuming posting files, reading config databases
- **No real-time POS integration**: Status boards, dashboards, export systems
- **Autonomous operation**: No user interaction required

**❌ DON'T USE when:**
- **POS participation required**: Need to block/modify transactions
- **Real-time response needed**: Must respond within milliseconds
- **Bidirectional communication**: External system must respond to extension
- **User authorization**: Require immediate user input during transactions

**Example Use Cases:**
- Kitchen status display boards
- Order tracking dashboards
- Data export to reporting systems
- Third-party analytics integration
- Automated data synchronization

---

## Pattern 1: Lifecycle Event Integration

### Problem
Background services need to start when Simphony starts and stop when Simphony exits, without participating in transaction processing.

### Solution

**Complete Implementation:**

```csharp
// SimphonyExtensibilityApplication.cs
public class SimphonyExtensibilityApplication : OpsExtensibilityApplication
{
    private ILogManager _logger;

    public SimphonyExtensibilityApplication(IExecutionContext context) : base(context)
    {
        DependencyManager.RegisterByInstance(
            new OpsExtensibilityEnvironment(ExecutionContext.CurrentExecutionContext));
        DependencyManager.Install<Dependencies>();

        // Only run on designated workstation (avoid duplicate processing)
        if(!DependencyManager.Resolve<IOpsContextClient>().IsKdsControllerHost())
            return;

        _logger = DependencyManager.Resolve<ILogManager>();
        _logger.LogInfo($"Instantiating {VersionHelper.NameAndVersion}");

        try
        {
            // Start services when Simphony is ready
            OpsReadyEvent += (s, a) =>
            {
                _logger.LogInfo("Simphony ready - starting background services");
                DependencyManager.Resolve<OsbService>().Start(unattended: true);
                return EventProcessingInstruction.Continue;
            };

            // Stop services when Simphony is exiting
            OpsExitEvent += (s, a) =>
            {
                _logger.LogInfo("Simphony exiting - stopping background services");
                DependencyManager.Resolve<OsbService>().Stop(unattended: true);
                return EventProcessingInstruction.Continue;
            };
        }
        catch (Exception e)
        {
            _logger.LogException($"Error registering lifecycle callbacks", e);
        }
    }
}
```

**Service Controller Pattern:**

```csharp
// OsbService.cs - Main orchestrator for background services
public class OsbService : AbstractScript
{
    private readonly IOpsContextClient _opsContextClient;
    private static KdsSyncService _kdsSyncService;
    private static ConfigSyncService _configSyncService;

    public OsbService(IOpsContextClient opsContextClient)
    {
        _opsContextClient = opsContextClient;
    }

    public void Start(bool unattended)
    {
        GetKdsSyncService(unattended)?.Start();
        GetConfigSyncService(unattended)?.Start();

        if (!unattended)
            _opsContextClient.ShowMessage("OSB Service has been started");
    }

    public void Stop(bool unattended)
    {
        GetKdsSyncService(unattended)?.Stop();
        GetConfigSyncService(unattended)?.Stop();

        if (!unattended)
            _opsContextClient.ShowMessage("OSB Service has been stopped");
    }

    // Manual operations available via Simphony UI
    public void ForceConfigSync()
    {
        var configService = GetConfigSyncService(false);
        configService.ForceSyncAsync().GetAwaiter().GetResult();
        _opsContextClient.ShowMessage("OSB Config has been synced");
    }

    public void SetOsbOpen() => GetKdsSyncService(false)?.SetClosed(false);
    public void SetOsbClosed() => GetKdsSyncService(false)?.SetClosed(true);

    private KdsSyncService GetKdsSyncService(bool unattended)
    {
        if (_kdsSyncService != null) return _kdsSyncService;
        _kdsSyncService = DependencyManager.Resolve<KdsSyncService>();
        return _kdsSyncService;
    }

    private ConfigSyncService GetConfigSyncService(bool unattended)
    {
        if (_configSyncService != null) return _configSyncService;
        _configSyncService = DependencyManager.Resolve<ConfigSyncService>();
        return _configSyncService;
    }
}
```

### Configuration

```json
{
  "Events": [],  // No POS transaction events
  "Timers": []   // Background services use internal timers
}
```

### Benefits
- Clean separation from POS transaction flow
- Automatic lifecycle management (start/stop with Simphony)
- Workstation isolation (only runs on designated machine)
- Supports both attended (UI buttons) and unattended (lifecycle) invocation
- No impact on POS performance

### When to Use
- Any extension that runs continuous background operations
- Services that don't participate in transaction processing
- Data bridges between Simphony and external systems
- Status boards, dashboards, export systems

### Integration with Universal Patterns
- Uses standard ApplicationFactory pattern
- Follows DependencyManager pattern
- Extends OpsExtensibilityApplication (not EventApplicationService)
- No POS event registration required

---

## Pattern 2: File Monitoring with Position Tracking

### Problem
Need to continuously monitor Simphony posting files for new content without re-reading entire file or causing file locking conflicts.

### Solution

**Complete File Monitor Implementation:**

```csharp
// PostingFileMonitor.cs
public class PostingFileMonitor : IDisposable
{
    private readonly string _filePath;
    private readonly int _checkIntervalMs;
    private Timer _timer;
    private long _lastPosition;
    private DateTime _lastFileWriteTime;
    private readonly object _lock = new object();
    private bool _isRunning;

    public string FilePath => _filePath;

    public event EventHandler<NewLinesEventArgs> NewLinesDetected;
    public event EventHandler<ErrorEventArgs> ErrorOccurred;

    public PostingFileMonitor(string filePath, int checkIntervalMs = 1000)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));

        _filePath = filePath;
        _checkIntervalMs = checkIntervalMs;
        _lastPosition = 0;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning)
                return;

            _isRunning = true;

            // Initialize position if file exists
            if (File.Exists(_filePath))
            {
                var fileInfo = new FileInfo(_filePath);
                _lastPosition = fileInfo.Length; // Start from end of file
                _lastFileWriteTime = fileInfo.LastWriteTime;
            }

            // Start timer-based polling
            _timer = new Timer(CheckFile, null, 0, _checkIntervalMs);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _isRunning = false;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void CheckFile(object state)
    {
        if (!_isRunning)
            return;

        try
        {
            if (!File.Exists(_filePath))
                return;

            var fileInfo = new FileInfo(_filePath);

            // Detect file rotation (file recreated or truncated)
            if (fileInfo.LastWriteTime < _lastFileWriteTime ||
                fileInfo.Length < _lastPosition)
            {
                // File was rotated, start from beginning
                _lastPosition = 0;
                _lastFileWriteTime = fileInfo.LastWriteTime;
            }

            // Check if there's new content
            if (fileInfo.Length > _lastPosition)
            {
                var newLines = ReadNewLines(fileInfo.Length);
                if (newLines.Count > 0)
                {
                    OnNewLinesDetected(newLines);
                }
            }
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Error checking file: {ex.Message}", ex);
        }
    }

    private List<string> ReadNewLines(long currentFileSize)
    {
        var lines = new List<string>();

        try
        {
            // Open file with sharing enabled (KDS might still be writing)
            using (var fs = new FileStream(_filePath, FileMode.Open,
                                          FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.Unicode))
            {
                // Seek to last known position
                fs.Seek(_lastPosition, SeekOrigin.Begin);

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lines.Add(line);
                    }
                }

                // Update position for next read
                _lastPosition = fs.Position;
            }
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Error reading file: {ex.Message}", ex);
        }

        return lines;
    }

    public void ResetPosition()
    {
        lock (_lock)
        {
            _lastPosition = 0;
        }
    }

    public void SkipToEnd()
    {
        lock (_lock)
        {
            if (File.Exists(_filePath))
            {
                var fileInfo = new FileInfo(_filePath);
                _lastPosition = fileInfo.Length;
                _lastFileWriteTime = fileInfo.LastWriteTime;
            }
        }
    }

    protected virtual void OnNewLinesDetected(List<string> lines)
    {
        NewLinesDetected?.Invoke(this, new NewLinesEventArgs(lines));
    }

    protected virtual void OnErrorOccurred(string message, Exception ex)
    {
        ErrorOccurred?.Invoke(this, new ErrorEventArgs(ex));
    }

    public void Dispose()
    {
        Stop();
    }
}

// Event arguments
public class NewLinesEventArgs : EventArgs
{
    public List<string> Lines { get; }

    public NewLinesEventArgs(List<string> lines)
    {
        Lines = lines;
    }
}
```

### Configuration

```json
{
  "PostingFilePath": "C:\\Micros\\Simphony\\KDS\\PostingFiles\\KdsPosting.txt",
  "FileCheckIntervalMs": 1000
}
```

### Key Techniques
- **Position tracking**: Only read content added since last check
- **File rotation detection**: Detect when file is recreated (LastWriteTime < previous OR Length < position)
- **FileShare.ReadWrite**: Allow KDS to continue writing while we read
- **Encoding.Unicode**: KDS posting files use Unicode encoding
- **Timer-based polling**: Non-blocking continuous monitoring
- **Thread-safe**: Lock on critical sections

### Benefits
- Minimal memory footprint (only reads new content)
- No file locking conflicts (FileShare.ReadWrite)
- Handles file rotation gracefully
- Non-blocking continuous monitoring
- Thread-safe implementation

### When to Use
- Monitoring KDS posting files
- Monitoring any append-only log files
- Consuming file-based data feeds
- Real-time file watching without FileSystemWatcher limitations

### Trade-offs
- Timer-based (slight delay vs FileSystemWatcher)
- Must handle encoding properly (KDS uses Unicode)
- Requires position tracking state management

---

## Pattern 3: Event-Driven Processing Pipeline

### Problem
Complex data processing with multiple stages (monitoring, parsing, processing, publishing) needs clean separation of concerns and testability.

### Solution

**Pipeline Architecture:**

```
PostingFileMonitor (reads new lines)
    ↓ NewLinesDetected event
RecordParser (parses CSV to records)
    ↓ RecordParsed event
RecordProcessor (updates state)
    ↓ StateChanged event
OsbPublisher (posts to API)
    ↓ HTTP POST
External System
```

**Complete Implementation:**

```csharp
// Orchestration Service - Wires up the pipeline
public class KdsSyncService : IDisposable
{
    private readonly PostingFileMonitor _fileMonitor;
    private readonly RecordParser _parser;
    private readonly KdsStateManager _stateManager;
    private readonly RecordProcessor _processor;
    private readonly OsbPublisher _publisher;
    private readonly ILogManager _logger;

    private bool _isRunning;
    private readonly object _lock = new object();

    public event EventHandler<ServiceEventArgs> ServiceStarted;
    public event EventHandler<ServiceEventArgs> ServiceStopped;
    public event EventHandler<ErrorEventArgs> ServiceError;

    public KdsSyncService(ILogManager logger,
                          IConfigurationClient configurationClient,
                          IOsbClient osbClient)
    {
        _logger = logger;

        var config = configurationClient.ReadConfig();

        // Initialize pipeline components
        _fileMonitor = new PostingFileMonitor(config.PostingFilePath,
                                              config.FileCheckIntervalMs);
        _parser = new RecordParser();
        _stateManager = new KdsStateManager(config);
        _processor = new RecordProcessor(_stateManager);
        _publisher = new OsbPublisher(_logger, config, osbClient);

        // Wire up the pipeline via events
        // Stage 1: File Monitor → Parser
        _parser.SubscribeToMonitor(_fileMonitor);

        // Stage 2: Parser → Processor
        _processor.SubscribeToParser(_parser);

        // Stage 3: Processor → Publisher
        _processor.StateChanged += OnStateChanged;

        // Additional: State expiration → Publisher
        _stateManager.Expired += OnStateChanged;

        // Error handling at each stage
        _fileMonitor.ErrorOccurred += OnComponentError;
        _parser.ParseError += OnParseError;
        _publisher.PublishError += OnPublishError;

        // Debug logging
        _fileMonitor.NewLinesDetected += (s, e) =>
            _logger.LogDebug($"Detected {e.Lines.Count} new lines");
        _parser.RecordParsed += (s, e) =>
            _logger.LogDebug($"Parsed {e.Record.RecordType} record");
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                _logger.LogWarn("Service is already running");
                return;
            }

            try
            {
                _logger.LogInfo("Starting KDS OSB Sync Service...");

                // Enable startup mode (see Pattern 4)
                _publisher.IsStartupMode = true;

                // Build initial state from existing file
                ProcessExistingFileContent().GetAwaiter().GetResult();

                // Remove expired orders
                _stateManager.ExpireOldOrders(null);

                // Disable startup mode
                _publisher.IsStartupMode = false;

                // Set monitor to only read new content going forward
                _fileMonitor.SkipToEnd();

                // Start monitoring for new changes
                _fileMonitor.Start();

                _isRunning = true;
                OnServiceStarted();
                _logger.LogInfo("Service started successfully");

                // Publish current state once
                var allSnapshots = _stateManager.GetAllSnapshots();
                _logger.LogInfo($"Publishing {allSnapshots.Count} device snapshots");
                foreach (var snapshot in allSnapshots)
                {
                    _publisher.PublishSnapshot(snapshot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to start service: {ex.Message}");
                OnServiceError(ex);
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;

            try
            {
                _logger.LogInfo("Stopping KDS OSB Sync Service...");

                _fileMonitor.Stop();
                _publisher.Stop();

                _isRunning = false;

                OnServiceStopped();
                _logger.LogInfo("Service stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping service: {ex.Message}");
                OnServiceError(ex);
            }
        }
    }

    private void OnStateChanged(object sender, StateChangedEventArgs e)
    {
        _logger.LogInfo($"State changed for device {e.DeviceId}: {e.ChangeType}");

        // Automatically publish updated state to external system
        _publisher.PublishSnapshot(e.Snapshot);
    }

    private void OnComponentError(object sender, ErrorEventArgs e)
    {
        _logger.LogError($"Component error: {e.GetException()?.Message}");
        OnServiceError(e.GetException());
    }

    private void OnParseError(object sender, ParseErrorEventArgs e)
    {
        _logger.LogWarn($"Failed to parse line: {e.Error?.Message}");
        // Don't stop service for parse errors, just log and continue
    }

    private void OnPublishError(object sender, PublishErrorEventArgs e)
    {
        _logger.LogError($"Failed to publish to OSB {e.DeviceId}: {e.Error?.Message}");
        // Could implement retry logic here
    }

    private async Task ProcessExistingFileContent()
    {
        _logger.LogInfo("Reading existing posting file content...");

        var filePath = _fileMonitor.FilePath;

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            _logger.LogWarn("Posting file not found, starting with empty state");
            return;
        }

        try
        {
            var lines = File.ReadAllLines(filePath, Encoding.Unicode);
            _logger.LogInfo($"Processing {lines.Length} existing lines");

            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _parser.ParseLine(line);
                }
            }

            _logger.LogInfo("Completed processing existing file content");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing existing file: {ex.Message}");
            throw;
        }
    }

    protected virtual void OnServiceStarted()
    {
        ServiceStarted?.Invoke(this, new ServiceEventArgs
        {
            Message = "Service started"
        });
    }

    protected virtual void OnServiceStopped()
    {
        ServiceStopped?.Invoke(this, new ServiceEventArgs
        {
            Message = "Service stopped"
        });
    }

    protected virtual void OnServiceError(Exception ex)
    {
        ServiceError?.Invoke(this, new ErrorEventArgs(ex));
    }

    public void Dispose()
    {
        Stop();
        _fileMonitor?.Dispose();
        _stateManager?.Dispose();
    }
}
```

**Pipeline Component: Parser**

```csharp
// RecordParser.cs - Converts lines to typed records
public class RecordParser
{
    private readonly char _delimiter = ',';

    public event EventHandler<RecordParsedEventArgs> RecordParsed;
    public event EventHandler<ParseErrorEventArgs> ParseError;

    public void SubscribeToMonitor(PostingFileMonitor monitor)
    {
        monitor.NewLinesDetected += OnNewLinesDetected;
    }

    private void OnNewLinesDetected(object sender, NewLinesEventArgs e)
    {
        foreach (var line in e.Lines)
        {
            ParseLine(line);
        }
    }

    public void ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        try
        {
            var fields = ParseCsvLine(line);
            if (fields.Length == 0)
                return;

            var record = CreateRecord(fields);
            if (record != null)
            {
                record.RawData = line;
                OnRecordParsed(record);
            }
        }
        catch (Exception ex)
        {
            OnParseError(line, ex);
        }
    }

    private KdsRecord CreateRecord(string[] fields)
    {
        if (fields.Length == 0)
            return null;

        var recordType = fields[0].Trim();
        KdsRecord record = null;

        switch (recordType)
        {
            case "1.0":
                record = new SuborderDoneRecord();
                break;
            case "2.0":
                record = new CheckClosedRecord();
                break;
            case "5.2":
                record = new DistributionStateRecord();
                break;
            default:
                // Unknown record type, ignore
                return null;
        }

        record.Parse(fields);
        return record;
    }

    protected virtual void OnRecordParsed(KdsRecord record)
    {
        RecordParsed?.Invoke(this, new RecordParsedEventArgs(record));
    }

    protected virtual void OnParseError(string line, Exception ex)
    {
        ParseError?.Invoke(this, new ParseErrorEventArgs
        {
            Line = line,
            Error = ex
        });
    }
}
```

### Benefits
- **Separation of Concerns**: Each component has single responsibility
- **Testability**: Each stage can be unit tested independently
- **Extensibility**: Easy to add new consumers (just subscribe to events)
- **Error Isolation**: Errors in one stage don't crash entire pipeline
- **Observability**: Event subscriptions enable logging/metrics at each stage

### When to Use
- Multi-stage data processing (read, parse, transform, publish)
- Complex workflows requiring testability
- Systems where stages may be reused or extended
- Scenarios requiring detailed observability

### Integration with Universal Patterns
- Uses standard event pattern (EventHandler<TEventArgs>)
- Each component implements IDisposable
- Orchestrator manages component lifecycle
- Logging at each stage via ILogManager

---

## Pattern 4: Startup Mode Optimization

### Problem
Service restart processes historical data from posting files, causing hundreds of API calls. This floods external API and temporarily displays incorrect state (e.g., showing all historical orders).

### Solution

**Startup Mode Flag Pattern:**

```csharp
// OsbPublisher.cs
public class OsbPublisher
{
    private readonly ILogManager _logger;
    private readonly Config _config;
    private readonly IOsbClient _osbClient;

    // Startup mode flag suppresses publishing during initialization
    public bool IsStartupMode { get; set; }
    public bool Closed { get; set; }

    public OsbPublisher(ILogManager logger, Config config, IOsbClient osbClient)
    {
        _logger = logger;
        _config = config;
        _osbClient = osbClient;
    }

    public void PublishSnapshot(OsbStateSnapshot snapshot)
    {
        // Skip publishing during startup mode
        if (IsStartupMode)
        {
            _logger.LogDebug($"Skipping publish for {snapshot.DeviceId} (startup mode)");
            return;
        }

        try
        {
            var mapping = _config.DeviceMappings
                .FirstOrDefault(x => x.DeviceId == snapshot.DeviceId);

            if (mapping == null)
            {
                _logger.LogWarn($"No mapping found for device {snapshot.DeviceId}");
                return;
            }

            _logger.LogInfo($"Publishing snapshot for {mapping.Name} ({snapshot.DeviceId})");

            _osbClient.PublishDeviceSnapshot(mapping.Path, mapping.Name, snapshot)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to publish snapshot: {ex.Message}");
            OnPublishError(snapshot.DeviceId, ex);
        }
    }

    public void Stop()
    {
        // Cleanup if needed
    }

    protected virtual void OnPublishError(string deviceId, Exception ex)
    {
        PublishError?.Invoke(this, new PublishErrorEventArgs
        {
            DeviceId = deviceId,
            Error = ex
        });
    }

    public event EventHandler<PublishErrorEventArgs> PublishError;
}
```

**Startup Sequence in Service:**

```csharp
public void Start()
{
    lock (_lock)
    {
        if (_isRunning)
            return;

        try
        {
            _logger.LogInfo("Starting KDS OSB Sync Service...");

            // ===== PHASE 1: SILENT STATE BUILDING =====
            // Enable startup mode to suppress publishing
            _publisher.IsStartupMode = true;

            // Read entire existing file to build current state in memory
            // This processes hundreds of historical records silently
            ProcessExistingFileContent().GetAwaiter().GetResult();

            // Remove old orders (>5 minutes) before publishing
            _stateManager.ExpireOldOrders(null);

            // ===== PHASE 2: SINGLE PUBLISH =====
            // Disable startup mode - now we can publish
            _publisher.IsStartupMode = false;

            // Set file monitor to only read NEW content going forward
            _fileMonitor.SkipToEnd();

            // Start monitoring for new changes
            _fileMonitor.Start();

            _isRunning = true;
            OnServiceStarted();

            // ===== PHASE 3: INITIAL STATE PUBLISH =====
            // Publish current state ONCE (one API call per device)
            var allSnapshots = _stateManager.GetAllSnapshots();
            _logger.LogInfo($"Publishing {allSnapshots.Count} device snapshots after startup");
            foreach (var snapshot in allSnapshots)
            {
                _publisher.PublishSnapshot(snapshot);
            }

            _logger.LogInfo("Service started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to start service: {ex.Message}");
            throw;
        }
    }
}
```

### Configuration

```json
{
  "FileCheckIntervalMs": 1000,
  "ReadyOrderExpirationMinutes": 5
}
```

### Problem Solved: API Flooding
**Without startup mode:**
- Posting file contains 500 historical orders
- Service processes each order → 500 state changes → 500 API calls
- External API gets flooded during startup
- Status board temporarily shows all historical orders

**With startup mode:**
- Posting file contains 500 historical orders
- Service processes all orders silently (IsStartupMode = true)
- Expire old orders (>5 min)
- Result: 10 current orders remaining
- Single publish per device → 3 API calls total (99.4% reduction)

### Benefits
- **99% reduction in API calls** during service restart
- Prevents temporary "order flood" on displays
- Faster startup (no network I/O during state building)
- Normal runtime behavior unaffected

### When to Use
- Any hosted service that processes historical data on startup
- File-based data sources with append-only logs
- Scenarios where external API has rate limits
- Systems that need fast startup time

### Trade-offs
- Adds complexity (startup mode flag)
- Must remember to set/unset flag correctly
- Initial state publish is delayed until after processing

---

## Pattern 5: Path-Based Hash Tracking for Content Sync

### Problem
Need to distribute same content (e.g., HTML page) to multiple destinations, but only upload when content actually changes at each destination. Traditional key-based hashing prevents multiple uploads of same content.

### Solution

**Path-Based Hash Manager:**

```csharp
// ContentHashManager.cs
public class ContentHashManager
{
    private readonly Dictionary<string, string> _pathHashes;
    private readonly ILogManager _logger;

    public ContentHashManager(ILogManager logger)
    {
        _logger = logger;
        _pathHashes = new Dictionary<string, string>();
    }

    // Check if content has changed for THIS SPECIFIC PATH
    public bool HasContentChanged(string path, byte[] content)
    {
        if (content == null || content.Length == 0)
            return false;

        var hash = ComputeSha256Hash(content);

        // First upload for this path
        if (!_pathHashes.TryGetValue(path, out var existingHash))
        {
            _logger.LogDebug($"No existing hash for path: {path}");
            return true;
        }

        // Compare with existing hash for this path
        var hasChanged = hash != existingHash;

        if (hasChanged)
        {
            _logger.LogDebug($"Content changed for path: {path}");
        }

        return hasChanged;
    }

    // Update hash after successful upload
    public void UpdateHash(string path, byte[] content)
    {
        if (content == null || content.Length == 0)
            return;

        var hash = ComputeSha256Hash(content);
        _pathHashes[path] = hash;

        _logger.LogDebug($"Updated hash for path: {path}");
    }

    // Force re-upload of all content
    public void ClearHashes()
    {
        _pathHashes.Clear();
        _logger.LogInfo("Cleared all content hashes");
    }

    private string ComputeSha256Hash(byte[] data)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var hash = sha256.ComputeHash(data);
            return Convert.ToBase64String(hash);
        }
    }
}
```

**Content Sync Service:**

```csharp
// ConfigSyncService.cs
public class ConfigSyncService : IDisposable
{
    private readonly IConfigurationClient _configurationClient;
    private readonly IOsbClient _osbClient;
    private readonly ContentHashManager _hashManager;
    private readonly ILogManager _logger;

    private Timer _syncTimer;
    private bool _isRunning;
    private readonly object _lock = new object();

    public event EventHandler<ServiceEventArgs> ServiceStarted;
    public event EventHandler<ServiceEventArgs> ServiceStopped;
    public event EventHandler<ContentSyncEventArgs> SyncCompleted;
    public event EventHandler<ErrorEventArgs> ServiceError;

    public ConfigSyncService(IConfigurationClient configurationClient,
                            IOsbClient osbClient,
                            ILogManager logger)
    {
        _configurationClient = configurationClient;
        _osbClient = osbClient;
        _logger = logger;
        _hashManager = new ContentHashManager(logger);
    }

    public async Task Start()
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                _logger.LogWarn("ConfigSyncService is already running");
                return;
            }

            try
            {
                _logger.LogInfo("Starting Config Sync Service...");

                // Perform initial sync on startup
                await PerformSync();

                // Start periodic sync timer
                var config = _configurationClient.ReadConfig();
                if (config.EnableConfigSync)
                {
                    var interval = TimeSpan.FromSeconds(config.ConfigSyncIntervalSec);
                    _syncTimer = new Timer(OnSyncTimer, null, interval, interval);
                    _logger.LogInfo($"Config sync timer started - interval: {interval}");
                }

                _isRunning = true;
                OnServiceStarted();
                _logger.LogInfo("Config Sync Service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to start Config Sync Service: {ex.Message}");
                OnServiceError(ex);
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;

            try
            {
                _logger.LogInfo("Stopping Config Sync Service...");

                _syncTimer?.Dispose();
                _syncTimer = null;

                _isRunning = false;

                OnServiceStopped();
                _logger.LogInfo("Config Sync Service stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping Config Sync Service: {ex.Message}");
                OnServiceError(ex);
            }
        }
    }

    // Force a manual sync of all content (clears hashes)
    public async Task ForceSyncAsync()
    {
        _logger.LogInfo("Starting forced content sync...");

        try
        {
            // Clear hashes to force full upload
            _hashManager.ClearHashes();

            // Perform sync
            await PerformSync();

            _logger.LogInfo("Forced content sync completed");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during forced sync: {ex.Message}");
            OnServiceError(ex);
            throw;
        }
    }

    private async void OnSyncTimer(object state)
    {
        if (!_isRunning)
            return;

        try
        {
            await PerformSync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during scheduled sync: {ex.Message}");
            OnServiceError(ex);
        }
    }

    private async Task PerformSync()
    {
        try
        {
            _logger.LogInfo("Starting content sync...");

            // Read current configuration
            var config = _configurationClient.ReadConfig();
            var contentMappings = config.ContentMappings ?? new List<ContentMapping>();

            if (contentMappings.Count == 0)
            {
                _logger.LogDebug("No content mappings configured - skipping sync");
                return;
            }

            var totalFiles = 0;
            var uploadedFiles = 0;
            var changedPaths = new List<string>();

            // Process each mapping independently
            foreach (var mapping in contentMappings)
            {
                totalFiles++;

                try
                {
                    // Read content from Simphony via ZonableKey
                    var fileContent = _configurationClient.ReadFile(mapping.ZonableKey);

                    if (fileContent == null || fileContent.Length == 0)
                    {
                        _logger.LogWarn($"No content found for zonable key: {mapping.ZonableKey}");
                        continue;
                    }

                    // Check if content changed for THIS SPECIFIC PATH
                    // KEY INNOVATION: Hash by path, not by ZonableKey
                    if (_hashManager.HasContentChanged(mapping.Path, fileContent))
                    {
                        try
                        {
                            _logger.LogDebug($"Uploading changed content: {mapping.ZonableKey} -> {mapping.Path} ({fileContent.Length} bytes)");

                            // Upload the content to the specified path
                            await _osbClient.PublishWebContent(mapping.Path, fileContent);

                            // Update the hash after successful upload
                            _hashManager.UpdateHash(mapping.Path, fileContent);

                            uploadedFiles++;
                            changedPaths.Add(mapping.Path);

                            _logger.LogInfo($"Successfully uploaded: {mapping.ZonableKey} -> {mapping.Path}");
                        }
                        catch (Exception uploadEx)
                        {
                            _logger.LogError($"Failed to upload {mapping.ZonableKey} to {mapping.Path}: {uploadEx.Message}");
                        }
                    }
                    else
                    {
                        _logger.LogDebug($"No change detected for path: {mapping.Path}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing mapping {mapping.ZonableKey} -> {mapping.Path}: {ex.Message}");
                }
            }

            OnSyncCompleted(changedPaths, totalFiles, uploadedFiles);

            if (uploadedFiles > 0)
            {
                _logger.LogInfo($"Content sync completed - {uploadedFiles} of {totalFiles} files uploaded");
            }
            else
            {
                _logger.LogDebug($"Content sync completed - no changes detected ({totalFiles} files checked)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during content sync: {ex.Message}");
            OnServiceError(ex);
            throw;
        }
    }

    protected virtual void OnServiceStarted()
    {
        ServiceStarted?.Invoke(this, new ServiceEventArgs
        {
            Message = "Config Sync Service started"
        });
    }

    protected virtual void OnServiceStopped()
    {
        ServiceStopped?.Invoke(this, new ServiceEventArgs
        {
            Message = "Config Sync Service stopped"
        });
    }

    protected virtual void OnSyncCompleted(List<string> changedPaths,
                                          int totalFiles,
                                          int uploadedFiles)
    {
        SyncCompleted?.Invoke(this, new ContentSyncEventArgs
        {
            Message = $"Sync completed - {uploadedFiles} of {totalFiles} files uploaded",
            UpdatedPaths = changedPaths,
            TotalFilesProcessed = totalFiles,
            FilesUploaded = uploadedFiles
        });
    }

    protected virtual void OnServiceError(Exception ex)
    {
        ServiceError?.Invoke(this, new ErrorEventArgs(ex));
    }

    public void Dispose()
    {
        Stop();
    }
}
```

### Configuration

```json
{
  "EnableConfigSync": true,
  "ConfigSyncIntervalSec": 3600,
  "ContentMappings": [
    {
      "ZonableKey": "OrderDevice.html",
      "Path": "/SSP1/1/index.html"
    },
    {
      "ZonableKey": "OrderDevice.html",
      "Path": "/SSP1/2/index.html"
    },
    {
      "ZonableKey": "OrderDevice.html",
      "Path": "/SSP1/3/index.html"
    },
    {
      "ZonableKey": "LocationOverview.html",
      "Path": "/SSP1/overview/index.html"
    }
  ]
}
```

**Entity:**

```csharp
public class ContentMapping
{
    public string ZonableKey { get; set; }  // Source in Simphony
    public string Path { get; set; }        // Destination path
}
```

### Key Innovation: Path-Based vs Key-Based Hashing

**Traditional approach (KEY-based hashing):**
```csharp
// BAD: Hashes by ZonableKey
if (_hashManager.HasContentChanged(mapping.ZonableKey, fileContent))
{
    // Problem: Same ZonableKey used 3 times (devices 1, 2, 3)
    // Only first path would be uploaded, other 2 would be skipped!
}
```

**This approach (PATH-based hashing):**
```csharp
// GOOD: Hashes by destination Path
if (_hashManager.HasContentChanged(mapping.Path, fileContent))
{
    // Each path tracked independently
    // OrderDevice.html uploaded to /SSP1/1/, /SSP1/2/, /SSP1/3/
    // All 3 uploads succeed
}
```

### Benefits
- **Supports content distribution**: Same content to multiple destinations
- **Minimizes API calls**: Only uploads when content changes at destination
- **Independent tracking**: Each destination tracked separately
- **Graceful failure**: One mapping failure doesn't stop others
- **Manual force sync**: Can clear hashes to force re-upload

### When to Use
- Distributing same content to multiple locations
- HTML pages served at multiple paths
- Configuration files deployed to multiple servers
- Any scenario where source:destination is 1:N

### Trade-offs
- More complex than key-based hashing
- Slightly more memory (stores hash per path, not per key)
- Must use paths consistently (case-sensitive)

---

## Pattern 6: Third-Party API Integration

### Problem
Need to post data to external REST API with authentication, error handling, and retry capability.

### Solution

**Simple HTTP Client Interface:**

```csharp
// IOsbClient.cs
public interface IOsbClient
{
    Task PublishDeviceSnapshot(string osbPath, string name,
                               OsbStateSnapshot snapshot);
    Task PublishWebContent(string uploadPath, byte[] fileContent);
}
```

**HTTP Client Implementation:**

```csharp
// OsbClient.cs
public class OsbClient : IOsbClient
{
    private readonly ILogManager _logger;
    private readonly Config _config;

    public OsbClient(ILogManager logger, IConfigurationClient configurationClient)
    {
        _logger = logger;
        _config = configurationClient.ReadConfig();
    }

    public async Task PublishDeviceSnapshot(string osbPath, string name,
                                            OsbStateSnapshot snapshot)
    {
        var body = new OsbPayload
        {
            Name = name,
            Timestamp = snapshot.Timestamp.ToUniversalTime().ToString("O"),
            Status = snapshot.Closed ? "closed" : "open",
            ShouldReload = false,

            // Sort for optimal display
            Preparing = snapshot.PreparingOrders
                .OrderBy(x => x.CreatedAt)
                .Select(x => new OsbOrder
                {
                    Id = x.CheckNumber,
                    CreatedAt = x.CreatedAt,
                }).ToArray(),

            Ready = snapshot.ReadyOrders
                .OrderByDescending(x => x.DoneAt)
                .Select(x => new OsbOrder
                {
                    Id = x.CheckNumber,
                    CreatedAt = x.CreatedAt,
                    ReadyAt = x.DoneAt,
                }).ToArray(),
        };

        var url = $"{_config.Url.TrimEnd('/')}/api{osbPath}";
        var data = JsonConvert.SerializeObject(body, Formatting.Indented);

        using (var client = new WebClient())
        {
            client.Headers.Add("X-Functions-Key", _config.ApiKey);
            client.Headers.Add("content-type", "application/json");

            _logger.LogDebug($"Posting snapshot: {osbPath}{Environment.NewLine}{data}");

            try
            {
                var result = await client.UploadStringTaskAsync(url, "POST", data);
                _logger.LogDebug($"Successfully published snapshot to {osbPath}");
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to publish snapshot to {osbPath}: {e.Message}");
                throw;
            }
        }
    }

    public async Task PublishWebContent(string uploadPath, byte[] fileContent)
    {
        var url = $"{_config.Url.TrimEnd('/')}{uploadPath}";

        using (var client = new WebClient())
        {
            client.Headers.Add("X-Functions-Key", _config.ApiKey);
            client.Headers.Add("Content-Type", "application/octet-stream");

            _logger.LogDebug($"Uploading content to {uploadPath} ({fileContent.Length} bytes)");

            try
            {
                var result = await client.UploadDataTaskAsync(url, "POST", fileContent);
                _logger.LogDebug($"Successfully published content to {uploadPath}");
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to publish content to {uploadPath}: {e.Message}");
                throw;
            }
        }
    }
}
```

**Payload Entity:**

```csharp
// OsbPayload.cs
public class OsbPayload
{
    public string Name { get; set; }
    public string Timestamp { get; set; }
    public string Status { get; set; }
    public bool ShouldReload { get; set; }
    public OsbOrder[] Preparing { get; set; }
    public OsbOrder[] Ready { get; set; }
}

public class OsbOrder
{
    public string Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
}
```

### Configuration

```json
{
  "Url": "https://myapi.azurewebsites.net",
  "ApiKey": "your-api-key-here"
}
```

### Benefits
- **Interface abstraction**: Easy to stub for testing
- **Async/await**: Non-blocking I/O operations
- **Configuration-driven**: URL and auth from config
- **Payload transformation**: Domain objects → JSON
- **Header management**: Authentication via custom headers

### When to Use
- Posting to external REST APIs
- JSON payload transformation required
- Authentication via headers
- Non-blocking HTTP operations

### Error Handling Best Practices

```csharp
// In Publisher class
try
{
    await _osbClient.PublishSnapshot(...);
}
catch (Exception ex)
{
    _logger.LogError($"Publish failed: {ex.Message}");
    OnPublishError(deviceId, ex);
    // Don't crash service - just log and continue
    // Could implement retry queue here
}
```

### Integration with Universal Patterns
- Uses interface abstraction (IOsbClient)
- Configuration via IConfigurationClient
- Logging via ILogManager
- Stub implementation for DEBUG builds

---

## Pattern 7: Timer-Based Background Processing

### Problem
Need to perform periodic tasks (content sync, state expiration) continuously while service is running.

### Solution

**Timer-Based Service:**

```csharp
public class ConfigSyncService : IDisposable
{
    private Timer _syncTimer;
    private bool _isRunning;
    private readonly object _lock = new object();

    public async Task Start()
    {
        lock (_lock)
        {
            if (_isRunning)
                return;

            try
            {
                // Perform initial sync immediately
                await PerformSync();

                // Start periodic timer
                var config = _configurationClient.ReadConfig();
                if (config.EnableConfigSync)
                {
                    var interval = TimeSpan.FromSeconds(config.ConfigSyncIntervalSec);
                    _syncTimer = new Timer(OnSyncTimer, null, interval, interval);
                    _logger.LogInfo($"Sync timer started - interval: {interval}");
                }

                _isRunning = true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to start: {ex.Message}");
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;

            _syncTimer?.Dispose();
            _syncTimer = null;
            _isRunning = false;
        }
    }

    private async void OnSyncTimer(object state)
    {
        if (!_isRunning)
            return;

        try
        {
            await PerformSync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during scheduled sync: {ex.Message}");
            // Don't stop service - log and continue
        }
    }

    private async Task PerformSync()
    {
        // Perform sync logic here
    }

    public void Dispose()
    {
        Stop();
    }
}
```

**State Expiration Timer:**

```csharp
public class KdsStateManager : IDisposable
{
    private Timer _expirationTimer;
    private readonly int _expirationMinutes;

    public event EventHandler<StateChangedEventArgs> Expired;

    public KdsStateManager(Config config)
    {
        _expirationMinutes = config.ReadyOrderExpirationMinutes;

        // Check for expired orders every minute
        _expirationTimer = new Timer(
            _ => ExpireOldOrders(null),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    public void ExpireOldOrders(string deviceId)
    {
        var cutoff = DateTime.Now.AddMinutes(-_expirationMinutes);

        var devicesToCheck = deviceId == null
            ? _deviceStates.Values
            : new[] { _deviceStates[deviceId] };

        foreach (var device in devicesToCheck)
        {
            var expired = device.ReadyOrders
                .Where(x => x.DoneAt < cutoff)
                .ToList();

            foreach (var order in expired)
            {
                device.ReadyOrders.Remove(order);
                OnExpired(new StateChangedEventArgs
                {
                    DeviceId = device.DeviceId,
                    ChangeType = "OrderExpired",
                    Snapshot = device.ToSnapshot()
                });
            }
        }
    }

    protected virtual void OnExpired(StateChangedEventArgs args)
    {
        Expired?.Invoke(this, args);
    }

    public void Dispose()
    {
        _expirationTimer?.Dispose();
    }
}
```

### Configuration

```json
{
  "ConfigSyncIntervalSec": 3600,
  "ReadyOrderExpirationMinutes": 5
}
```

### Benefits
- **Configuration-driven intervals**: Easy to adjust timing
- **Non-blocking**: Doesn't block main thread
- **Thread-safe**: Lock on state changes
- **Graceful errors**: Errors don't stop service
- **Automatic cleanup**: IDisposable pattern

### When to Use
- Periodic data synchronization
- State expiration/cleanup
- Health checks or monitoring
- Any recurring background task

### Best Practices

**1. Always check running flag:**
```csharp
private async void OnSyncTimer(object state)
{
    if (!_isRunning)  // Exit early if service stopped
        return;

    // Perform work
}
```

**2. Use lock for thread safety:**
```csharp
lock (_lock)
{
    if (_isRunning)
        return;

    _syncTimer = new Timer(...);
    _isRunning = true;
}
```

**3. Dispose timer properly:**
```csharp
public void Dispose()
{
    Stop();
    _syncTimer?.Dispose();
}
```

**4. Handle errors gracefully:**
```csharp
catch (Exception ex)
{
    _logger.LogError($"Error during scheduled task: {ex.Message}");
    // Continue running - don't stop service
}
```

---

## Pattern 8: State Management with Auto-Expiration

### Problem
Need to track device-specific state (orders in preparing/ready status) with automatic expiration of old items.

### Solution

**State Manager with Expiration:**

```csharp
// KdsStateManager.cs
public class KdsStateManager : IDisposable
{
    private readonly Dictionary<string, DeviceState> _deviceStates;
    private readonly Config _config;
    private readonly Timer _expirationTimer;
    private readonly object _lock = new object();

    public event EventHandler<StateChangedEventArgs> StateChanged;
    public event EventHandler<StateChangedEventArgs> Expired;

    public KdsStateManager(Config config)
    {
        _config = config;
        _deviceStates = new Dictionary<string, DeviceState>();

        // Initialize device states from config
        foreach (var mapping in config.DeviceMappings)
        {
            _deviceStates[mapping.DeviceId] = new DeviceState
            {
                DeviceId = mapping.DeviceId,
                DeviceName = mapping.Name
            };
        }

        // Start expiration timer (check every minute)
        _expirationTimer = new Timer(
            _ => ExpireOldOrders(null),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    public void AddOrder(string deviceId, Order order)
    {
        lock (_lock)
        {
            if (!_deviceStates.TryGetValue(deviceId, out var device))
                return;

            device.PreparingOrders.Add(order);

            OnStateChanged(new StateChangedEventArgs
            {
                DeviceId = deviceId,
                ChangeType = "OrderAdded",
                Snapshot = device.ToSnapshot()
            });
        }
    }

    public void MoveToReady(string deviceId, string checkNumber, DateTime doneAt)
    {
        lock (_lock)
        {
            if (!_deviceStates.TryGetValue(deviceId, out var device))
                return;

            var order = device.PreparingOrders
                .FirstOrDefault(x => x.CheckNumber == checkNumber);

            if (order == null)
                return;

            device.PreparingOrders.Remove(order);
            order.DoneAt = doneAt;
            device.ReadyOrders.Add(order);

            OnStateChanged(new StateChangedEventArgs
            {
                DeviceId = deviceId,
                ChangeType = "OrderReady",
                Snapshot = device.ToSnapshot()
            });
        }
    }

    public void RemoveOrder(string deviceId, string checkNumber)
    {
        lock (_lock)
        {
            if (!_deviceStates.TryGetValue(deviceId, out var device))
                return;

            var removed = false;

            // Try removing from preparing
            var preparing = device.PreparingOrders
                .FirstOrDefault(x => x.CheckNumber == checkNumber);
            if (preparing != null)
            {
                device.PreparingOrders.Remove(preparing);
                removed = true;
            }

            // Try removing from ready
            var ready = device.ReadyOrders
                .FirstOrDefault(x => x.CheckNumber == checkNumber);
            if (ready != null)
            {
                device.ReadyOrders.Remove(ready);
                removed = true;
            }

            if (removed)
            {
                OnStateChanged(new StateChangedEventArgs
                {
                    DeviceId = deviceId,
                    ChangeType = "OrderRemoved",
                    Snapshot = device.ToSnapshot()
                });
            }
        }
    }

    public void ExpireOldOrders(string deviceId)
    {
        var cutoff = DateTime.Now.AddMinutes(-_config.ReadyOrderExpirationMinutes);

        lock (_lock)
        {
            var devicesToCheck = deviceId == null
                ? _deviceStates.Values
                : new[] { _deviceStates[deviceId] };

            foreach (var device in devicesToCheck)
            {
                var expired = device.ReadyOrders
                    .Where(x => x.DoneAt < cutoff)
                    .ToList();

                if (expired.Count == 0)
                    continue;

                foreach (var order in expired)
                {
                    device.ReadyOrders.Remove(order);
                }

                OnExpired(new StateChangedEventArgs
                {
                    DeviceId = device.DeviceId,
                    ChangeType = "OrderExpired",
                    Snapshot = device.ToSnapshot()
                });
            }
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            foreach (var device in _deviceStates.Values)
            {
                device.PreparingOrders.Clear();
                device.ReadyOrders.Clear();
            }
        }
    }

    public OsbStateSnapshot GetSnapshot(string deviceId)
    {
        lock (_lock)
        {
            if (!_deviceStates.TryGetValue(deviceId, out var device))
                return null;

            return device.ToSnapshot();
        }
    }

    public List<OsbStateSnapshot> GetAllSnapshots()
    {
        lock (_lock)
        {
            return _deviceStates.Values
                .Select(x => x.ToSnapshot())
                .ToList();
        }
    }

    protected virtual void OnStateChanged(StateChangedEventArgs args)
    {
        StateChanged?.Invoke(this, args);
    }

    protected virtual void OnExpired(StateChangedEventArgs args)
    {
        Expired?.Invoke(this, args);
    }

    public void Dispose()
    {
        _expirationTimer?.Dispose();
    }
}

// DeviceState.cs
public class DeviceState
{
    public string DeviceId { get; set; }
    public string DeviceName { get; set; }
    public List<Order> PreparingOrders { get; set; } = new List<Order>();
    public List<Order> ReadyOrders { get; set; } = new List<Order>();

    public OsbStateSnapshot ToSnapshot()
    {
        return new OsbStateSnapshot
        {
            DeviceId = DeviceId,
            DeviceName = DeviceName,
            PreparingOrders = PreparingOrders.ToList(),
            ReadyOrders = ReadyOrders.ToList(),
            Timestamp = DateTime.UtcNow
        };
    }
}

// Order.cs
public class Order
{
    public string CheckNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime DoneAt { get; set; }
}

// OsbStateSnapshot.cs
public class OsbStateSnapshot
{
    public string DeviceId { get; set; }
    public string DeviceName { get; set; }
    public List<Order> PreparingOrders { get; set; }
    public List<Order> ReadyOrders { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Closed { get; set; }
}
```

### Configuration

```json
{
  "ReadyOrderExpirationMinutes": 5,
  "DeviceMappings": [
    { "DeviceId": "201", "Name": "Burger", "Path": "/SSP1/1" },
    { "DeviceId": "202", "Name": "Pizza", "Path": "/SSP1/2" },
    { "DeviceId": "203", "Name": "Salad", "Path": "/SSP1/3" }
  ]
}
```

### Benefits
- **Single source of truth**: All state in one manager
- **Thread-safe**: Lock on all mutations
- **Event-driven**: State changes trigger events
- **Automatic cleanup**: Timer-based expiration
- **Snapshot pattern**: Immutable snapshots for publishing

### When to Use
- Multi-device state tracking
- Order/item lifecycle management
- Any state that needs automatic expiration
- Status board/dashboard applications

### State Lifecycle

```
[New Order]
    ↓
AddOrder() → PreparingOrders list
    ↓ (order completed)
MoveToReady() → ReadyOrders list
    ↓ (5 minutes)
ExpireOldOrders() → Removed
    ↓ OR (customer picked up)
RemoveOrder() → Removed
```

---

## Anti-Patterns to Avoid

### ❌ Anti-Pattern 1: Using POS Events for Background Services

**DON'T:**
```csharp
// BAD: Trying to use transaction events for background processing
BeforeEndOfCheckEvent += (s, a) =>
{
    // This blocks POS transaction!
    _backgroundService.PerformLongRunningTask();
    return EventProcessingInstruction.Continue;
};
```

**DO:**
```csharp
// GOOD: Use lifecycle events for background services
OpsReadyEvent += (s, a) =>
{
    // Starts background service that runs independently
    _backgroundService.Start(unattended: true);
    return EventProcessingInstruction.Continue;
};
```

### ❌ Anti-Pattern 2: File Reading Without Position Tracking

**DON'T:**
```csharp
// BAD: Re-reads entire file every time
var lines = File.ReadAllLines(filePath);
foreach (var line in lines)
{
    ProcessLine(line); // Processes all historical data every time!
}
```

**DO:**
```csharp
// GOOD: Only reads new content
fs.Seek(_lastPosition, SeekOrigin.Begin);
while ((line = reader.ReadLine()) != null)
{
    ProcessLine(line); // Only new lines
}
_lastPosition = fs.Position; // Update position
```

### ❌ Anti-Pattern 3: Publishing During Startup Without Optimization

**DON'T:**
```csharp
// BAD: Publishes every historical order during startup
foreach (var line in existingLines)
{
    var record = ParseLine(line);
    UpdateState(record);
    PublishState(); // Hundreds of API calls!
}
```

**DO:**
```csharp
// GOOD: Build state silently, publish once
_publisher.IsStartupMode = true;
foreach (var line in existingLines)
{
    var record = ParseLine(line);
    UpdateState(record); // No publishing
}
_publisher.IsStartupMode = false;
PublishCurrentState(); // Single publish
```

### ❌ Anti-Pattern 4: Key-Based Hashing for Content Distribution

**DON'T:**
```csharp
// BAD: Hashes by source key
if (_hashManager.HasContentChanged(mapping.ZonableKey, content))
{
    // Only first path gets uploaded!
}
```

**DO:**
```csharp
// GOOD: Hashes by destination path
if (_hashManager.HasContentChanged(mapping.Path, content))
{
    // Each path uploaded independently
}
```

### ❌ Anti-Pattern 5: Not Handling File Locking

**DON'T:**
```csharp
// BAD: Exclusive file access
using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
{
    // Conflicts with KDS writing to file!
}
```

**DO:**
```csharp
// GOOD: Shared file access
using (var fs = new FileStream(path, FileMode.Open,
                               FileAccess.Read, FileShare.ReadWrite))
{
    // KDS can continue writing
}
```

### ❌ Anti-Pattern 6: Crashing Service on External API Errors

**DON'T:**
```csharp
// BAD: Unhandled exception stops service
public void PublishSnapshot(OsbStateSnapshot snapshot)
{
    _osbClient.PublishSnapshot(snapshot); // Throws if API down!
}
```

**DO:**
```csharp
// GOOD: Isolate external failures
public void PublishSnapshot(OsbStateSnapshot snapshot)
{
    try
    {
        _osbClient.PublishSnapshot(snapshot);
    }
    catch (Exception ex)
    {
        _logger.LogError($"Publish failed: {ex.Message}");
        OnPublishError(snapshot.DeviceId, ex);
        // Service continues running
    }
}
```

### ❌ Anti-Pattern 7: Running on Multiple Workstations

**DON'T:**
```csharp
// BAD: Service runs on all workstations
OpsReadyEvent += (s, a) =>
{
    _backgroundService.Start(); // Duplicate posting!
    return EventProcessingInstruction.Continue;
};
```

**DO:**
```csharp
// GOOD: Only run on designated workstation
if(!DependencyManager.Resolve<IOpsContextClient>().IsKdsControllerHost())
    return; // Exit early on other workstations

OpsReadyEvent += (s, a) =>
{
    _backgroundService.Start(); // Only runs on KDS controller
    return EventProcessingInstruction.Continue;
};
```

---

## Quick Reference: Pattern Decision Matrix

| Requirement | Use Pattern |
|-------------|-------------|
| **Start/stop with Simphony** | Lifecycle Event Integration |
| **Monitor posting files** | File Monitoring with Position Tracking |
| **Multi-stage processing** | Event-Driven Processing Pipeline |
| **Avoid startup API flood** | Startup Mode Optimization |
| **Same content, multiple paths** | Path-Based Hash Tracking |
| **Post to external API** | Third-Party API Integration |
| **Periodic tasks** | Timer-Based Background Processing |
| **Device/item state tracking** | State Management with Auto-Expiration |

---

## Integration with Universal Patterns

All hosted application patterns build on universal mandatory patterns:

1. ✅ **11-Folder Directory Structure** - Add domain-specific folders (KdsPosting/, ConfigPosting/)
2. ✅ **ApplicationFactory Pattern** - Extends OpsExtensibilityApplication
3. ✅ **Custom DI Container** - Uses DependencyManager for all components
4. ✅ **Interface-First Design** - All clients abstracted (IOsbClient, IConfigurationClient)
5. ✅ **Logging Framework** - ILogManager + ILogger throughout
6. ✅ **Build Automation** - Standard PostBuildEvent deployment
7. ✅ **Stub Implementations** - Stub clients for DEBUG builds
8. ✅ **Exception Handling** - GetFirstException pattern
9. ✅ **Configuration Management** - Medium complexity hierarchical config
10. ✅ **Version Management** - VersionHelper
11. ✅ **Status Management** - StatusHelper
12. ✅ **Named Dependencies** - Multiple implementations (Debug vs Release)

**Additional patterns specific to hosted applications:**
13. **Startup Mode Optimization** - Prevent API flooding
14. **Path-Based Hash Tracking** - Content distribution
15. **File Position Monitoring** - Continuous file watching
16. **Lifecycle Event Integration** - Background service hosting
17. **Event-Driven Processing Pipeline** - Multi-stage processing

---

## Summary

The Hosted Background Service Pattern enables Simphony extensions to act as autonomous data bridges between Simphony and external systems. Unlike traditional POS integration, this pattern:

- Uses lifecycle events (OpsReady/OpsExit) instead of transaction events
- Consumes file-based data (posting files) instead of event parameters
- Runs continuously in background without user interaction
- Posts to external systems without blocking POS operations

**Key innovations:**
1. Position tracking for efficient file monitoring
2. Startup mode to prevent API flooding
3. Path-based hashing for content distribution
4. Event-driven pipeline for clean separation
5. State management with auto-expiration

**Use for:**
- Status boards and dashboards
- Data export and synchronization
- Third-party analytics integration
- Order tracking displays
- Any autonomous data bridging scenario

This pattern completes the comprehensive Simphony Extension Application development skill, covering all 6 architectural categories from simple direct integration to complex hosted background services.
