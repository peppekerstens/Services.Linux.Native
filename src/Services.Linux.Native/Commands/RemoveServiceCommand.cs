// Copyright (c) peppekerstens.
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
            if (OperatingSystem.IsWindows())
            {
                InvokeCommand.InvokeScript("Microsoft.PowerShell.Management\\Remove-Service");
                return;
            }

            string unitName = SystemdHelper.ResolveUnitName(Name);

            if (!ShouldProcess(unitName, "Stop, disable, and delete systemd service unit")) return;

            using DBusConnection conn = SystemdHelper.OpenSystem();

            try { SystemdHelper.StopUnit(conn, unitName); }
            catch (Exception ex)
            {
                // Best-effort stop before deletion — non-terminating.
                WriteWarning(ErrorMessages.Format(ErrorMessages.UnitOperationFailed, "Stop", unitName, ex.Message));
            }

            try { SystemdHelper.DisableUnits(conn, new[] { unitName }); }
            catch (Exception ex)
            {
                // Best-effort disable before deletion — non-terminating.
                WriteWarning(ErrorMessages.Format(ErrorMessages.UnitOperationFailed, "Disable", unitName, ex.Message));
            }

            try { SystemdHelper.RemoveUnitFile(unitName); }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException(
                        ErrorMessages.Format(ErrorMessages.UnitFileRemoveFailed, unitName, ex.Message), ex),
                    "UnitFileRemoveFailed", ErrorCategory.WriteError, unitName));
                return;
            }

            try { SystemdHelper.DaemonReload(conn); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("root privileges"))
            {
                WriteError(new ErrorRecord(
                    new PSSecurityException(ErrorMessages.Format(ErrorMessages.ElevationRequired, MyInvocation.MyCommand.Name)),
                                            "UnauthorizedAccess", ErrorCategory.SecurityError, unitName));
                return;
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException(
                        ErrorMessages.Format(ErrorMessages.DaemonReloadAfterRemoveFailed, ex.Message), ex),
                    "DaemonReloadFailed", ErrorCategory.OperationStopped, unitName));
            }
        }
    }
}
