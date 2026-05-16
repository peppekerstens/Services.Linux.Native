// Copyright (c) peppekerstens.
// Licensed under the MIT License.

using System.Management.Automation;

using Tmds.DBus.Protocol;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.New, "Service",
        SupportsShouldProcess = true,
        HelpUri = "https://learn.microsoft.com/powershell/module/microsoft.powershell.management/new-service",
        RemotingCapability = RemotingCapability.SupportedByCommand)]
    [OutputType(typeof(LinuxServiceController))]
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
            if (OperatingSystem.IsWindows())
            {
                InvokeCommand.InvokeScript("Microsoft.PowerShell.Management\\New-Service");
                return;
            }

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
                        ErrorMessages.Format(ErrorMessages.UnitFileCreateFailed, unitName, ex.Message), ex),
                    "UnitFileCreateFailed", ErrorCategory.WriteError, unitName));
                return;
            }

            using DBusConnection conn = SystemdHelper.OpenSystem();

            try
            {
                SystemdHelper.DaemonReload(conn);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("root privileges"))
            {
                WriteError(new ErrorRecord(
                    new PSSecurityException(ErrorMessages.Format(ErrorMessages.ElevationRequired, MyInvocation.MyCommand.Name)),
                    "ElevationRequired", ErrorCategory.PermissionDenied, unitName));
                return;
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException(
                        ErrorMessages.Format(ErrorMessages.DaemonReloadFailed, ex.Message), ex),
                    "DaemonReloadFailed", ErrorCategory.OperationStopped, unitName));
                return;
            }

            if (StartupType == ServiceStartupType.Automatic)
            {
                try { SystemdHelper.EnableUnits(conn, new[] { unitName }); }
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
                            ErrorMessages.Format(ErrorMessages.EnableFailed, ex.Message), ex),
                        "EnableFailed", ErrorCategory.OperationStopped, unitName));
                }
            }

            WriteObject(new LinuxServiceController(
                serviceName: unitName,
                displayName: string.IsNullOrEmpty(Description) ? Name : Description,
                status: "Stopped",
                startType: StartupType,
                activeState: "inactive",
                subState: "dead"));
        }
    }
}
