// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

using Tmds.DBus.Protocol;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Set, "Service", DefaultParameterSetName = "Name",
        SupportsShouldProcess = true)]
    [OutputType(typeof(LinuxServiceInfo))]
    public sealed class SetServiceCommand : PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Name",
            ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string? Name { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "InputObject",
            ValueFromPipeline = true)]
        public LinuxServiceInfo? InputObject { get; set; }

        [Parameter]
        public ServiceStartupType StartupType { get; set; } = ServiceStartupType.InvalidValue;

        [Parameter]
        [ValidateSet("Running", "Stopped")]
        public string? Status { get; set; }

        [Parameter]
        public SwitchParameter PassThru { get; set; }

        protected override void ProcessRecord()
        {
            string rawName  = ParameterSetName == "InputObject" ? InputObject!.Name : Name!;
            string unitName = SystemdHelper.ResolveUnitName(rawName);

            if (!ShouldProcess(unitName, "Set")) return;

            if (!Utils.IsAdministrator())
            {
                WriteError(new ErrorRecord(
                    new PSSecurityException($"{MyInvocation.MyCommand.Name} requires root privileges."),
                    "ElevationRequired", ErrorCategory.PermissionDenied, unitName));
                return;
            }

            using DBusConnection conn = SystemdHelper.OpenSystem();

            if (StartupType != ServiceStartupType.InvalidValue)
            {
                try
                {
                    if (StartupType == ServiceStartupType.Disabled)
                        SystemdHelper.DisableUnits(conn, new[] { unitName });
                    else
                        SystemdHelper.EnableUnits(conn, new[] { unitName });
                }
                catch (Exception ex)
                {
                    WriteError(new ErrorRecord(
                        new InvalidOperationException(
                            $"Could not change startup type for {unitName}: {ex.Message}", ex),
                        "DBusOperationFailed", ErrorCategory.OperationStopped, unitName));
                    return;
                }
            }

            if (!string.IsNullOrEmpty(Status))
            {
                try
                {
                    if (Status == "Running")
                        SystemdHelper.StartUnit(conn, unitName);
                    else
                        SystemdHelper.StopUnit(conn, unitName);
                }
                catch (Exception ex)
                {
                    WriteError(new ErrorRecord(
                        new InvalidOperationException(
                            $"Could not set status for {unitName}: {ex.Message}", ex),
                        "DBusOperationFailed", ErrorCategory.OperationStopped, unitName));
                    return;
                }
            }

            if (PassThru)
            {
                foreach (var svc in SystemdHelper.GetServices(new[] { unitName }))
                    WriteObject(svc);
            }
        }
    }
}
