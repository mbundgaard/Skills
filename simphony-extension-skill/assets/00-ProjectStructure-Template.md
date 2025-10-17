# Simphony Extension Application - Project Structure Template

## Universal Mandatory 11-Folder Structure

**Validated across all 5 projects with 100% consistency**

```
YourExtensionName/
├── Clients/
│   ├── Configuration/
│   │   ├── SimphonyConfigurationClient.cs
│   │   └── FileConfigurationClient.cs (optional - for testing)
│   ├── Database/
│   │   └── DatabaseClient.cs
│   ├── OpsContext/
│   │   └── SimphonyOpsContextClient.cs
│   └── [Add domain-specific clients here]
│       ├── Email/
│       ├── ExternalApi/
│       └── etc.
│
├── Contracts/
│   ├── Clients/
│   │   ├── IConfigurationClient.cs
│   │   ├── IDatabaseClient.cs
│   │   ├── IOpsContextClient.cs
│   │   └── [Add domain-specific interfaces]
│   ├── Factories/
│   │   └── IDatabaseConnectionFactory.cs
│   ├── Logging/
│   │   ├── ILogManager.cs
│   │   └── ILogger.cs
│   ├── IScript.cs
│   ├── IEventHandler.cs (optional - if using event handler pattern)
│   └── IService.cs (optional - if using background services)
│
├── Dependency/
│   ├── DependencyManager.cs
│   ├── DependencyNameAttribute.cs
│   └── SimphonyDependencies.cs
│
├── Entities/
│   ├── Args/
│   │   ├── AbstractEventArgs.cs
│   │   └── [Event-specific args classes]
│   ├── Config.cs
│   ├── WorkstationInfo.cs
│   ├── Status.cs
│   └── [Add domain entities here]
│
├── EventHandlers/ (optional - if using event handler pattern)
│   └── [Event handler implementations]
│
├── Factories/
│   └── DatabaseConnections/
│       ├── SimphonyDbConnectionFactory.cs
│       └── SqlDbConnectionFactory.cs (optional)
│
├── Helpers/
│   ├── DatabaseService.cs
│   ├── ExceptionHelper.cs
│   ├── SimphonyDataHelper.cs
│   ├── SimphonyEventHelper.cs
│   ├── SimphonyOpsCommandArguments.cs
│   ├── SqlQuery.cs (if database operations needed)
│   └── VersionHelper.cs
│
├── Logging/
│   ├── Console/
│   │   └── ConsoleLogger.cs (or DebugLogger.cs)
│   ├── EGateway/
│   │   └── EGatewayLogger.cs
│   ├── FileLog/
│   │   └── FileLogger.cs
│   ├── Level.cs
│   ├── LogEntry.cs
│   └── LogManager.cs
│
├── Scripts/
│   ├── [Your script implementations]
│   │   ├── MyScript1.cs
│   │   ├── MyScript2.cs
│   │   └── Version.cs (recommended)
│   └── AbstractScript.cs (optional - if using auto-dispatch)
│
├── Serializers/
│   ├── JsonSerializer.cs
│   └── XmlSerializer.cs (optional)
│
├── Services/ (optional - if using background services)
│   └── [Background service implementations]
│
├── Properties/
│   └── AssemblyInfo.cs
│
├── SimphonyExtensibilityApplication.cs
└── YourExtensionName.csproj
```

## File Creation Checklist

### Phase 1: Core Infrastructure (Required)
- [ ] SimphonyExtensibilityApplication.cs
- [ ] Dependency/DependencyManager.cs
- [ ] Dependency/DependencyNameAttribute.cs
- [ ] Dependency/SimphonyDependencies.cs
- [ ] Entities/Status.cs
- [ ] YourExtensionName.csproj

### Phase 2: Logging Framework (Required)
- [ ] Contracts/Logging/ILogManager.cs
- [ ] Contracts/Logging/ILogger.cs
- [ ] Logging/LogManager.cs
- [ ] Logging/LogEntry.cs
- [ ] Logging/Level.cs
- [ ] Logging/Console/ConsoleLogger.cs
- [ ] Logging/FileLog/FileLogger.cs
- [ ] Logging/EGateway/EGatewayLogger.cs

### Phase 3: Helpers (Required)
- [ ] Helpers/ExceptionHelper.cs
- [ ] Helpers/SimphonyEventHelper.cs
- [ ] Helpers/SimphonyOpsCommandArguments.cs
- [ ] Helpers/VersionHelper.cs

### Phase 4: Clients & Contracts (Required)
- [ ] Contracts/Clients/IConfigurationClient.cs
- [ ] Contracts/Clients/IOpsContextClient.cs
- [ ] Clients/Configuration/SimphonyConfigurationClient.cs
- [ ] Clients/OpsContext/SimphonyOpsContextClient.cs

### Phase 5: Scripts (Required)
- [ ] Contracts/IScript.cs
- [ ] Scripts/Version.cs
- [ ] Scripts/[YourBusinessLogicScripts].cs

### Phase 6: Configuration (Required)
- [ ] Entities/Config.cs
- [ ] Entities/WorkstationInfo.cs

### Phase 7: Optional Advanced Features
- [ ] EventHandlers/ directory and IEventHandler pattern
- [ ] Services/ directory and IService pattern
- [ ] Scripts/AbstractScript.cs for auto-dispatch
- [ ] Stub implementations for testing
- [ ] Database clients if needed
- [ ] Additional serializers

## Recommended Creation Order

1. **Start with project file** - YourExtensionName.csproj
2. **Create directory structure** - All 11 folders
3. **Add core infrastructure** - DependencyManager, SimphonyExtensibilityApplication
4. **Add logging framework** - Complete logging infrastructure
5. **Add helpers** - ExceptionHelper, SimphonyEventHelper, etc.
6. **Add configuration** - Config.cs and related
7. **Add scripts** - Your business logic
8. **Register dependencies** - Complete SimphonyDependencies.cs
9. **Build and test** - Verify compilation and deployment

## Notes

- **Never deviate from this structure** - Validated across all 5 projects
- **Create all directories upfront** - Even if some are empty initially
- **Follow naming conventions exactly** - Case-sensitive, exact spelling
- **Optional folders** (EventHandlers, Services) can be added as needed
- **Domain-specific clients** go in subdirectories under Clients/
