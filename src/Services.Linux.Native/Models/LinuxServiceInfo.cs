// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.PowerShell.Commands
{
    public sealed class LinuxServiceInfo
    {
        public string Name { get; internal set; } = string.Empty;

        public string DisplayName { get; internal set; } = string.Empty;

        public string Status { get; internal set; } = "Stopped";

        public ServiceStartupType StartType { get; internal set; }

        public string ActiveState { get; internal set; } = string.Empty;

        public string SubState { get; internal set; } = string.Empty;

        internal static string MapStatus(string activeState, string subState)
            => (activeState, subState) switch
            {
                ("active", "running")     => "Running",
                ("active", _)             => "StartPending",
                ("activating", _)         => "StartPending",
                ("deactivating", _)       => "StopPending",
                ("reloading", _)          => "ContinuePending",
                _                         => "Stopped",
            };

        internal static ServiceStartupType MapStartupType(string unitFileState)
            => unitFileState switch
            {
                "enabled"         => ServiceStartupType.Automatic,
                "enabled-runtime" => ServiceStartupType.Automatic,
                "static"          => ServiceStartupType.Manual,
                "disabled"        => ServiceStartupType.Disabled,
                "masked"          => ServiceStartupType.Disabled,
                _                 => ServiceStartupType.Manual,
            };
    }
}
