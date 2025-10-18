using System;
using System.Collections.Generic;
using System.IO;
using YourNamespace.Contracts.Clients;
using YourNamespace.Entities.Configuration;
using YourNamespace.Serializers;

namespace YourNamespace.Clients.Configuration
{
    /// <summary>
    /// File-based configuration client - reads from local XML file.
    /// Useful for non-CAPS workstations or when Simphony DataStore is not available.
    /// Can be used as fallback or for specific deployment scenarios.
    /// Place in: Clients/Configuration/FileConfigurationClient.cs
    /// </summary>
    public class FileConfigurationClient : IConfigurationClient
    {
        private readonly AppStatus _status;

        public FileConfigurationClient(AppStatus status)
        {
            _status = status;
        }

        public YourConfig ReadConfig()
        {
            var filename = Path.Combine(_status.AppDir, $"{_status.AppName}.xml");

            if (!File.Exists(filename))
                throw new Exception($"Configuration file not found: {filename}");

            var data = File.ReadAllText(filename);

            return XmlSerializer.Deserialize<YourConfig>(data);
        }

        public IEnumerable<string> ReadZonableKeys()
        {
            throw new NotImplementedException("File-based configuration does not support zoneable keys");
        }

        public string ReadTextFile(string zonableKey)
        {
            throw new NotImplementedException("File-based configuration does not support text files");
        }
    }
}
