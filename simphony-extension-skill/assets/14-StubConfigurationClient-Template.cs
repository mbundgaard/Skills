using System.Collections.Generic;
using System.IO;
using YourNamespace.Contracts.Clients;
using YourNamespace.Entities.Configuration;
using YourNamespace.Serializers;

namespace YourNamespace.Clients.Configuration
{
    /// <summary>
    /// Stub configuration client for development and testing.
    /// Creates default configuration in code and writes to disk for inspection.
    /// Perfect for unit tests and development without Simphony.
    /// Place in: Clients/Configuration/StubConfigurationClient.cs
    /// </summary>
    public class StubConfigurationClient : IConfigurationClient
    {
        private readonly AppStatus _status;

        public StubConfigurationClient(AppStatus status)
        {
            _status = status;
        }

        public YourConfig ReadConfig()
        {
            // Create default configuration for testing/development
            var config = new YourConfig
            {
                // Set your default values here
                SomeSetting = "default value",
                SomeNumber = 42,

                // Example: Configure timers for background tasks
                // Timers = new[]
                // {
                //     new Timer
                //     {
                //         Cron = "0 * * * *",  // Every hour
                //         Script = "YourScript",
                //         Function = "YourFunction"
                //     }
                // },

                // Example: Configure event handlers
                // Events = new[]
                // {
                //     new Event
                //     {
                //         Name = "OpsReady",
                //         Script = "YourScript",
                //         Function = "Initialize"
                //     }
                // }
            };

            // Write configuration to disk for inspection/debugging
            var filename = Path.Combine(_status.AppDir, $"{_status.AppName}.xml");
            var content = XmlSerializer.Serialize(config);
            File.WriteAllText(filename, content);

            return config;
        }

        public IEnumerable<string> ReadZonableKeys()
        {
            throw new System.NotImplementedException("Stub configuration does not support zoneable keys");
        }

        public string ReadTextFile(string zonableKey)
        {
            throw new System.NotImplementedException("Stub configuration does not support text files");
        }
    }
}
