using System.Management.Automation;

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

            if (StartupType != ServiceStartupType.InvalidValue)
            {
                try
                {
                    if (StartupType == ServiceStartupType.Disabled)
                        SystemdHelper.DisableUnits(new[] { unitName });
                    else
                        SystemdHelper.EnableUnits(new[] { unitName });
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
                        SystemdHelper.StartUnit(unitName);
                    else
                        SystemdHelper.StopUnit(unitName);
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
