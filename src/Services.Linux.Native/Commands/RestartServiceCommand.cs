// Copyright (c) peppekerstens.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Restart, "Service", DefaultParameterSetName = "Name",
        SupportsShouldProcess = true,
        HelpUri = "https://github.com/peppekerstens/Services.Linux.Native",
        RemotingCapability = RemotingCapability.SupportedByCommand)]
    [OutputType(typeof(LinuxServiceController))]
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
