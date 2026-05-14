using System.Management.Automation;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Resume, "Service", DefaultParameterSetName = "Name",
        SupportsShouldProcess = true)]
    [OutputType(typeof(LinuxServiceInfo))]
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
