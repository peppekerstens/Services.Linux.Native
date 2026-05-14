using System.Management.Automation;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Stop, "Service", DefaultParameterSetName = "Name",
        SupportsShouldProcess = true)]
    [OutputType(typeof(LinuxServiceInfo))]
    public sealed class StopServiceCommand : ServiceUnixBase
    {
        protected override void OperateOnService(string unitName)
        {
            if (!ShouldProcess(unitName, "Stop")) return;
            try   { SystemdHelper.StopUnit(unitName); }
            catch (Exception ex) { WriteDBusError(unitName, "Stop", ex); return; }
            if (PassThru) EmitServiceInfo(unitName);
        }
    }
}
