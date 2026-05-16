// Copyright (c) peppekerstens.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.Commands
{
    /// <summary>
    /// Indicates the type of service.
    /// Values match System.ServiceProcess.ServiceType for cross-platform alignment.
    /// </summary>
    [System.Flags]
    public enum ServiceType
    {
        Adapter = 4,
        FileSystemDriver = 2,
        InteractiveProcess = 256,
        KernelDriver = 1,
        RecognizerDriver = 8,
        Win32OwnProcess = 16,
        Win32ShareProcess = 32,
    }
}
