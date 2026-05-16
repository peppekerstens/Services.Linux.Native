// Copyright (c) peppekerstens.
// Licensed under the MIT License.

using System.Globalization;

namespace Microsoft.PowerShell.Commands
{
    /// <summary>
    /// Centralized error message templates for the Services.Linux.Native module.
    /// All messages use string.Format with CultureInfo.InvariantCulture.
    /// </summary>
    internal static class ErrorMessages
    {
        internal const string ElevationRequired = "{0} requires root privileges. Use 'sudo pwsh'.";
        internal const string SubprocessFailed = "{0} failed: {1} (exit code {2})";
        internal const string UnitOperationFailed = "{0} {1} failed: {2}";
        internal const string StartupTypeChangeFailed = "Could not change startup type for {0}: {1}";
        internal const string StatusChangeFailed = "Could not set status for {0}: {1}";
        internal const string UnitFileCreateFailed = "Failed to create unit file for {0}: {1}";
        internal const string DaemonReloadFailed = "Created unit file but daemon-reload failed: {0}";
        internal const string EnableFailed = "Created unit file but enable failed: {0}";
        internal const string UnitFileRemoveFailed = "Failed to remove unit file for {0}: {1}";
        internal const string DaemonReloadAfterRemoveFailed = "Removed unit file but daemon-reload failed: {0}";

        internal static string Format(string template, params object[] args)
            => string.Format(CultureInfo.InvariantCulture, template, args);
    }
}
