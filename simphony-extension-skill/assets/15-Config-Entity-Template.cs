using System;
using System.Xml.Serialization;

namespace YourNamespace.Entities.Configuration
{
    /// <summary>
    /// Main configuration entity for your extension.
    /// Stored as XML in Simphony Extension Application Content.
    /// Place in: Entities/Configuration/YourConfig.cs
    ///
    /// Pattern Guidelines:
    /// 1. Use default initializers to prevent null reference exceptions
    /// 2. Use XmlAttribute for simple properties when you want compact XML
    /// 3. Use nested classes for complex hierarchical configuration
    /// 4. Arrays/collections for repeating configuration items
    /// 5. Track configuration read time for diagnostics
    /// </summary>
    public class YourConfig
    {
        /// <summary>
        /// Timestamp when configuration was read (for diagnostics)
        /// </summary>
        public DateTime ReadTime { get; set; } = DateTime.Now;

        // ===================================================================
        // SIMPLE PROPERTIES
        // ===================================================================

        /// <summary>
        /// Example: Simple string setting
        /// </summary>
        public string SomeSetting { get; set; }

        /// <summary>
        /// Example: Simple numeric setting
        /// </summary>
        public int SomeNumber { get; set; }

        /// <summary>
        /// Example: Boolean flag
        /// </summary>
        public bool EnableFeature { get; set; }

        // ===================================================================
        // COLLECTIONS (Use default initializers to prevent null)
        // ===================================================================

        /// <summary>
        /// Example: Event handlers configuration
        /// Maps Simphony events to Script/Function handlers
        /// </summary>
        public Event[] Events { get; set; } = new Event[0];

        /// <summary>
        /// Example: Scheduled tasks configuration
        /// Cron-based timers for background processing
        /// </summary>
        public Timer[] Timers { get; set; } = new Timer[0];

        // ===================================================================
        // DOMAIN-SPECIFIC CONFIGURATION
        // Add your extension-specific settings here
        // ===================================================================

        // Example for export applications:
        // public string OutputFolder { get; set; }
        // public string EndOfDay { get; set; }

        // Example for loyalty applications:
        // public string ApiEndpoint { get; set; }
        // public string ApiKey { get; set; }

        // Example for payment applications:
        // public PaymentProvider[] Providers { get; set; } = new PaymentProvider[0];
    }

    /// <summary>
    /// Event handler configuration entity.
    /// Maps a Simphony event to a Script class and function to execute.
    /// Example XML: &lt;Event Name="OpsReady" Script="InitScript" Function="Initialize" /&gt;
    /// </summary>
    public class Event
    {
        /// <summary>
        /// Simphony event name (e.g., "OpsReady", "CheckTotal", "Tender")
        /// </summary>
        [XmlAttribute]
        public string Name { get; set; }

        /// <summary>
        /// Script class name to resolve from DI container
        /// </summary>
        [XmlAttribute]
        public string Script { get; set; }

        /// <summary>
        /// Function/method name to call on the script
        /// </summary>
        [XmlAttribute]
        public string Function { get; set; }
    }

    /// <summary>
    /// Timer/scheduled task configuration entity.
    /// Defines cron-based scheduled execution of scripts.
    /// Example XML: &lt;Timer Cron="0 * * * *" Script="ExportScript" Function="Run" /&gt;
    /// </summary>
    public class Timer
    {
        /// <summary>
        /// Cron expression (e.g., "0 * * * *" = every hour at minute 0)
        /// </summary>
        [XmlAttribute]
        public string Cron { get; set; }

        /// <summary>
        /// Script class name to resolve from DI container
        /// </summary>
        [XmlAttribute]
        public string Script { get; set; }

        /// <summary>
        /// Function/method name to call on the script
        /// </summary>
        [XmlAttribute]
        public string Function { get; set; }

        /// <summary>
        /// Optional arguments to pass to the function
        /// </summary>
        [XmlAttribute]
        public string Arguments { get; set; }

        /// <summary>
        /// Add random delay (0-59 seconds) before execution
        /// Useful to prevent multiple workstations from executing simultaneously
        /// </summary>
        [XmlAttribute]
        public bool RandomMin { get; set; }
    }

    // ===================================================================
    // ADD YOUR DOMAIN-SPECIFIC ENTITIES HERE
    // ===================================================================

    /// <summary>
    /// Example: Payment provider configuration
    /// </summary>
    //public class PaymentProvider
    //{
    //    [XmlAttribute]
    //    public string Name { get; set; }
    //
    //    [XmlAttribute]
    //    public string Endpoint { get; set; }
    //
    //    [XmlAttribute]
    //    public int TenderMediaNumber { get; set; }
    //
    //    public string ApiKey { get; set; }
    //}

    /// <summary>
    /// Example: Export mapping configuration
    /// </summary>
    //public class ExportMapping
    //{
    //    [XmlAttribute]
    //    public int MenuItemNumber { get; set; }
    //
    //    [XmlAttribute]
    //    public string ExternalCode { get; set; }
    //
    //    [XmlAttribute]
    //    public string Category { get; set; }
    //}
}
