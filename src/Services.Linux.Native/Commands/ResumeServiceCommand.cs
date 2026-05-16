// Copyright (c) peppekerstens.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Resume, "Service", DefaultParameterSetName = "Name",
        HelpUri = "https://learn.microsoft.com/powershell/module/microsoft.powershell.management/resume-service",
        RemotingCapability = RemotingCapability.SupportedByCommand)]
    [OutputType(typeof(LinuxServiceController))]
    public sealed class ResumeServiceCommand : ServiceUnixBase
    {
        protected override void OperateOnService(string unitName)
        {
            WriteError(new ErrorRecord(
                new PlatformNotSupportedException(
                    "Resume-Service is not supported on Linux. " +
                    "systemd has no pause/continue concept for services."),
                "PlatformNotSupported", ErrorCategory.NotImplemented, unitName));
        }
    }
}
