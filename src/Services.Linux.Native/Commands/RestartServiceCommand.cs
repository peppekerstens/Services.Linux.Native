// Copyright (c) peppekerstens.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Restart, "Service", DefaultParameterSetName = "Name",
        SupportsShouldProcess = true,
        HelpUri = "https://learn.microsoft.com/powershell/module/microsoft.powershell.management/restart-service",
        RemotingCapability = RemotingCapability.SupportedByCommand)]
    [OutputType(typeof(LinuxServiceController))]
    public sealed class RestartServiceCommand : ServiceUnixBase
    {
        protected override void OperateOnService(string unitName)
        {
            if (!ShouldProcess(FormatShouldProcessTarget(unitName), "Restart")) return;
            try   { SystemdHelper.RestartUnit(unitName); }
            catch (Exception ex) { WriteDBusError(unitName, "Restart", ex); return; }
            if (PassThru) EmitServiceInfo(unitName);
        }
    }
}
