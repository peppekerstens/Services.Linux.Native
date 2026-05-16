// Copyright (c) peppekerstens.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.Commands
{
    /// <summary>
    /// Indicates the systemd unit type.
    /// </summary>
    public enum LinuxServiceType
    {
        Simple,
        Forking,
        Oneshot,
        DBus,
        Notify,
        Idle,
        Exec,
        Unknown,
    }
}
