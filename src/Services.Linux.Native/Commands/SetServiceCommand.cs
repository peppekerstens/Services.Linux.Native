// Copyright (c) peppekerstens.
// Licensed under the MIT License.

using System.Management.Automation;

using Tmds.DBus.Protocol;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Set, "Service", DefaultParameterSetName = "Name",
        SupportsShouldProcess = true,
        HelpUri = "https://github.com/peppekerstens/Services.Linux.Native",
        RemotingCapability = RemotingCapability.SupportedByCommand)]
    [OutputType(typeof(LinuxServiceController))]
    public sealed class SetServiceCommand : PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Name",
            ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string? Name { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "InputObject",
            ValueFromPipeline = true)]
        public LinuxServiceController? InputObject { get; set; }

        [Parameter]
        public ServiceStartupType StartupType { get; set; } = ServiceStartupType.InvalidValue;

        [Parameter]
        [ValidateSet("Running", "Stopped")]
        public string? Status { get; set; }

        [Parameter]
        public SwitchParameter PassThru { get; set; }

        protected override void ProcessRecord()
        {
            if (OperatingSystem.IsWindows())
            {
                InvokeCommand.InvokeScript("Microsoft.PowerShell.Management\\Set-Service");
                return;
            }

            string rawName  = ParameterSetName == "InputObject" ? InputObject!.ServiceName : Name!;
            string unitName = SystemdHelper.ResolveUnitName(rawName);

            if (!ShouldProcess(unitName, "Set")) return;

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
                            ErrorMessages.Format(ErrorMessages.StartupTypeChangeFailed, unitName, ex.Message), ex),
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
                            ErrorMessages.Format(ErrorMessages.StatusChangeFailed, unitName, ex.Message), ex),
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
