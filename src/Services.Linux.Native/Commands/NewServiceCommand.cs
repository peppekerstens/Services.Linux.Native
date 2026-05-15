// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

using Tmds.DBus.Protocol;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.New, "Service", SupportsShouldProcess = true)]
    [OutputType(typeof(LinuxServiceInfo))]
    public sealed class NewServiceCommand : PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; } = string.Empty;

        [Parameter(Mandatory = true)]
        public string BinaryPathName { get; set; } = string.Empty;

        [Parameter]
        public string Description { get; set; } = string.Empty;

        [Parameter]
        public ServiceStartupType StartupType { get; set; } = ServiceStartupType.Manual;

        protected override void ProcessRecord()
        {
            string unitName = SystemdHelper.ResolveUnitName(Name);

            if (!ShouldProcess(unitName, "Create systemd service unit")) return;

            try
            {
                SystemdHelper.WriteUnitFile(unitName, Description, BinaryPathName);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException(
                        $"Failed to create unit file for {unitName}: {ex.Message}", ex),
                    "UnitFileCreateFailed", ErrorCategory.WriteError, unitName));
                return;
            }

            using DBusConnection conn = SystemdHelper.OpenSystem();

            try
            {
                SystemdHelper.DaemonReload(conn);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException(
                        $"Created unit file but daemon-reload failed: {ex.Message}", ex),
                    "DaemonReloadFailed", ErrorCategory.OperationStopped, unitName));
                return;
            }

            if (StartupType == ServiceStartupType.Automatic)
            {
                try { SystemdHelper.EnableUnits(conn, new[] { unitName }); }
                catch (Exception ex)
                {
                    WriteError(new ErrorRecord(
                        new InvalidOperationException(
                            $"Created unit file but enable failed: {ex.Message}", ex),
                        "EnableFailed", ErrorCategory.OperationStopped, unitName));
                }
            }

            WriteObject(new LinuxServiceInfo
            {
                Name        = unitName,
                DisplayName = string.IsNullOrEmpty(Description) ? Name : Description,
                Status      = "Stopped",
                StartType   = StartupType,
                ActiveState = "inactive",
                SubState    = "dead",
            });
        }
    }
}
