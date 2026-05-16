// Copyright (c) peppekerstens.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.Commands
{
    /// <summary>
    /// Indicates the current state of the service.
    /// Values match System.ServiceProcess.ServiceControllerStatus for cross-platform alignment.
    /// </summary>
    public enum ServiceControllerStatus
    {
        Stopped = 1,
        StartPending = 2,
        StopPending = 3,
        Running = 4,
        ContinuePending = 5,
        PausePending = 6,
        Paused = 7,
    }
}
