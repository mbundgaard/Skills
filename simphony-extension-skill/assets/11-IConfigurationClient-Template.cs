using System.Collections.Generic;
using YourNamespace.Entities.Configuration;

namespace YourNamespace.Contracts.Clients
{
    /// <summary>
    /// Interface for reading extension configuration.
    /// Configuration is stored in Simphony as Extension Application Content (XML format).
    /// Place in: Contracts/Clients/IConfigurationClient.cs
    /// </summary>
    public interface IConfigurationClient
    {
        /// <summary>
        /// Read the main configuration object from Simphony or file system.
        /// </summary>
        /// <returns>Configuration object (never null - returns empty config if not found)</returns>
        YourConfig ReadConfig();

        /// <summary>
        /// Read all available zoneable keys (configuration zones) from Extension Application.
        /// Useful for multi-zone configurations.
        /// </summary>
        IEnumerable<string> ReadZonableKeys();

        /// <summary>
        /// Read a specific text file/content by zoneable key.
        /// Useful for additional configuration files (templates, mappings, etc.)
        /// </summary>
        string ReadTextFile(string zonableKey);
    }
}
