using System.Management.Automation;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Restart, "Service", DefaultParameterSetName = "Name",
        SupportsShouldProcess = true)]
    [OutputType(typeof(LinuxServiceInfo))]
    public sealed class RestartServiceCommand : ServiceUnixBase
    {
        protected override void OperateOnService(string unitName)
        {
            if (!ShouldProcess(unitName, "Restart")) return;
            try   { SystemdHelper.RestartUnit(unitName); }
            catch (Exception ex) { WriteDBusError(unitName, "Restart", ex); return; }
            if (PassThru) EmitServiceInfo(unitName);
        }
    }
}
