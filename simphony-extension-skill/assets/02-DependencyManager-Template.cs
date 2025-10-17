// DependencyManager.cs
// Universal Mandatory Pattern - Custom DI Container
// Location: Dependency/DependencyManager.cs
// Validated: Production version (Projects 1, 3, 4, 5) - Use this one

using System;
using System.Collections.Generic;
using System.Linq;

namespace YourNamespace.Dependency
{
    /// <summary>
    /// Base class for dependency installers.
    /// Inherit from this to create modular dependency registration classes.
    /// </summary>
    public abstract class AbstractDependencyInstaller
    {
        public abstract void Install();
    }

    /// <summary>
    /// Attribute to specify named dependency injection for constructor parameters.
    /// Use when a constructor parameter needs a specific named instance.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class DependencyNameAttribute : Attribute
    {
        public string Name { get; }

        public DependencyNameAttribute(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Lightweight dependency injection container with auto-wiring support.
    /// Provides registration, resolution, and installer pattern.
    /// </summary>
    public static class DependencyManager
    {
        /// <summary>
        /// Internal registration class to track type, resolution function, and name.
        /// </summary>
        private class Registration
        {
            public Type Type { get; set; }
            public Func<object> Resolve { get; set; }
            public Type ResolveType { get; set; }
            public string Name { get; set; }
        }

        private static readonly List<Registration> Registrations = new List<Registration>();

        // ====================================================================
        // REGISTRATION METHODS
        // ====================================================================

        /// <summary>
        /// Registers an interface to implementation mapping.
        /// The implementation will be auto-wired (constructor dependencies resolved automatically).
        /// </summary>
        /// <typeparam name="T">Interface type</typeparam>
        /// <typeparam name="I">Implementation type</typeparam>
        /// <param name="name">Optional name for multiple implementations of same interface</param>
        /// <example>
        /// DependencyManager.RegisterByType&lt;IScript, MyScript&gt;(nameof(MyScript));
        /// DependencyManager.RegisterByType&lt;ILogger, FileLogger&gt;();
        /// </example>
        public static void RegisterByType<T, I>(string name = "")
        {
            Registrations.Add(new Registration
            {
                Type = typeof(T),
                Resolve = () => ResolveObject(typeof(I), string.Empty),
                ResolveType = typeof(I),
                Name = name,
            });
        }

        /// <summary>
        /// Registers a singleton instance.
        /// The same instance will be returned on every resolution.
        /// </summary>
        /// <typeparam name="T">Type to register</typeparam>
        /// <param name="instance">Instance to register</param>
        /// <param name="name">Optional name for multiple instances</param>
        /// <example>
        /// var status = new Status();
        /// DependencyManager.RegisterByInstance(status);
        ///
        /// var fileLogger = new FileLogger();
        /// DependencyManager.RegisterByInstance&lt;ILogger&gt;(fileLogger);
        /// </example>
        public static void RegisterByInstance<T>(T instance, string name = "")
        {
            Registrations.Add(new Registration
            {
                Type = typeof(T),
                Resolve = () => instance,
                ResolveType = instance.GetType(),
                Name = name,
            });
        }

        /// <summary>
        /// Registers a factory function for creating instances.
        /// The factory will be called each time the type is resolved (not singleton).
        /// </summary>
        /// <typeparam name="T">Type to register</typeparam>
        /// <param name="factory">Factory function</param>
        /// <param name="name">Optional name</param>
        /// <example>
        /// DependencyManager.RegisterByFunction&lt;IDbConnection&gt;(() =>
        ///     new SqlConnection(connectionString));
        /// </example>
        public static void RegisterByFunction<T>(Func<T> factory, string name = "")
        {
            Registrations.Add(new Registration
            {
                Type = typeof(T),
                Resolve = () => factory.Invoke(),
                ResolveType = typeof(T),
                Name = name,
            });
        }

        /// <summary>
        /// Removes all registrations for a specific type and name.
        /// Useful for re-registering or cleanup in tests.
        /// </summary>
        public static void Remove<T>(string name = "")
        {
            Registrations.RemoveAll(x => x.Type == typeof(T) && x.Name == name);
        }

        // ====================================================================
        // RESOLUTION METHODS
        // ====================================================================

        /// <summary>
        /// Resolves a registered type.
        /// Throws exception if type is not registered.
        /// </summary>
        /// <typeparam name="T">Type to resolve</typeparam>
        /// <param name="name">Optional name for named registrations</param>
        /// <returns>Instance of type T</returns>
        /// <exception cref="InvalidOperationException">If type not registered</exception>
        /// <example>
        /// var logger = DependencyManager.Resolve&lt;ILogManager&gt;();
        /// var myScript = DependencyManager.Resolve&lt;IScript&gt;(nameof(MyScript));
        /// </example>
        public static T Resolve<T>(string name = "")
        {
            return (T)ResolveObject(typeof(T), name);
        }

        /// <summary>
        /// Non-generic version of Resolve.
        /// Useful for reflection-based resolution.
        /// </summary>
        public static object Resolve(Type type, string name = "")
        {
            return ResolveObject(type, name);
        }

        /// <summary>
        /// Resolves a type, returning null/default if not registered.
        /// Safe alternative to Resolve that doesn't throw.
        /// Recommended for optional dependencies and script routing.
        /// </summary>
        /// <typeparam name="T">Type to resolve</typeparam>
        /// <param name="name">Optional name</param>
        /// <returns>Instance of T or default(T) if not found</returns>
        /// <example>
        /// // Safe script resolution (won't throw if script not found)
        /// var script = DependencyManager.ResolveSafe&lt;IScript&gt;(args.Script);
        /// if (script == null)
        ///     throw new Exception($"Script {args.Script} not found");
        /// </example>
        public static T ResolveSafe<T>(string name = "")
        {
            var type = typeof(T);
            var registration = Registrations.LastOrDefault(x => x.Type == type && (x.Name == name || (string.IsNullOrEmpty(x.Name) && string.IsNullOrEmpty(name))));

            if (registration == null)
                return default(T);

            return (T)ResolveObject(typeof(T), name, false);
        }

        /// <summary>
        /// Alternative name for ResolveSafe (used in Projects 2 & 5).
        /// Functionally identical to ResolveSafe.
        /// Choose one and use consistently in your project.
        /// </summary>
        public static T ResolveOrDefault<T>(string name = "")
        {
            return ResolveSafe<T>(name);
        }

        /// <summary>
        /// Resolves all registered instances of a type.
        /// Useful for multi-target patterns like multiple ILogger implementations.
        /// </summary>
        /// <typeparam name="T">Type to resolve</typeparam>
        /// <param name="name">Optional name filter (empty string matches all)</param>
        /// <returns>Collection of all registered instances</returns>
        /// <example>
        /// // Get all loggers
        /// var loggers = DependencyManager.ResolveAll&lt;ILogger&gt;();
        /// foreach (var logger in loggers)
        ///     logger.Log(logEntry);
        ///
        /// // Get all services and start them
        /// DependencyManager.ResolveAll&lt;IService&gt;().ToList().ForEach(x => x.Start());
        /// </example>
        public static IEnumerable<T> ResolveAll<T>(string name = "")
        {
            return Registrations
                .Where(x => x.Type == typeof(T) && (x.Name == name || name == ""))
                .Select(x => (T)x.Resolve.Invoke())
                .ToList();
        }

        // ====================================================================
        // INSTALLER PATTERN
        // ====================================================================

        /// <summary>
        /// Executes a dependency installer class.
        /// Installers organize related registrations into modular units.
        /// </summary>
        /// <typeparam name="T">Installer type (must inherit AbstractDependencyInstaller)</typeparam>
        /// <example>
        /// DependencyManager.Install&lt;SimphonyDependencies&gt;();
        /// </example>
        public static void Install<T>() where T : AbstractDependencyInstaller
        {
            ((AbstractDependencyInstaller)CreateInstance(typeof(T))).Install();
        }

        // ====================================================================
        // INTERNAL AUTO-WIRING
        // ====================================================================

        /// <summary>
        /// Internal resolution with optional instance creation.
        /// Handles auto-wiring of constructor dependencies.
        /// </summary>
        private static object ResolveObject(Type type, string name, bool createInstance = true)
        {
            var registration = Registrations.LastOrDefault(x => x.Type == type && (x.Name == name || (string.IsNullOrEmpty(x.Name) && string.IsNullOrEmpty(name))));

            if (registration != null)
                return registration.Resolve.Invoke();

            if (createInstance && !type.IsAbstract)
                return CreateInstance(type);

            throw new InvalidOperationException($"No registration found for {type.Name}" + (string.IsNullOrEmpty(name) ? "" : $" with name '{name}'"));
        }

        /// <summary>
        /// Creates instance with automatic constructor dependency injection.
        /// Uses reflection to find constructor parameters and resolve them.
        /// Supports DependencyNameAttribute for named dependencies.
        /// </summary>
        private static object CreateInstance(Type type)
        {
            var parameters = new List<object>();

            // Get constructor with fewest parameters (most specific)
            var constructor = type.GetConstructors().OrderBy(x => x.GetParameters().Count()).First();

            foreach (var parameter in constructor.GetParameters())
            {
                // Check for DependencyNameAttribute
                var nameAttributes = parameter.GetCustomAttributes(typeof(DependencyNameAttribute), false);

                if (nameAttributes.Any())
                {
                    // Named dependency
                    var dependencyName = ((DependencyNameAttribute)nameAttributes[0]).Name;
                    parameters.Add(ResolveObject(parameter.ParameterType, dependencyName));
                }
                else
                {
                    // Unnamed dependency
                    parameters.Add(ResolveObject(parameter.ParameterType, ""));
                }
            }

            return parameters.Any()
                ? Activator.CreateInstance(type, parameters.ToArray())
                : Activator.CreateInstance(type);
        }
    }
}

// ============================================================================
// USAGE EXAMPLES
// ============================================================================

/*
 * BASIC REGISTRATION AND RESOLUTION:
 *
 * // Register types
 * DependencyManager.RegisterByType<ILogManager, LogManager>();
 * DependencyManager.RegisterByType<IConfigurationClient, SimphonyConfigurationClient>();
 *
 * // Resolve types
 * var logger = DependencyManager.Resolve<ILogManager>();
 * var config = DependencyManager.Resolve<IConfigurationClient>();
 */

/*
 * NAMED REGISTRATIONS (Multiple implementations):
 *
 * // Register multiple scripts
 * DependencyManager.RegisterByType<IScript, ClockInOut>(nameof(ClockInOut));
 * DependencyManager.RegisterByType<IScript, Loyalty>(nameof(Loyalty));
 * DependencyManager.RegisterByType<IScript, Version>(nameof(Version));
 *
 * // Resolve specific script
 * var script = DependencyManager.Resolve<IScript>("ClockInOut");
 *
 * // Safe resolution (returns null if not found)
 * var script = DependencyManager.ResolveSafe<IScript>(args.Script);
 * if (script == null)
 *     throw new Exception($"Script {args.Script} not found");
 */

/*
 * SINGLETON INSTANCES:
 *
 * // Register single instance
 * var status = new Status();
 * DependencyManager.RegisterByInstance(status);
 *
 * // All resolutions return same instance
 * var status1 = DependencyManager.Resolve<Status>();
 * var status2 = DependencyManager.Resolve<Status>();
 * // status1 == status2 (same reference)
 */

/*
 * MULTI-TARGET PATTERN (Multiple implementations):
 *
 * // Register multiple loggers
 * DependencyManager.RegisterByInstance<ILogger>(new ConsoleLogger());
 * DependencyManager.RegisterByInstance<ILogger>(new FileLogger());
 * DependencyManager.RegisterByInstance<ILogger>(new EGatewayLogger());
 *
 * // Get all loggers
 * var loggers = DependencyManager.ResolveAll<ILogger>();
 * foreach (var logger in loggers)
 *     logger.Log(logEntry);
 */

/*
 * AUTO-WIRING (Constructor injection):
 *
 * // This class will have dependencies auto-injected:
 * public class MyScript : IScript
 * {
 *     private readonly ILogManager _logger;
 *     private readonly IConfigurationClient _config;
 *
 *     // Dependencies automatically resolved!
 *     public MyScript(ILogManager logger, IConfigurationClient config)
 *     {
 *         _logger = logger;
 *         _config = config;
 *     }
 * }
 *
 * // When you resolve MyScript, DependencyManager automatically resolves
 * // ILogManager and IConfigurationClient and passes them to constructor.
 */

/*
 * NAMED DEPENDENCY INJECTION:
 *
 * public class MyClass
 * {
 *     // Inject specific named dependency
 *     public MyClass(
 *         [DependencyName("local")] ITimeKeeping localTimeKeeping,
 *         [DependencyName("remote")] ITimeKeeping remoteTimeKeeping)
 *     {
 *         // ...
 *     }
 * }
 */

/*
 * FACTORY REGISTRATION:
 *
 * // Register factory for non-singleton instances
 * DependencyManager.RegisterByFunction<IDbConnection>(() =>
 *     new SqlConnection(connectionString));
 *
 * // Each resolution creates new instance
 * using (var conn = DependencyManager.Resolve<IDbConnection>())
 * {
 *     // Use connection
 * }
 */

/*
 * INSTALLER PATTERN:
 *
 * public class SimphonyDependencies : AbstractDependencyInstaller
 * {
 *     public override void Install()
 *     {
 *         // Register all dependencies here
 *         DependencyManager.RegisterByType<ILogManager, LogManager>();
 *         // ... more registrations
 *     }
 * }
 *
 * // Install all dependencies at once
 * DependencyManager.Install<SimphonyDependencies>();
 */
