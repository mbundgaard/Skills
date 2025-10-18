# Time-Keeping Domain Patterns

Reference guide for implementing time-keeping and employee labor tracking functionality in Simphony Extension Applications.

**Source:** MunerisTimeKeeping project analysis (80 files, 6 scripts, medium complexity)

---

## 1. CAPS/Non-CAPS Workstation Strategy Pattern

### Problem
Restaurant environments have workstations with different capabilities:
- **CAPS workstations:** Have database access, can perform operations directly
- **Non-CAPS workstations:** No database access, need to route operations to CAPS workstation

Time-keeping operations require database access, but must work from any workstation.

### Solution: Strategy Pattern with Factory Selection

**Core abstraction:**
```csharp
public interface ITimeKeeping
{
    SignInResult PerformSignIn(WorkstationInfo workstationInfo, long employeeId);
    ClockInOutStatus GetClockInOutStatus(WorkstationInfo workstationInfo, long idNumber);
    ClockInOutResult PerformClockInOut(WorkstationInfo workstationInfo, long employeeId,
        string departmentKey, string comment, long managerId, DateTime? clockInOutOverride);
    OpenClockInOut[] GetOpenClockInOuts();
    string PrintDailyTimeReport(int offset, long employeeId, int guestCheckPrinterNumber);
    string DeleteAllTimeEntries(WorkstationInfo workstationInfo);
    TimeEntrySummary[] GetTimeEntrySummaries();
}
```

**Two implementations:**

1. **LocalTimeKeeping** - For CAPS workstations with direct database access
2. **TimeKeepingNetworkClient** - For non-CAPS workstations, routes to CAPS via network

**Factory selection based on workstation capability:**
```csharp
public class TimeKeepingFactory : ITimeKeepingFactory
{
    private readonly IOpsContextClient _opsContextClient;

    public TimeKeepingFactory(IOpsContextClient opsContextClient)
    {
        _opsContextClient = opsContextClient;
    }

    public ITimeKeeping CreateTimeKeeping()
    {
        // Use CAPS detection to select implementation
        return _opsContextClient.IsCaps()
            ? DependencyManager.Resolve<ITimeKeeping>("local")
            : DependencyManager.Resolve<ITimeKeeping>("remote");
    }
}
```

**Dependency registration:**
```csharp
// In SimphonyDependencies.Install():
DependencyManager.RegisterByType<ITimeKeepingFactory, TimeKeepingFactory>();
DependencyManager.RegisterByType<ITimeKeeping, LocalTimeKeeping>("local");
DependencyManager.RegisterByType<ITimeKeeping, TimeKeepingNetworkClient>("remote");
```

**Script usage:**
```csharp
public class ClockInOut : AbstractScript
{
    private readonly ITimeKeepingFactory _timeKeepingFactory;

    public ClockInOut(ITimeKeepingFactory timeKeepingFactory)
    {
        _timeKeepingFactory = timeKeepingFactory;
    }

    public ClockInOutStatus GetStatus()
    {
        // Factory automatically selects correct implementation
        var timeKeeping = _timeKeepingFactory.CreateTimeKeeping();
        return timeKeeping.GetClockInOutStatus(WorkstationInfo.Current(), employeeId);
    }
}
```

**Benefits:**
- Transparent operation from any workstation
- Single codebase for both CAPS and non-CAPS
- Centralized time-keeping database
- Easy testing with mock implementations

**When to use:**
- Distributed POS environments with mixed workstation capabilities
- Operations requiring centralized database access
- Multi-location scenarios

---

## 2. Business Date Calculation Pattern

### Problem
Hospitality industry uses "business dates" that don't align with calendar dates. A business day typically runs from 4 AM to 4 AM (next day), not midnight to midnight. This ensures overnight shifts are counted in the same business day.

### Solution: Configurable End-of-Day with Business Date Helper

**Configuration:**
```csharp
public class Config
{
    public int EndOfDayHour { get; set; }  // e.g., 4 for 4 AM
    public int EndOfDayMin { get; set; }   // e.g., 0 for :00
}
```

**Business date calculation:**
```csharp
// Get current business date
var endOfDay = new TimeSpan(_config.EndOfDayHour, _config.EndOfDayMin, 0);
var businessDate = SimphonyDataHelper.GetBusinessDate(DateTime.UtcNow, endOfDay, true);

// Calculate business date start (e.g., today at 4 AM UTC)
var businessDateStart = businessDate.Add(endOfDay).ToUniversalTime();

// Calculate previous business day (for reports)
var previousBusinessDate = businessDate.Add(endOfDay).AddDays(-1).ToUniversalTime();
```

**Time entry retrieval by business date:**
```csharp
public string PrintDailyTimeReport(int offset, long employeeId, int guestCheckPrinterNumber)
{
    var endOfDay = new TimeSpan(_config.EndOfDayHour, _config.EndOfDayMin, 0);
    var businessDate = SimphonyDataHelper.GetBusinessDate(DateTime.UtcNow, endOfDay, true);

    // Apply offset (0 = today, -1 = yesterday, etc.)
    var businessDateStart = businessDate.Add(endOfDay).AddDays(offset * -1).ToUniversalTime();

    // Get all time entries for this business date
    var allTimeEntries = _databaseClient.GetTimeEntriesByClockIn(
        businessDateStart,
        businessDateStart.AddDays(1)
    ).ToList();

    // Process time entries...
}
```

**Benefits:**
- Correctly groups overnight shifts in same business day
- Configurable end-of-day time per property
- Standard hospitality industry practice
- Accurate labor cost reporting

**When to use:**
- Any time-keeping or labor tracking implementation
- Shift reporting that spans midnight
- Multi-day event analysis
- Labor cost calculations

**Important notes:**
- Always use UTC for database storage (DateTime.UtcNow)
- Convert to local time only for display
- End-of-day configuration should be property-specific in multi-location scenarios

---

## 3. Clock-In/Out Override and Authorization Pattern

### Problem
Employees sometimes forget to clock out. Managers need to retroactively correct clock-out times with:
1. Ability to set past time (not current time)
2. Audit trail showing who authorized the override
3. Handling overnight shifts (clock-out time before clock-in time means next day)

### Solution: Manager Override with Audit Trail

**Clock-in/out implementation with override support:**
```csharp
public ClockInOutResult PerformClockInOut(
    WorkstationInfo workstationInfo,
    long employeeId,
    string departmentKey,
    string comment,
    long managerId,           // Manager authorizing override (0 if none)
    DateTime? clockInOutOverride)  // Override time (null for current time)
{
    var employee = _databaseClient.GetEmployeeById(employeeId);
    var entry = _databaseClient.GetOpenTimeEntries()
        .FirstOrDefault(x => x.EmployeeId == employeeId);

    if (entry == null)
    {
        // CLOCK IN
        _databaseClient.AddUpdateTimeEntry(new TimeEntry
        {
            EmployeeId = employeeId,
            DepartmentKey = departmentKey,
            ClockInTime = clockInOutOverride ?? DateTime.UtcNow,
            ClockInWorkstation = workstationInfo.Hostname,
            ClockInAppVersion = workstationInfo.AppVersion,
            Comment = comment
        });
    }
    else
    {
        // CLOCK OUT

        // Handle override time for overnight shifts
        if (entry != null && clockInOutOverride.HasValue)
        {
            // If override time is earlier than clock-in time, assume next day
            var c = clockInOutOverride.Value.TimeOfDay > entry.ClockInTime.TimeOfDay
                ? entry.ClockInTime.Date      // Same day
                : entry.ClockInTime.Date.AddDays(1);  // Next day

            clockInOutOverride = c.Add(clockInOutOverride.Value.TimeOfDay);
        }

        var clockInOutTime = clockInOutOverride ?? DateTime.UtcNow;

        // Update time entry
        entry.ClockOutTime = clockInOutTime;
        entry.ClockOutWorkstation = workstationInfo.Hostname;
        entry.ClockOutAppVersion = workstationInfo.AppVersion;

        // Record manager authorization if provided
        if (managerId != 0)
        {
            entry.ClockOutManagerId = managerId;
            entry.ClockOutManagerTime = DateTime.UtcNow;
        }

        _databaseClient.AddUpdateTimeEntry(entry);
    }

    return new ClockInOutResult { /* ... */ };
}
```

**Database schema for audit trail:**
```csharp
public class TimeEntry
{
    // Clock-in information
    public DateTime ClockInTime { get; set; }
    public string ClockInWorkstation { get; set; }
    public string ClockInAppVersion { get; set; }

    // Clock-out information
    public DateTime? ClockOutTime { get; set; }
    public string ClockOutWorkstation { get; set; }
    public string ClockOutAppVersion { get; set; }

    // Manager override audit trail
    public long? ClockOutManagerId { get; set; }
    public DateTime? ClockOutManagerTime { get; set; }

    // Other fields...
}
```

**Benefits:**
- Full audit trail for compliance
- Handles overnight shifts correctly
- Manager accountability
- Workstation and version tracking

**When to use:**
- Any clock-in/out implementation
- Labor law compliance requirements
- Payroll accuracy needs
- Dispute resolution scenarios

---

## 4. Department-Based Time Tracking Pattern

### Problem
Employees may work in multiple departments during their shift. Time tracking needs to record which department they're working in, and support:
- Multi-property configurations with different departments per property
- Department selection only when clocking in (not out)
- Department-based reporting and totals

### Solution: Property-Specific Department Configuration

**Configuration structure:**
```csharp
public class Config
{
    public PropertyConfig[] PropertyConfigs { get; set; }
}

public class PropertyConfig
{
    public int PropertyNumber { get; set; }
    public Department[] Departments { get; set; }
}

public class Department
{
    public string Key { get; set; }
    public string Name { get; set; }
}
```

**Configuration example (JSON):**
```json
{
  "PropertyConfigs": [
    {
      "PropertyNumber": 1,
      "Departments": [
        { "Key": "KITCHEN", "Name": "Kitchen" },
        { "Key": "FOH", "Name": "Front of House" },
        { "Key": "BAR", "Name": "Bar" }
      ]
    },
    {
      "PropertyNumber": 2,
      "Departments": [
        { "Key": "KITCHEN", "Name": "Kitchen" },
        { "Key": "DELIVERY", "Name": "Delivery" }
      ]
    }
  ]
}
```

**Department retrieval with caching:**
```csharp
private Department[] _departments;

private IEnumerable<Department> GetDepartments()
{
    // Cache departments to avoid repeated config lookups
    if (_departments?.Any() ?? false)
        return _departments;

    var propertyNumber = _opsContextClient.GetPropertyNumber();
    _departments = _config.PropertyConfigs
        .FirstOrDefault(x => x.PropertyNumber == propertyNumber)
        ?.Departments ?? Array.Empty<Department>();

    return _departments;
}
```

**Clock-in/out status with department list:**
```csharp
public ClockInOutStatus GetClockInOutStatus(WorkstationInfo workstationInfo, long idNumber)
{
    var employee = _databaseClient.GetEmployeeById(idNumber);
    var departments = GetDepartments().ToList();

    if (!departments.Any())
        return new ClockInOutStatus
        {
            AbortOperation = true,
            ErrorMessage = _config.MsgDepartmentNotFound
        };

    var entry = _databaseClient.GetOpenTimeEntries()
        .FirstOrDefault(x => x.EmployeeId == employee.EmployeeId);

    return new ClockInOutStatus
    {
        EmployeeId = employee.EmployeeId,

        // Show department list ONLY when clocking in (entry == null)
        // When clocking out, department is already recorded
        DepartmentList = entry == null ? departments : new List<Department>(),

        ClockInOutConfirmationPrompt = entry != null
            ? _config.MsgClockOutConfirmation?.Replace("{FirstName}", employee.FirstName)
            : _config.MsgClockInConfirmation?.Replace("{FirstName}", employee.FirstName)
    };
}
```

**Department-based reporting:**
```csharp
var timeEntriesByDepartment = allTimeEntries.GroupBy(x => x.DepartmentKey);

var dailyTotal = new TimeSpan();
foreach (var departmentGroup in timeEntriesByDepartment)
{
    var departmentTotal = new TimeSpan();
    var department = propertyConfig.Departments
        .FirstOrDefault(x => x.Key == departmentGroup.Key);

    foreach (var timeEntry in departmentGroup)
    {
        var duration = (timeEntry.ClockOutTime ?? DateTime.UtcNow)
            .Subtract(timeEntry.ClockInTime);
        departmentTotal += duration;
    }

    // Report department total
    dailyTotal += departmentTotal;
}
```

**Benefits:**
- Multi-property support with different departments
- Efficient caching of department configuration
- Department-based labor cost reporting
- Flexible configuration without code changes

**When to use:**
- Multi-location or multi-concept restaurants
- Department-based labor cost analysis
- Cross-training scenarios where employees work multiple departments

---

## 5. Open Entry Management Pattern

### Problem
Managers need real-time visibility into:
- Which employees are currently clocked in
- How long they've been working
- Which department they're working in
- Ability to monitor labor hours in real-time

### Solution: Open Time Entry Queries

**Open entry retrieval:**
```csharp
public OpenClockInOut[] GetOpenClockInOuts()
{
    var openTimeEntries = _databaseClient.GetOpenTimeEntries()
        .OrderBy(x => x.ClockInTime)
        .ToList();

    var employees = _databaseClient.GetEmployees();
    var departments = GetDepartments().ToList();

    return openTimeEntries.Select(timeEntry =>
    {
        var employee = employees.FirstOrDefault(x => x.EmployeeId == timeEntry.EmployeeId);
        var department = departments.FirstOrDefault(x => x.Key == timeEntry.DepartmentKey);

        return new OpenClockInOut
        {
            EmployeeId = timeEntry.EmployeeId,
            EmployeeName = $"{employee?.FirstName} {employee?.LastName}",
            DepartmentName = department?.Name,
            ClockInTime = timeEntry.ClockInTime,
            Duration = DateTime.UtcNow.Subtract(timeEntry.ClockInTime)
        };
    }).ToArray();
}
```

**Display formatting:**
```csharp
public class OpenClockInOut
{
    public long EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string DepartmentName { get; set; }
    public DateTime ClockInTime { get; set; }
    public TimeSpan Duration { get; set; }

    public override string ToString()
    {
        return $"{EmployeeName} - {DepartmentName} - {Duration:hh\\:mm}";
    }
}
```

**Script implementation:**
```csharp
public class EmployeesClockedIn : AbstractScript
{
    private readonly ITimeKeepingFactory _timeKeepingFactory;
    private readonly IOpsContextClient _opsContextClient;

    public void ShowClockedInEmployees()
    {
        var timeKeeping = _timeKeepingFactory.CreateTimeKeeping();
        var openEntries = timeKeeping.GetOpenClockInOuts();

        if (!openEntries.Any())
        {
            _opsContextClient.ShowMessage("No employees currently clocked in");
            return;
        }

        var message = string.Join("\n", openEntries.Select(x => x.ToString()));
        _opsContextClient.ShowMessage($"Employees Clocked In:\n\n{message}");
    }
}
```

**Benefits:**
- Real-time labor monitoring
- Easy identification of long shifts
- Department staffing visibility
- Formatted display for POS screens

**When to use:**
- Manager dashboards
- Labor law compliance (overtime alerts)
- Shift handoff scenarios
- Real-time labor cost monitoring

---

## 6. Receipt Printer Integration Pattern

### Problem
Time reports need to be printed on kitchen receipt printers with:
- Fixed width (typically 40 characters)
- Proper alignment (left, right, center)
- Totals and subtotals
- Clean formatting

### Solution: StringHelper Utilities for Receipt Formatting

**StringHelper implementation:**
```csharp
public static class StringHelper
{
    public static string AlignCenter(string text, int width)
    {
        if (text.Length >= width)
            return text.Substring(0, width);

        int padding = (width - text.Length) / 2;
        return text.PadLeft(text.Length + padding).PadRight(width);
    }

    public static string StraightMargin(string left, string right, int width)
    {
        if (left.Length + right.Length >= width)
            return (left + right).Substring(0, width);

        int spaces = width - left.Length - right.Length;
        return left + new string(' ', spaces) + right;
    }

    public static string MakeLine(int width, char character = '-')
    {
        return new string(character, width);
    }
}
```

**Daily time report with receipt formatting:**
```csharp
public string PrintDailyTimeReport(int offset, long employeeId, int guestCheckPrinterNumber)
{
    var endOfDay = new TimeSpan(_config.EndOfDayHour, _config.EndOfDayMin, 0);
    var businessDate = SimphonyDataHelper.GetBusinessDate(DateTime.UtcNow, endOfDay, true);
    var businessDateStart = businessDate.Add(endOfDay).AddDays(offset * -1).ToUniversalTime();

    var allTimeEntries = _databaseClient.GetTimeEntriesByClockIn(
        businessDateStart,
        businessDateStart.AddDays(1)
    ).ToList();

    var timeEntriesByDepartment = allTimeEntries.GroupBy(x => x.DepartmentKey);
    var employees = _databaseClient.GetEmployees().ToList();
    var propertyConfig = _config.PropertyConfigs
        .FirstOrDefault(x => x.PropertyNumber == _opsContextClient.GetPropertyNumber());

    const int width = 40;
    var lines = new List<string>();

    // Header
    lines.Add(StringHelper.AlignCenter("Daily Clock in/out report", width));
    lines.Add(StringHelper.AlignCenter(businessDate.ToString("yyyy-MM-dd"), width));
    lines.Add(StringHelper.MakeLine(width));

    var dailyTotal = new TimeSpan();

    foreach (var departmentGroup in timeEntriesByDepartment)
    {
        var departmentTotal = new TimeSpan();
        var department = propertyConfig.Departments
            .FirstOrDefault(x => x.Key == departmentGroup.Key);

        // Department header
        lines.Add(StringHelper.AlignCenter(department.Name, width));
        lines.Add(StringHelper.MakeLine(width, '-'));

        foreach (var timeEntry in departmentGroup)
        {
            var employee = employees.FirstOrDefault(x => x.EmployeeId == timeEntry.EmployeeId);
            var duration = (timeEntry.ClockOutTime ?? DateTime.UtcNow)
                .Subtract(timeEntry.ClockInTime);
            departmentTotal += duration;

            // Employee line: "John Smith            8h 30m"
            lines.Add(StringHelper.StraightMargin(
                $"{employee.FirstName} {employee.LastName}",
                $"{duration:hh}h {duration:mm}m",
                width
            ));
        }

        // Department subtotal
        lines.Add(StringHelper.MakeLine(width, '-'));
        lines.Add(StringHelper.StraightMargin(
            "Department Total:",
            $"{departmentTotal:hh}h {departmentTotal:mm}m",
            width
        ));
        lines.Add("");  // Blank line between departments

        dailyTotal += departmentTotal;
    }

    // Daily total
    lines.Add(StringHelper.MakeLine(width, '='));
    lines.Add(StringHelper.StraightMargin(
        "Daily Total:",
        $"{dailyTotal:hh}h {dailyTotal:mm}m",
        width
    ));

    // Print to receipt printer
    _opsContextClient.Print(guestCheckPrinterNumber, lines.ToArray());

    return "OK";
}
```

**Example output:**
```
    Daily Clock in/out report
         2025-01-15
========================================
           Kitchen
----------------------------------------
John Smith                    8h 30m
Jane Doe                      7h 15m
----------------------------------------
Department Total:            15h 45m

         Front of House
----------------------------------------
Mike Johnson                  9h 00m
Sarah Williams                8h 30m
----------------------------------------
Department Total:            17h 30m

========================================
Daily Total:                 33h 15m
```

**Benefits:**
- Consistent formatting across all reports
- Works with standard receipt printer width
- Clear visual hierarchy with separators
- Easy to read on thermal paper

**When to use:**
- Any receipt printer output
- Time reports, sales reports, shift summaries
- Kitchen printer notifications
- Fixed-width display requirements

---

## 7. Network Service Architecture for Distributed Operations

### Problem
Time-keeping operations require centralized database access, but:
- Non-CAPS workstations cannot access database directly
- Network calls need to be transparent to business logic
- Multiple operations need to be supported over network
- Type-safe method invocation across workstations

### Solution: Network Service with Method Call Serialization

**Network service base class:**
```csharp
public abstract class AbstractNetworkService : INetworkService
{
    private readonly ISerializer _serializer;

    protected AbstractNetworkService()
    {
        _serializer = DependencyManager.Resolve<ISerializer>("json");
    }

    public abstract string Name { get; }

    public string ProcessMessage(string data)
    {
        var message = _serializer.Deserialize<MethodCallNetworkMessage>(data);
        var result = ProcessMessage(message);
        return _serializer.Serialize(result);
    }

    protected abstract object ProcessMessage(MethodCallNetworkMessage message);
}
```

**TimeKeeping network service:**
```csharp
public class TimeKeepingNetworkService : AbstractNetworkService
{
    private readonly ITimeKeeping _timeKeeping;

    public TimeKeepingNetworkService([DependencyName("local")] ITimeKeeping timeKeeping)
    {
        _timeKeeping = timeKeeping;
    }

    public override string Name => nameof(TimeKeepingNetworkService);

    protected override object ProcessMessage(MethodCallNetworkMessage message)
    {
        // Use reflection to invoke method on local TimeKeeping instance
        return message.ExecuteMethod(_timeKeeping);
    }
}
```

**TimeKeeping network client (for non-CAPS workstations):**
```csharp
public class TimeKeepingNetworkClient : ITimeKeeping
{
    private readonly INetworkServiceClient _networkServiceClient;

    public TimeKeepingNetworkClient(INetworkServiceClient networkServiceClient)
    {
        _networkServiceClient = networkServiceClient;
    }

    public ClockInOutStatus GetClockInOutStatus(WorkstationInfo workstationInfo, long idNumber)
    {
        // Create method call message
        var message = new MethodCallNetworkMessage
        {
            MethodName = nameof(GetClockInOutStatus),
            Parameters = new object[] { workstationInfo, idNumber }
        };

        // Send to CAPS workstation
        return _networkServiceClient.SendMessage<ClockInOutStatus>(
            nameof(TimeKeepingNetworkService),
            message
        );
    }

    // Implement other ITimeKeeping methods the same way...
}
```

**Extension Application Service registration:**
```csharp
public class SimphonyExtensionApplicationService : ExtensionApplicationService
{
    private readonly List<INetworkService> _networkServices;

    public SimphonyExtensionApplicationService()
    {
        DependencyManager.Install<SimphonyDependencies>();
        _networkServices = DependencyManager.ResolveAll<INetworkService>().ToList();
    }

    public override ApplicationResponse ProcessMessage(NetworkMessage message)
    {
        var service = _networkServices.FirstOrDefault(x => x.Name == message.Command);

        if (service == null)
            return new ApplicationResponse
            {
                ErrorText = $"No service found with name {message.Command}"
            };

        try
        {
            var response = service.ProcessMessage(message.DataAsUnicode);
            return new ApplicationResponse
            {
                Success = true,
                DataAsUnicode = response
            };
        }
        catch (Exception e)
        {
            return new ApplicationResponse
            {
                ErrorText = ExceptionHelper.GetFirstException(e).Message
            };
        }
    }
}
```

**Dependency registration:**
```csharp
// In SimphonyDependencies.Install():

// Register network service (runs on CAPS workstation)
DependencyManager.RegisterByInstance<INetworkService>(
    DependencyManager.Resolve<TimeKeepingNetworkService>()
);

// Register local and remote TimeKeeping implementations
DependencyManager.RegisterByType<ITimeKeeping, LocalTimeKeeping>("local");
DependencyManager.RegisterByType<ITimeKeeping, TimeKeepingNetworkClient>("remote");

// Register factory
DependencyManager.RegisterByType<ITimeKeepingFactory, TimeKeepingFactory>();
```

**Benefits:**
- Transparent network communication
- Type-safe method calls
- Centralized database access
- Extensible to other operations

**When to use:**
- Mixed CAPS/non-CAPS workstation environments
- Centralized data operations
- Cross-workstation communication needs
- Master-slave database architectures

---

## Summary

### When to Use These Patterns

**Use these time-keeping patterns when building:**
- Employee labor tracking systems
- Clock-in/clock-out functionality
- Time and attendance reporting
- Department-based labor cost analysis
- Shift management systems
- Payroll integration systems

### Pattern Combination Recommendations

**Basic Time-Keeping Implementation:**
1. CAPS/Non-CAPS Strategy Pattern (mandatory for distributed environments)
2. Business Date Calculation Pattern (mandatory for hospitality)
3. Clock-In/Out Override Pattern (recommended for compliance)

**Advanced Time-Keeping Implementation:**
Add department tracking, open entry management, receipt printing, and network services as needed.

### Integration with Universal Patterns

These domain patterns build on universal Simphony Extension Application patterns:
- Use **Interface-First Design** for ITimeKeeping abstraction
- Use **Custom DI Container** for strategy selection and named dependencies
- Use **Logging Framework** for audit trail and debugging
- Use **Configuration Management** for end-of-day times and department mappings
- Use **AbstractScript** for single-purpose time-keeping scripts

### Key Takeaways

1. **CAPS/Non-CAPS awareness is critical** - Always use strategy pattern for distributed environments
2. **Business dates ≠ calendar dates** - Hospitality industry requires special date handling
3. **Audit trail is essential** - Track who, when, where for every time entry
4. **Department tracking adds value** - Enables labor cost analysis by department
5. **Receipt printers are standard output** - Fixed-width formatting utilities are essential
6. **Network services enable distribution** - Transparent cross-workstation operations
