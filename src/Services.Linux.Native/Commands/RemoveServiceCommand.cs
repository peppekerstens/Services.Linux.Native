// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

using Tmds.DBus.Protocol;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Remove, "Service", SupportsShouldProcess = true,
        ConfirmImpact = ConfirmImpact.High)]
    public sealed class RemoveServiceCommand : PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0,
            ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; } = string.Empty;

        protected override void ProcessRecord()
        {
            string unitName = SystemdHelper.ResolveUnitName(Name);

            if (!ShouldProcess(unitName, "Stop, disable, and delete systemd service unit")) return;

            using DBusConnection conn = SystemdHelper.OpenSystem();

            try { SystemdHelper.StopUnit(conn, unitName); }
            catch (Exception) { }

            try { SystemdHelper.DisableUnits(conn, new[] { unitName }); }
            catch (Exception) { }

            try { SystemdHelper.RemoveUnitFile(unitName); }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException(
                        $"Failed to remove unit file for {unitName}: {ex.Message}", ex),
                    "UnitFileRemoveFailed", ErrorCategory.WriteError, unitName));
                return;
            }

            try { SystemdHelper.DaemonReload(conn); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("root privileges"))
            {
                WriteError(new ErrorRecord(
                    new PSSecurityException($"{MyInvocation.MyCommand.Name} requires root privileges."),
                    "ElevationRequired", ErrorCategory.PermissionDenied, unitName));
                return;
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException(
                        $"Removed unit file but daemon-reload failed: {ex.Message}", ex),
                    "DaemonReloadFailed", ErrorCategory.OperationStopped, unitName));
            }
        }
    }
}
