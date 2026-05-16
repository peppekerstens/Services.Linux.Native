// Copyright (c) peppekerstens.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Stop, "Service", DefaultParameterSetName = "Name",
        SupportsShouldProcess = true,
        HelpUri = "https://github.com/peppekerstens/Services.Linux.Native",
        RemotingCapability = RemotingCapability.SupportedByCommand)]
    [OutputType(typeof(LinuxServiceController))]
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
