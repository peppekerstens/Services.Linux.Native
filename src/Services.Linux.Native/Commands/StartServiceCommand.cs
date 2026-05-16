// Copyright (c) peppekerstens.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Start, "Service", DefaultParameterSetName = "Name",
        SupportsShouldProcess = true)]
    [OutputType(typeof(LinuxServiceController))]
    public sealed class StartServiceCommand : ServiceUnixBase
    {
        protected override void OperateOnService(string unitName)
        {
            if (!ShouldProcess(unitName, "Start")) return;
            try   { SystemdHelper.StartUnit(unitName); }
            catch (Exception ex) { WriteDBusError(unitName, "Start", ex); return; }
            if (PassThru) EmitServiceInfo(unitName);
        }
    }
}
