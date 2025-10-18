using YourNamespace.Contracts.Clients;
using YourNamespace.Helpers;

namespace YourNamespace.Scripts
{
    /// <summary>
    /// Standard version display script - ALWAYS include in every extension.
    /// Essential for troubleshooting to verify which version is actually loaded in Simphony.
    /// Place in: Scripts/Version.cs
    ///
    /// Usage:
    /// 1. Create a button in Simphony that calls this script
    /// 2. Configure button: Script="Version", Function="ShowVersion"
    /// 3. Click button to display version information to verify deployment
    /// </summary>
    public class Version : AbstractScript
    {
        private readonly IOpsContextClient _opsContextClient;

        public Version(IOpsContextClient opsContextClient)
        {
            _opsContextClient = opsContextClient;
        }

        /// <summary>
        /// Display extension name and version.
        /// Uses VersionHelper to get assembly version information.
        /// </summary>
        public void ShowVersion()
        {
            _opsContextClient.ShowMessage(VersionHelper.NameAndVersion);
        }
    }
}
