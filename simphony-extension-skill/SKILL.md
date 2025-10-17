---
name: Simphony Extension Application Development
description: Comprehensive patterns and templates for developing Oracle Simphony Extension Applications. Use when creating new Extension Applications, implementing POS integrations, following Simphony development best practices, or working with Oracle Micros Simphony platforms. Covers universal mandatory patterns, scale-appropriate designs, and domain-specific implementations across 6 architectural categories from simple posting to complex hosted applications.
---

# Simphony Extension Application Development

Comprehensive patterns and templates for Oracle Simphony Extension Application development, based on analysis of 6 production projects (721 C# files) with 99% confidence validation across diverse domains and complexity levels.

## Quick Start

Every Simphony Extension Application follows this exact workflow:

1. **Create universal 11-folder structure** - See `assets/00-ProjectStructure-Template.md`
2. **Copy ApplicationFactory template** - Use `assets/01-ApplicationFactory-Template.cs`
3. **Set up dependency injection** - Use `assets/02-DependencyManager-Template.cs`
4. **Choose scale-appropriate IScript variant** - Based on script count (see Pattern Selection below)
5. **Configure logging framework** - Use `assets/05-Logging-Framework-Template.cs`
6. **Register dependencies** - Complete `assets/03-SimphonyDependencies-Template.cs`

## Universal Mandatory Patterns

These 12 patterns are required in every Extension Application:

### 1. Exact Directory Structure
Create exactly these 11 folders:
```
YourExtension/
├── Clients/           - External system implementations
├── Contracts/         - Interface definitions
├── Dependency/        - Custom DI container
├── Entities/          - Domain models and DTOs
├── Factories/         - Factory implementations
├── Helpers/           - Utility classes
├── Logging/           - Multi-sink logging framework
├── Scripts/           - Business logic implementations
├── Serializers/       - JSON/XML serialization
├── Services/          - Background services (optional)
└── Properties/        - Assembly information
```

### 2. ApplicationFactory Pattern
Use exact pattern from `assets/01-ApplicationFactory-Template.cs`:
- Implements `IExtensibilityAssemblyFactory`
- Handles initialization sequence: DI → Status → Logger → Config → Events
- Robust exception handling with `ExceptionHelper.GetFirstException`

### 3. Custom DI Container
Use validated `DependencyManager` from `assets/02-DependencyManager-Template.cs`:
- Conditional registration (debug vs release)
- Named dependencies for multiple implementations
- Thread-safe singleton pattern

### 4. Interface-First Design
Abstract ALL external dependencies behind interfaces in `Contracts/` folder

### 5. Multi-Sink Logging Framework
Implement from `assets/05-Logging-Framework-Template.cs`:
- ConsoleLogger, FileLogger, EGatewayLogger
- Debug file control (`debug.txt` presence enables debug logging)
- Structured logging with LogEntry and Level classes

### 6. Automated Build Deployment
Configure PostBuildEvent in `assets/07-ProjectFile-Template.csproj` for automatic POS deployment

## Pattern Selection Guide

### IScript Interface Variants

Choose based on script complexity:

**Simple (2-5 scripts)**: 
```csharp
void Execute(string functionName, string argument)
```

**Medium (5-15 scripts)**:
```csharp
void Execute(string functionName, string argument)
// Use switch statements for routing
```

**Complex (15+ scripts)**:
```csharp
void Execute(string functionName, string argument)
void Event() // Additional event-driven methods
```

**Single-purpose**:
```csharp
void Execute(object argument)
```

See `assets/04-IScript-Variants-Template.cs` for complete implementations.

### Configuration Complexity

**Simple (2-10 settings)**: Flat POCO class
**Medium (10-50 settings)**: Hierarchical with nested classes  
**Complex (50+ settings)**: Multi-level hierarchy with collections

## Architectural Categories

### Traditional POS Integration
Event-driven transaction participation with real-time POS interaction.
- **Use for**: Transaction modifications, payment processing, real-time validations
- **Events**: CheckTotal, Tender, ServiceTotal
- **Pattern**: See `references/ops-patterns.md`

### Simple Posting Operations  
Minimal configuration direct posting to Simphony database.
- **Use for**: Loyalty points, discounts, simple item additions
- **Events**: Tender, ServiceTotal
- **Pattern**: See `references/dispense-patterns.md`

### Export & Data Processing
ETL pipelines and external system integration with batch processing.
- **Use for**: Daily sales exports, inventory synchronization, reporting
- **Events**: OpsReady, Tender (for data collection)
- **Pattern**: See `references/export-patterns.md`

### Time-Keeping & Compliance
Domain-specific business rules with specialized validation.
- **Use for**: Employee time tracking, labor compliance, scheduling
- **Events**: OpsReady, custom domain events
- **Pattern**: See `references/timekeeping-patterns.md`

### Loyalty & Customer Management
Background services with customer data integration.
- **Use for**: Loyalty programs, customer profiles, stored value cards
- **Events**: CheckOpen, Tender, ServiceTotal
- **Pattern**: See `references/loyalty-patterns.md`

### Hosted Background Services ⭐ NEW
Autonomous data bridging using Simphony as runtime host.
- **Use for**: File monitoring, external API synchronization, data bridging
- **Events**: OpsReady, OpsExit (lifecycle-based)
- **Pattern**: See `references/hosted-application-patterns.md`

## Development Workflow

### Starting New Extension

1. **Project Setup**
   ```bash
   # Create directory structure from template
   # Copy assets/07-ProjectFile-Template.csproj
   # Set .NET Framework 4.6.2, x86 platform
   ```

2. **Core Foundation**
   - Copy `assets/01-ApplicationFactory-Template.cs` → customize namespace
   - Copy `assets/02-DependencyManager-Template.cs` → no changes needed
   - Copy `assets/03-SimphonyDependencies-Template.cs` → add your registrations

3. **Scale-Appropriate Patterns**
   - **Ask**: How many scripts? → Choose IScript variant
   - **Ask**: How many events? → Choose event registration approach
   - **Ask**: Configuration complexity? → Choose config pattern

4. **Logging Setup**
   - Copy complete framework from `assets/05-Logging-Framework-Template.cs`
   - Create debug control file for development

### Adding Business Logic

1. **Define Script Interface** (chosen variant in `Contracts/IScript.cs`)
2. **Implement Script Class** (domain-specific naming in `Scripts/`)
3. **Register Dependencies** (add to SimphonyDependencies)
4. **Configure Events** (add to SimphonyEventHelper)

### External System Integration

1. **Design Interface** (`Contracts/Clients/IExternalClient.cs`)
2. **Implement Client** (`Clients/ExternalClient.cs`)
3. **Create Stub** (`Clients/StubExternalClient.cs` for testing)
4. **Register in DI** (conditional registration based on debug mode)

## Critical Anti-Patterns

**Never do these** - they break the validated patterns:

1. **Wrong exception handling**: Use `GetFirstException`, never `GetRootException`
2. **Missing stub implementations**: Every external dependency needs test stub
3. **Directory structure variations**: Never modify the 11-folder structure
4. **Missing interface abstractions**: All external dependencies must be behind interfaces
5. **Inconsistent logging**: Always use the multi-sink framework pattern

## Helper Templates

Use these proven helpers from `assets/06-Helper-Templates.cs`:

- **ExceptionHelper**: Exception unwrapping and formatting
- **SimphonyEventHelper**: Event registration and management
- **SimphonyOpsCommandArguments**: Command parsing for button clicks
- **VersionHelper**: Assembly version and naming utilities
- **DatabaseService**: Common database operations (if needed)

## Domain-Specific Guidance

**Time-Keeping Applications**: See `references/timekeeping-patterns.md`
- Employee clock in/out, labor compliance, scheduling integration
- Network services for CAPS/non-CAPS communication

**Loyalty & Stored Value**: See `references/loyalty-patterns.md`  
- Customer identification, points processing, stored value management
- Background services for customer data synchronization

**Export & Integration**: See `references/export-patterns.md`
- Batch data processing, ETL pipelines, external system synchronization
- File-based and API-based export patterns

**Operations Management**: See `references/ops-patterns.md`
- Complex multi-domain operations, advanced POS integration
- Administrative tools and operational dashboards

**Simple Dispensing**: See `references/dispense-patterns.md`
- Direct posting operations, minimal configuration requirements
- Item dispensing and simple transaction modifications

**Hosted Background Services**: See `references/hosted-application-patterns.md`
- Autonomous file monitoring, external API integration
- Data bridging without direct POS participation

## Bundled Resources

### assets/
Copy-paste ready templates and complete implementations:
- `00-ProjectStructure-Template.md` - Directory structure guide
- `01-ApplicationFactory-Template.cs` - Complete main entry point  
- `02-DependencyManager-Template.cs` - Custom DI container
- `03-SimphonyDependencies-Template.cs` - Dependency registration
- `04-IScript-Variants-Template.cs` - All IScript implementations
- `05-Logging-Framework-Template.cs` - Complete logging system
- `06-Helper-Templates.cs` - Utility classes
- `07-ProjectFile-Template.csproj` - Project configuration
- `08-Scale-Appropriate-Templates.md` - Pattern selection guide

### references/  
Domain-specific implementation patterns:
- `timekeeping-patterns.md` - Time-keeping and compliance patterns
- `loyalty-patterns.md` - Customer management and stored value
- `export-patterns.md` - Data processing and ETL patterns
- `ops-patterns.md` - Complex operational integration
- `dispense-patterns.md` - Simple posting operations
- `hosted-application-patterns.md` - Autonomous background services

## Instructions for Claude

When helping with Simphony Extension Application development:

1. **Always start with universal mandatory patterns** - these are non-negotiable
2. **Ask about scale and domain** to recommend appropriate patterns:
   - Script count → IScript variant
   - Event count → Event registration approach  
   - Domain type → Reference appropriate patterns guide
3. **Use templates as starting points** - copy from assets/, don't recreate
4. **Create stub implementations** for all external dependencies
5. **Follow exact directory structure** - no variations allowed
6. **Reference domain patterns** when applicable from references/
7. **Maintain .NET Framework 4.6.2 compatibility** with modern C# practices

This skill represents validated patterns from 6 production projects across all architectural categories. Always prefer these proven patterns over custom implementations.
