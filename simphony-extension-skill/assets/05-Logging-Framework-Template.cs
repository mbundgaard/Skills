// Logging Framework - Complete Implementation
// Universal Mandatory Pattern - 100% consistent across all 5 projects
// Components: ILogManager, ILogger, LogManager, LogEntry, Level, Concrete Loggers

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using YourNamespace.Dependency;

namespace YourNamespace.Logging
{
    // ========================================================================
    // LEVEL ENUM
    // ========================================================================
    // Location: Logging/Level.cs

    /// <summary>
    /// Log severity levels.
    /// </summary>
    public enum Level
    {
        Debug,      // Detailed diagnostic information
        Info,       // Informational messages
        Warn,       // Warning messages
        Error,      // Error messages
        Exception   // Exception with stack trace
    }

    // ========================================================================
    // LOG ENTRY
    // ========================================================================
    // Location: Logging/LogEntry.cs

    /// <summary>
    /// Represents a single log entry with all metadata.
    /// </summary>
    public class LogEntry
    {
        public Level Level { get; set; }
        public string Message { get; set; }
        public string[] Data { get; set; }
        public Exception Exception { get; set; }
        public DateTime LocalDateTime { get; set; }
    }
}

namespace YourNamespace.Contracts.Logging
{
    // ========================================================================
    // ILOGGER INTERFACE
    // ========================================================================
    // Location: Contracts/Logging/ILogger.cs

    /// <summary>
    /// Interface for concrete logger implementations.
    /// Multiple loggers can be registered to receive all log entries.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Log an entry.
        /// Implementation should handle errors gracefully (don't throw).
        /// </summary>
        void Log(YourNamespace.Logging.LogEntry logEntry);
    }

    // ========================================================================
    // ILOGMANAGER INTERFACE
    // ========================================================================
    // Location: Contracts/Logging/ILogManager.cs

    /// <summary>
    /// Main logging interface used by application code.
    /// Manages log history and dispatches to all registered loggers.
    /// </summary>
    public interface ILogManager
    {
        void LogDebug(string message, params string[] data);
        void LogInfo(string message, params string[] data);
        void LogWarn(string message, params string[] data);
        void LogError(string message, params string[] data);
        void LogException(string message, Exception exception);

        /// <summary>
        /// Recent log history (last 100 entries).
        /// </summary>
        IEnumerable<YourNamespace.Logging.LogEntry> History { get; }
    }
}

namespace YourNamespace.Logging
{
    // ========================================================================
    // LOG MANAGER - CORE IMPLEMENTATION
    // ========================================================================
    // Location: Logging/LogManager.cs

    using YourNamespace.Contracts.Logging;
    using YourNamespace.Dependency;

    /// <summary>
    /// Main logging manager.
    /// Maintains log history and dispatches to all registered ILogger implementations.
    /// </summary>
    public class LogManager : ILogManager
    {
        private static readonly Queue<LogEntry> LogHistory;
        private static readonly int MaxLogHistoryLength;

        static LogManager()
        {
            LogHistory = new Queue<LogEntry>();
            MaxLogHistoryLength = 100;
        }

        private static void Log(LogEntry logEntry)
        {
            // ==============================================================
            // DEBUG CONTROL - Choose ONE approach
            // ==============================================================

            // OPTION 1: No control (always log) - Original pattern (Project 1)
            // No code needed - logs everything

            // OPTION 2: File-based control (Production flexibility) - Project 2, 4
            // RECOMMENDED for production deployments
            if (logEntry.Level == Level.Debug &&
                !Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "debug*").Any())
                return;

            // OPTION 3: Compiler directive (Performance) - Project 3
            // Uncomment for compile-time debug control
            //#if !DEBUG
            //    if (logEntry.Level == Level.Debug) return;
            //#endif

            // OPTION 4: Hybrid (Both file-based AND compiler directive)
            // Best of both worlds
            //#if !DEBUG
            //    if (logEntry.Level == Level.Debug &&
            //        !Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "debug*").Any())
            //        return;
            //#endif

            // ==============================================================
            // Log entry processing
            // ==============================================================

            logEntry.LocalDateTime = DateTime.Now.ToLocalTime();

            LogHistory.Enqueue(logEntry);
            if (LogHistory.Count > MaxLogHistoryLength)
                LogHistory.Dequeue();

            var loggers = DependencyManager.ResolveAll<ILogger>().ToList();

            foreach (var logger in loggers)
            {
                try
                {
                    logger.Log(logEntry);
                }
                catch
                {
                    // Silently fail - don't let logging break the application
                    // Never throw from logging code
                }
            }
        }

        // Expression-bodied convenience methods
        public void LogDebug(string message, params string[] data) =>
            Log(new LogEntry { Level = Level.Debug, Message = message, Data = data });

        public void LogInfo(string message, params string[] data) =>
            Log(new LogEntry { Level = Level.Info, Message = message, Data = data });

        public void LogWarn(string message, params string[] data) =>
            Log(new LogEntry { Level = Level.Warn, Message = message, Data = data });

        public void LogError(string message, params string[] data) =>
            Log(new LogEntry { Level = Level.Error, Message = message, Data = data });

        public void LogException(string message, Exception exception) =>
            Log(new LogEntry { Level = Level.Exception, Message = message, Exception = exception });

        public IEnumerable<LogEntry> History => LogHistory;
    }

    // ========================================================================
    // CONSOLE LOGGER / DEBUG LOGGER
    // ========================================================================
    // Location: Logging/Console/ConsoleLogger.cs OR Logging/Debug/DebugLogger.cs

    /// <summary>
    /// Logs to console output (Debug.WriteLine or Console.WriteLine).
    /// Useful for development and debugging.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        public void Log(LogEntry logEntry)
        {
            var message = FormatMessage(logEntry);

            // Option 1: Use System.Diagnostics.Debug (appears in VS Output window)
            System.Diagnostics.Debug.WriteLine(message);

            // Option 2: Use Console (appears in console if attached)
            // Console.WriteLine(message);
        }

        private string FormatMessage(LogEntry logEntry)
        {
            var level = logEntry.Level.ToString().ToUpper();
            var timestamp = logEntry.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

            if (logEntry.Exception != null)
            {
                return $"[{timestamp}] [{level}] {logEntry.Message}\n{logEntry.Exception}";
            }

            var dataString = logEntry.Data != null && logEntry.Data.Any()
                ? " | Data: " + string.Join(", ", logEntry.Data)
                : "";

            return $"[{timestamp}] [{level}] {logEntry.Message}{dataString}";
        }
    }

    /// <summary>
    /// Alternative name for ConsoleLogger.
    /// Some projects use DebugLogger, others use ConsoleLogger.
    /// Functionally identical - choose one name for your project.
    /// </summary>
    public class DebugLogger : ConsoleLogger
    {
        // Inherits all functionality from ConsoleLogger
    }

    // ========================================================================
    // FILE LOGGER
    // ========================================================================
    // Location: Logging/FileLog/FileLogger.cs

    /// <summary>
    /// Logs to a file on disk.
    /// Recommended for production diagnostics.
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();

        public FileLogger()
        {
            // Create log file in assembly directory
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var logFileName = $"ExtensionLog_{DateTime.Now:yyyyMMdd}.log";
            _logFilePath = Path.Combine(assemblyPath, logFileName);
        }

        public void Log(LogEntry logEntry)
        {
            try
            {
                var message = FormatMessage(logEntry);

                // Lock to prevent multiple threads from writing simultaneously
                lock (_lockObject)
                {
                    File.AppendAllText(_logFilePath, message + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail - don't let file logging break the application
            }
        }

        private string FormatMessage(LogEntry logEntry)
        {
            var level = logEntry.Level.ToString().ToUpper();
            var timestamp = logEntry.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

            if (logEntry.Exception != null)
            {
                return $"[{timestamp}] [{level}] {logEntry.Message}\n{logEntry.Exception}\n";
            }

            var dataString = logEntry.Data != null && logEntry.Data.Any()
                ? " | Data: " + string.Join(", ", logEntry.Data)
                : "";

            return $"[{timestamp}] [{level}] {logEntry.Message}{dataString}";
        }
    }

    // ========================================================================
    // EGATEWAY LOGGER (SIMPHONY EVENT LOG)
    // ========================================================================
    // Location: Logging/EGateway/EGatewayLogger.cs

    /// <summary>
    /// Logs to Simphony's EGateway event log.
    /// Visible in Simphony EMC under "Event Log".
    /// Optional - only if you want logs in Simphony's management console.
    /// </summary>
    public class EGatewayLogger : ILogger
    {
        public void Log(LogEntry logEntry)
        {
            try
            {
                // Only log warnings, errors, and exceptions to EGateway
                // (avoid cluttering Simphony event log with debug/info)
                if (logEntry.Level == Level.Debug || logEntry.Level == Level.Info)
                    return;

                var message = FormatMessage(logEntry);

                // Write to Simphony event log
                // Note: Requires reference to EGatewayDB.dll
                // EGateway.WriteLog(message, (int)logEntry.Level);

                // If EGatewayDB.dll not available, use Windows Event Log:
                // System.Diagnostics.EventLog.WriteEntry("SimphonyExtension", message, EventLogEntryType.Warning);
            }
            catch
            {
                // Silently fail
            }
        }

        private string FormatMessage(LogEntry logEntry)
        {
            if (logEntry.Exception != null)
            {
                return $"{logEntry.Message}\n{logEntry.Exception}";
            }

            return logEntry.Message;
        }
    }
}

// ============================================================================
// REGISTRATION IN SIMPHONY DEPENDENCIES
// ============================================================================

/*
 * Add to SimphonyDependencies.Install():
 *
 * // Log manager (singleton)
 * DependencyManager.RegisterByType<ILogManager, LogManager>();
 *
 * // Multiple loggers (all will receive log entries)
 * DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<ConsoleLogger>());
 * // OR
 * DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<DebugLogger>());
 *
 * DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<FileLogger>());
 *
 * // Optional: EGateway logger
 * DependencyManager.RegisterByInstance<ILogger>(DependencyManager.Resolve<EGatewayLogger>());
 */

// ============================================================================
// USAGE EXAMPLES
// ============================================================================

/*
 * BASIC LOGGING:
 *
 * public class MyScript : IScript
 * {
 *     private readonly ILogManager _logger;
 *
 *     public MyScript(ILogManager logger)
 *     {
 *         _logger = logger;
 *     }
 *
 *     public void Execute(string functionName, string argument)
 *     {
 *         _logger.LogInfo($"Executing {functionName}");
 *
 *         try
 *         {
 *             // Business logic
 *             _logger.LogDebug("Processing step 1");
 *             // ...
 *             _logger.LogInfo("Operation completed successfully");
 *         }
 *         catch (Exception e)
 *         {
 *             _logger.LogException("Error during execution", e);
 *             throw;
 *         }
 *     }
 * }
 */

/*
 * LOGGING WITH ADDITIONAL DATA:
 *
 * _logger.LogInfo("Processing check", $"CheckNumber: {checkNumber}", $"Amount: {amount}");
 *
 * _logger.LogWarn("Invalid parameter", $"Parameter: {paramName}", $"Value: {paramValue}");
 *
 * _logger.LogError("Database connection failed", $"ConnectionString: {connString}");
 */

/*
 * EXCEPTION LOGGING:
 *
 * try
 * {
 *     // Code that might throw
 * }
 * catch (Exception e)
 * {
 *     _logger.LogException("Operation failed", e);
 *
 *     // If using ExceptionHelper:
 *     var first = ExceptionHelper.GetFirstException(e);
 *     _logger.LogException("Operation failed", first);
 * }
 */

/*
 * LOG HISTORY ACCESS:
 *
 * var logger = DependencyManager.Resolve<ILogManager>();
 * var recentLogs = logger.History.Take(10);
 *
 * foreach (var entry in recentLogs)
 * {
 *     Console.WriteLine($"{entry.LocalDateTime}: {entry.Message}");
 * }
 */

// ============================================================================
// DEBUG CONTROL GUIDE
// ============================================================================

/*
 * FILE-BASED DEBUG CONTROL (Recommended):
 *
 * ENABLE DEBUG LOGGING:
 * 1. Navigate to extension DLL directory:
 *    C:\Micros\Simphony\WebServer\wwwroot\EGateway\Handlers\ExtensionApplications\YourExtension\
 * 2. Create empty file named "debug.txt" (or "debug.flag", any file starting with "debug")
 * 3. Debug logging is now enabled
 *
 * DISABLE DEBUG LOGGING:
 * 1. Delete the debug file
 * 2. Debug logging is now disabled
 *
 * ADVANTAGES:
 * - No recompilation needed
 * - Toggle in production without redeployment
 * - Non-technical staff can enable for troubleshooting
 */

/*
 * COMPILER DIRECTIVE DEBUG CONTROL:
 *
 * DEBUG BUILD:
 * - All logging levels active
 *
 * RELEASE BUILD:
 * - Debug logging disabled at compile time
 * - Zero runtime overhead
 *
 * ADVANTAGES:
 * - Best performance (no runtime checks)
 * - Clear DEBUG vs RELEASE separation
 *
 * DISADVANTAGES:
 * - Cannot enable debug logging in release build
 * - Requires recompilation to change
 */

// ============================================================================
// BEST PRACTICES
// ============================================================================

/*
 * LOG LEVEL GUIDELINES:
 *
 * DEBUG:
 * - Detailed diagnostic information
 * - Loop iterations, variable values
 * - Not visible in production (with debug control)
 * Example: _logger.LogDebug($"Processing item {i} of {totalItems}");
 *
 * INFO:
 * - Informational messages
 * - Application flow, operations completed
 * - Always visible
 * Example: _logger.LogInfo("Extension application initialized");
 *
 * WARN:
 * - Warning conditions
 * - Recoverable errors, deprecations
 * - Always visible
 * Example: _logger.LogWarn("Configuration missing, using default value");
 *
 * ERROR:
 * - Error conditions
 * - Operation failed but application continues
 * - Always visible
 * Example: _logger.LogError("Failed to connect to external API");
 *
 * EXCEPTION:
 * - Exceptions with stack trace
 * - Unexpected errors
 * - Always visible
 * Example: _logger.LogException("Unhandled error in script", exception);
 */

/*
 * COMMON MISTAKES:
 *
 * MISTAKE 1: Throwing exceptions from loggers
 * BAD:
 * public void Log(LogEntry entry)
 * {
 *     File.AppendAllText(path, entry.Message); // Can throw!
 * }
 *
 * GOOD:
 * public void Log(LogEntry entry)
 * {
 *     try
 *     {
 *         File.AppendAllText(path, entry.Message);
 *     }
 *     catch
 *     {
 *         // Silently fail
 *     }
 * }
 *
 * MISTAKE 2: Logging in tight loops without DEBUG level
 * BAD:
 * foreach (var item in items)
 * {
 *     _logger.LogInfo($"Processing {item}"); // Clutters logs!
 * }
 *
 * GOOD:
 * foreach (var item in items)
 * {
 *     _logger.LogDebug($"Processing {item}"); // Only when debug enabled
 * }
 * _logger.LogInfo($"Processed {items.Count} items"); // Summary
 *
 * MISTAKE 3: Not using ExceptionHelper for exception logging
 * BAD:
 * _logger.LogException("Error", e); // May log wrapper exceptions
 *
 * GOOD:
 * var first = ExceptionHelper.GetFirstException(e);
 * _logger.LogException("Error", first); // Logs root cause
 */
