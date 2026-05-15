// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Suspend, "Service", DefaultParameterSetName = "Name")]
    [OutputType(typeof(LinuxServiceInfo))]
    public sealed class SuspendServiceCommand : ServiceUnixBase
    {
        protected override void OperateOnService(string unitName)
        {
            WriteError(new ErrorRecord(
                new PlatformNotSupportedException(
                    "Suspend-Service is not supported on Linux. " +
                    "systemd has no pause/continue concept for services."),
                "PlatformNotSupported", ErrorCategory.NotImplemented, unitName));
        }
    }
}
