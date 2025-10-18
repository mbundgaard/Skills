using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Micros.Ops.Extensibility;
using YourNamespace.Contracts.Clients;
using YourNamespace.Entities.Configuration;
using YourNamespace.Serializers;

namespace YourNamespace.Clients.Configuration
{
    /// <summary>
    /// Production configuration client - reads from Simphony DataStore.
    /// Reads Extension Application Content as XML and deserializes to config object.
    /// Also writes backup XML file to disk for debugging.
    /// Place in: Clients/Configuration/SimphonyConfigurationClient.cs
    /// </summary>
    public class SimphonyConfigurationClient : IConfigurationClient
    {
        private static readonly object _lock = new object();
        private readonly OpsExtensibilityEnvironment _environment;
        private readonly AppStatus _status;

        public SimphonyConfigurationClient(OpsExtensibilityEnvironment environment, AppStatus status)
        {
            _environment = environment;
            _status = status;
        }

        public YourConfig ReadConfig()
        {
            // Read Extension Application by name and RVC
            var extApp = _environment.DataStore.ReadExtensionApplicationByName(
                _environment.OpsContext.RvcID,
                _environment.ApplicationName
            );

            // Iterate through content list (hierarchical - check higher zones first)
            foreach (var extAppContent in extApp.ContentList.OrderByDescending(x => x.HierStrucID))
            {
                var unicodeData = _environment.DataStore
                    .ReadExtensionApplicationContentByID(extAppContent.ExtensionApplicationContentID)
                    .ContentData
                    .DataBlob;

                if (unicodeData == null) continue;

                // Convert binary blob to string
                var stringData = Encoding.Unicode.GetString(unicodeData);

                // Check if it's your config XML (root element name)
                if (stringData.StartsWith("<YourConfig>"))
                {
                    var config = XmlSerializer.Deserialize<YourConfig>(stringData);

                    // Write backup XML to disk for debugging/troubleshooting
                    try
                    {
                        lock (_lock)
                        {
                            var filename = Path.Combine(_status.AppDir, $"{_status.AppName}.xml");
                            File.WriteAllText(filename, XmlSerializer.Serialize(config));
                        }
                    }
                    catch
                    {
                        // Ignore file write errors - don't fail configuration load
                    }

                    return config;
                }
            }

            // Return empty config if not found (never return null)
            return new YourConfig();
        }

        public IEnumerable<string> ReadZonableKeys()
        {
            var extApp = _environment.DataStore.ReadExtensionApplicationByName(
                _environment.OpsContext.RvcID,
                _environment.ApplicationName
            );

            return extApp.ContentList.Select(x => x.ZoneableKey).ToList();
        }

        public string ReadTextFile(string zonableKey)
        {
            var extApp = _environment.DataStore.ReadExtensionApplicationByName(
                _environment.OpsContext.RvcID,
                _environment.ApplicationName
            );

            var extAppContent = extApp.ContentList.FirstOrDefault(x => x.ZoneableKey == zonableKey);

            if (extAppContent == null)
                throw new Exception($"Zoneable key '{zonableKey}' not found");

            var unicodeData = _environment.DataStore
                .ReadExtensionApplicationContentByID(extAppContent.ExtensionApplicationContentID)
                .ContentData
                .DataBlob;

            if (unicodeData == null)
                throw new Exception($"No data found for zoneable key '{zonableKey}'");

            return Encoding.Unicode.GetString(unicodeData);
        }
    }
}
