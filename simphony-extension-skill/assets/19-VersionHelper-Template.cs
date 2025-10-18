using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace YourNamespace.Helpers
{
    /// <summary>
    /// Version information helper for Simphony Extension Applications.
    /// Provides assembly version, build time, and environment information.
    /// Place in: Helpers/VersionHelper.cs
    ///
    /// Features:
    /// - Assembly version from AssemblyInfo
    /// - Build timestamp from PE header
    /// - .NET Framework version
    /// - Windows version information
    /// </summary>
    public static class VersionHelper
    {
        private struct _IMAGE_FILE_HEADER
        {
            public ushort Machine;
            public ushort NumberOfSections;
            public uint TimeDateStamp;
            public uint PointerToSymbolTable;
            public uint NumberOfSymbols;
            public ushort SizeOfOptionalHeader;
            public ushort Characteristics;
        };

        static VersionHelper()
        {
            // Read assembly version
            IntegrationVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString(4);

            // Read .NET Framework version
            EnvironmentVersion = Environment.Version.ToString(4);

            // Read Windows version from registry
            ReadWindowsVersion();

            // Read build timestamp from PE header
            ReadIntegrationBuildTimeUtc();
        }

        private static void ReadWindowsVersion()
        {
            var windowsName = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion",
                "ProductName",
                null
            ) as string;

            var windowsBuild = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "ReleaseId",
                ""
            ).ToString();

            WindowsVersion = $"{windowsName} (Build {windowsBuild})";
        }

        public static void ReadIntegrationBuildTimeUtc()
        {
            var buffer = new byte[Math.Max(Marshal.SizeOf(typeof(_IMAGE_FILE_HEADER)), 4)];

            using (var fileStream = new FileStream(Assembly.GetExecutingAssembly().Location, FileMode.Open, FileAccess.Read))
            {
                fileStream.Position = 0x3C;
                fileStream.Read(buffer, 0, 4);
                fileStream.Position = BitConverter.ToUInt32(buffer, 0); // COFF header offset
                fileStream.Read(buffer, 0, 4); // "PE\0\0"
                fileStream.Read(buffer, 0, buffer.Length);
            }

            var pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var coffHeader = (_IMAGE_FILE_HEADER)Marshal.PtrToStructure(
                    pinnedBuffer.AddrOfPinnedObject(),
                    typeof(_IMAGE_FILE_HEADER)
                );

                IntegrationBuildDateTimeUtc = new DateTime(1970, 1, 1) + new TimeSpan(coffHeader.TimeDateStamp * TimeSpan.TicksPerSecond);
            }
            finally
            {
                pinnedBuffer.Free();
            }
        }

        /// <summary>
        /// Assembly version (e.g., "1.2.3.4")
        /// </summary>
        public static string IntegrationVersion { get; }

        /// <summary>
        /// .NET Framework version
        /// </summary>
        public static string EnvironmentVersion { get; set; }

        /// <summary>
        /// Build timestamp (UTC) read from PE header
        /// </summary>
        public static DateTime IntegrationBuildDateTimeUtc;

        /// <summary>
        /// Windows version information
        /// </summary>
        public static string WindowsVersion { get; set; }

        /// <summary>
        /// Formatted name and version string.
        /// Customize this to match your extension name.
        /// Example: "Your Extension Name v1.2.3.4"
        /// </summary>
        public static string NameAndVersion => $"Your Extension Name v{IntegrationVersion}";
    }
}
