// Copyright (c) peppekerstens.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.Commands
{
    /// <summary>
    /// Indicates the type of a Linux systemd service.
    /// Values are distinct from Windows ServiceType to avoid cross-platform confusion.
    /// </summary>
    public enum ServiceType
    {
        Simple = 1001,
        Forking = 1002,
        Oneshot = 1003,
        DBus = 1004,
        Notify = 1005,
        Idle = 1006,
        Exec = 1007,
        Unknown = 1000,
    }
}
