// SimphonyDependencies.cs
// Universal Mandatory Pattern - Centralized Dependency Registration
// Location: Dependency/SimphonyDependencies.cs
// Purpose: Register all clients, scripts, services, loggers, etc.

using YourNamespace.Clients.Configuration;
using YourNamespace.Clients.Database;
using YourNamespace.Clients.OpsContext;
using YourNamespace.Contracts;
using YourNamespace.Contracts.Clients;
using YourNamespace.Contracts.Factories;
using YourNamespace.Contracts.Logging;
using YourNamespace.Entities;
using YourNamespace.Factories.DbConnections;
using YourNamespace.Logging;
using YourNamespace.Logging.Console; // or Logging.Debug
using YourNamespace.Logging.EGateway;
using YourNamespace.Logging.FileLog;
using YourNamespace.Scripts;
// Add additional using statements for your domain-specific classes

namespace YourNamespace.Dependency
{
    /// <summary>
    /// Centralized dependency installer for all Simphony Extension Application dependencies.
    /// Called once during application initialization.
    /// </summary>
    public class SimphonyDependencies : AbstractDependencyInstaller
    {
        private static bool _installed;

        public override void Install()
        {
            // Prevent duplicate installation (safety check)
            if (_installed) return;

            // ================================================================
            // CORE INFRASTRUCTURE (Required for all projects)
            // ================================================================

            // Status tracking (workstation ID, service host ID)
            DependencyManager.RegisterByInstance(new Status());

            // ================================================================
            // LOGGING FRAMEWORK (Required for all projects)
            // ================================================================

            // Log manager (singleton)
            DependencyManager.RegisterByType<ILogManager, LogManager>();

            // Multiple loggers (all will receive log entries)
            DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<ConsoleLogger>());
            // OR: DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<DebugLogger>());

            DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<FileLogger>());

            // Optional: EGateway logger (logs to Simphony event log)
            // DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<EGatewayLogger>());

            // ================================================================
            // CORE CLIENTS (Required for most projects)
            // ================================================================

            // Configuration client
            DependencyManager.RegisterByType<IConfigurationClient, SimphonyConfigurationClient>();

            // Optional: File-based configuration for testing
            // #if DEBUG
            //     DependencyManager.RegisterByType<IConfigurationClient, FileConfigurationClient>();
            // #else
            //     DependencyManager.RegisterByType<IConfigurationClient, SimphonyConfigurationClient>();
            // #endif

            // Simphony POS context client
            DependencyManager.RegisterByType<IOpsContextClient, SimphonyOpsContextClient>();

            // Optional: Stub for testing
            // #if DEBUG
            //     DependencyManager.RegisterByType<IOpsContextClient, StubOpsContextClient>();
            // #else
            //     DependencyManager.RegisterByType<IOpsContextClient, SimphonyOpsContextClient>();
            // #endif

            // ================================================================
            // DATABASE (If your extension uses database)
            // ================================================================

            // Database client
            // DependencyManager.RegisterByType<IDatabaseClient, DatabaseClient>();

            // Database connection factory
            // DependencyManager.RegisterByType<IDbConnectionFactory, SimphonyDbConnectionFactory>();
            // OR for SQL Server:
            // DependencyManager.RegisterByType<IDbConnectionFactory, SqlDbConnectionFactory>();

            // ================================================================
            // SERIALIZERS (If needed)
            // ================================================================

            // JSON serializer
            // DependencyManager.RegisterByType<ISerializer, JsonSerializer>("json");

            // XML serializer
            // DependencyManager.RegisterByType<ISerializer, XmlSerializer>("xml");

            // ================================================================
            // SCRIPTS (Register each script with name)
            // ================================================================

            // Version script (recommended for all projects)
            DependencyManager.RegisterByType<IScript, Version>(nameof(Version));

            // Register your business logic scripts here
            // Example for simple project (2-5 scripts):
            // DependencyManager.RegisterByType<IScript, MyScript1>(nameof(MyScript1));
            // DependencyManager.RegisterByType<IScript, MyScript2>(nameof(MyScript2));

            // Example for medium project (5-15 scripts):
            // DependencyManager.RegisterByType<IScript, ClockInOut>(nameof(ClockInOut));
            // DependencyManager.RegisterByType<IScript, Loyalty>(nameof(Loyalty));
            // DependencyManager.RegisterByType<IScript, Admin>(nameof(Admin));
            // DependencyManager.RegisterByType<IScript, Reports>(nameof(Reports));
            // ... more scripts

            // Example for complex project (15+ scripts):
            // Register in groups by domain
            // Check processing scripts:
            // DependencyManager.RegisterByType<IScript, BeginCheck>(nameof(BeginCheck));
            // DependencyManager.RegisterByType<IScript, ProcessCheck>(nameof(ProcessCheck));
            // ... more check scripts

            // Inventory scripts:
            // DependencyManager.RegisterByType<IScript, InventoryCount>(nameof(InventoryCount));
            // ... more inventory scripts

            // ================================================================
            // EVENT HANDLERS (Optional - if using IEventHandler pattern)
            // ================================================================

            // Example: Stored value void transaction handler
            // DependencyManager.RegisterByType<IEventHandler, StoredValueVoidTransaction>(nameof(StoredValueVoidTransaction));

            // ================================================================
            // BACKGROUND SERVICES (Optional - if using IService pattern)
            // ================================================================

            // Example: New orders service for loyalty integration
            // DependencyManager.RegisterByType<IService, NewOrdersService>(nameof(NewOrdersService));

            // ================================================================
            // NETWORK SERVICES (Optional - if using cross-workstation communication)
            // ================================================================

            // Example: Time-keeping network service
            // DependencyManager.RegisterByType<INetworkService, TimeKeepingNetworkService>(nameof(TimeKeepingNetworkService));

            // ================================================================
            // DOMAIN-SPECIFIC CLIENTS
            // ================================================================

            // Example: Email client
            // DependencyManager.RegisterByType<IEmailClient, SendGridEmailClient>();

            // Example: Loyalty client
            // DependencyManager.RegisterByType<ILoyaltyClient, LoyaltyClient>();
            // With stub for testing:
            // #if DEBUG
            //     DependencyManager.RegisterByType<ILoyaltyClient, StubLoyaltyClient>();
            // #else
            //     DependencyManager.RegisterByType<ILoyaltyClient, LoyaltyClient>();
            // #endif

            // Example: External API client
            // DependencyManager.RegisterByType<IExternalApiClient, ExternalApiClient>();

            // ================================================================
            // FACTORIES (If using factory pattern)
            // ================================================================

            // Example: Time-keeping factory for CAPS/non-CAPS selection
            // DependencyManager.RegisterByType<ITimeKeepingFactory, TimeKeepingFactory>();
            // DependencyManager.RegisterByType<ITimeKeeping, LocalTimeKeeping>("local");
            // DependencyManager.RegisterByType<ITimeKeeping, RemoteTimeKeeping>("remote");

            // ================================================================
            // INITIALIZATION (Optional - run once after registration)
            // ================================================================

            // Example: Create database support user
            // DependencyManager.Resolve<IDatabaseClient>().CreateSupportUser();

            // Example: Validate configuration
            // var config = DependencyManager.Resolve<IConfigurationClient>().ReadConfig();
            // if (config == null)
            //     throw new Exception("Configuration is required but not found");

            // Mark as installed
            _installed = true;
        }
    }
}

// ============================================================================
// REGISTRATION PATTERNS BY PROJECT SIZE
// ============================================================================

/*
 * SIMPLE PROJECT (2-5 scripts):
 *
 * public override void Install()
 * {
 *     // Core infrastructure
 *     DependencyManager.RegisterByInstance(new Status());
 *     DependencyManager.RegisterByType<ILogManager, LogManager>();
 *     DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<ConsoleLogger>());
 *     DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<FileLogger>());
 *
 *     // Clients
 *     DependencyManager.RegisterByType<IConfigurationClient, SimphonyConfigurationClient>();
 *     DependencyManager.RegisterByType<IOpsContextClient, SimphonyOpsContextClient>();
 *     DependencyManager.RegisterByType<IDatabaseClient, DatabaseClient>();
 *
 *     // Scripts
 *     DependencyManager.RegisterByType<IScript, DispensePosting>(nameof(DispensePosting));
 *     DependencyManager.RegisterByType<IScript, Version>(nameof(Version));
 *
 *     _installed = true;
 * }
 */

/*
 * MEDIUM PROJECT (5-15 scripts):
 *
 * public override void Install()
 * {
 *     // Core infrastructure
 *     DependencyManager.RegisterByInstance(new Status());
 *     DependencyManager.RegisterByType<ILogManager, LogManager>();
 *     DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<DebugLogger>());
 *     DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<FileLogger>());
 *
 *     // Clients
 *     DependencyManager.RegisterByType<IConfigurationClient, SimphonyConfigurationClient>();
 *     DependencyManager.RegisterByType<IOpsContextClient, SimphonyOpsContextClient>();
 *     DependencyManager.RegisterByType<IDatabaseClient, DatabaseClient>();
 *     DependencyManager.RegisterByType<ILoyaltyClient, LoyaltyClient>();
 *     DependencyManager.RegisterByType<IStoredValueClient, StoredValueClient>();
 *     DependencyManager.RegisterByType<IEmailClient, SendGridEmailClient>();
 *
 *     // Scripts
 *     DependencyManager.RegisterByType<IScript, Admin>(nameof(Admin));
 *     DependencyManager.RegisterByType<IScript, Loyalty>(nameof(Loyalty));
 *     DependencyManager.RegisterByType<IScript, StoredValue>(nameof(StoredValue));
 *     DependencyManager.RegisterByType<IScript, Version>(nameof(Version));
 *
 *     // Event handlers
 *     DependencyManager.RegisterByType<IEventHandler, StoredValueVoidTransaction>(nameof(StoredValueVoidTransaction));
 *
 *     // Background services
 *     DependencyManager.RegisterByType<IService, NewOrdersService>(nameof(NewOrdersService));
 *
 *     _installed = true;
 * }
 */

/*
 * COMPLEX PROJECT (15+ scripts):
 *
 * public override void Install()
 * {
 *     // Core infrastructure
 *     DependencyManager.RegisterByInstance(new Status());
 *     DependencyManager.RegisterByType<ILogManager, LogManager>();
 *     DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<ConsoleLogger>());
 *     DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<FileLogger>());
 *     DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<EGatewayLogger>());
 *
 *     // Clients
 *     DependencyManager.RegisterByType<IConfigurationClient, SimphonyConfigurationClient>();
 *     DependencyManager.RegisterByType<IOpsContextClient, SimphonyOpsContextClient>();
 *     DependencyManager.RegisterByType<IDatabaseClient, SimphonyDatabaseClient>();
 *
 *     // Factories
 *     DependencyManager.RegisterByType<IDbConnectionFactory, SimphonyDbConnectionFactory>();
 *
 *     // Serializers
 *     DependencyManager.RegisterByType<ISerializer, JsonSerializer>("json");
 *     DependencyManager.RegisterByType<ISerializer, XmlSerializer>("xml");
 *     DependencyManager.RegisterByType<ISerializer, Base64Serializer>("base64");
 *
 *     // Scripts (many - organized by domain)
 *     // Check processing:
 *     DependencyManager.RegisterByType<IScript, BeginCheck>(nameof(BeginCheck));
 *     DependencyManager.RegisterByType<IScript, ProcessCheck>(nameof(ProcessCheck));
 *     DependencyManager.RegisterByType<IScript, CloseCheck>(nameof(CloseCheck));
 *     // ... 20+ more scripts
 *
 *     _installed = true;
 * }
 */

// ============================================================================
// CONDITIONAL COMPILATION PATTERNS
// ============================================================================

/*
 * DEBUG vs RELEASE registration:
 *
 * #if DEBUG
 *     // Use stubs in debug mode for testing without Simphony
 *     DependencyManager.RegisterByType<IOpsContextClient, StubOpsContextClient>();
 *     DependencyManager.RegisterByType<ILoyaltyClient, StubLoyaltyClient>();
 * #else
 *     // Use real implementations in release
 *     DependencyManager.RegisterByType<IOpsContextClient, SimphonyOpsContextClient>();
 *     DependencyManager.RegisterByType<ILoyaltyClient, LoyaltyClient>();
 * #endif
 */

/*
 * CAPS detection for services:
 *
 * var isCaps = DependencyManager.Resolve<SimphonyCapsHelper>().IsCaps();
 * if (isCaps)
 * {
 *     // Register CAPS-only services
 *     DependencyManager.RegisterByType<IService, NewOrdersService>(nameof(NewOrdersService));
 * }
 */

// ============================================================================
// COMMON MISTAKES TO AVOID
// ============================================================================

/*
 * MISTAKE 1: Forgetting to register a script
 * SYMPTOM: "ScriptName is not a valid script name" exception
 * FIX: Add registration:
 * DependencyManager.RegisterByType<IScript, YourScript>(nameof(YourScript));
 *
 * MISTAKE 2: Wrong name in registration
 * SYMPTOM: Script not found or wrong script executes
 * FIX: Use nameof() for type safety:
 * DependencyManager.RegisterByType<IScript, MyScript>(nameof(MyScript));  // GOOD
 * DependencyManager.RegisterByType<IScript, MyScript>("MyScripts");       // BAD (typo)
 *
 * MISTAKE 3: Duplicate registrations
 * SYMPTOM: Unexpected behavior, wrong implementation used
 * FIX: Remove duplicate or use named registrations
 *
 * MISTAKE 4: Circular dependencies
 * SYMPTOM: Stack overflow during resolution
 * FIX: Refactor to break circular dependency
 *
 * MISTAKE 5: Missing _installed = true
 * SYMPTOM: Dependencies registered multiple times
 * FIX: Always set _installed = true at end of Install()
 */
