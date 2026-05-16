// Copyright (c) peppekerstens.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.PowerShell.Commands
{
    public abstract class ServiceUnixBase : PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Name",
            ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string[]? Name { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "InputObject",
            ValueFromPipeline = true)]
        public LinuxServiceController[]? InputObject { get; set; }

        [Parameter]
        public SwitchParameter PassThru { get; set; }

        protected override void ProcessRecord()
        {
            if (OperatingSystem.IsWindows())
            {
                string cmdletName = MyInvocation.MyCommand.Name;
                InvokeCommand.InvokeScript($"Microsoft.PowerShell.Management\\{cmdletName}");
                return;
            }

            IEnumerable<string> unitNames;
            if (ParameterSetName == "InputObject" && InputObject is not null)
                unitNames = System.Array.ConvertAll(InputObject, s => s.ServiceName);
            else if (Name is not null)
                unitNames = System.Array.ConvertAll(Name, SystemdHelper.ResolveUnitName);
            else
                return;

            foreach (var name in unitNames)
                OperateOnService(name);
        }

        protected abstract void OperateOnService(string unitName);

        protected void EmitServiceInfo(string unitName)
        {
            foreach (var svc in SystemdHelper.GetServices(new[] { unitName }))
                WriteObject(svc);
        }

        protected void WriteDBusError(string unitName, string operation, Exception ex)
        {
            if (ex is InvalidOperationException && ex.Message.Contains("root privileges"))
            {
                string cmdletName = $"{operation}-Service";
                WriteError(new ErrorRecord(
                    new PSSecurityException($"{cmdletName} requires root privileges."),
                    "ElevationRequired", ErrorCategory.PermissionDenied, unitName));
                return;
            }
            WriteError(new ErrorRecord(
                new InvalidOperationException(
                    $"{operation} {unitName} failed: {ex.Message}", ex),
                "DBusOperationFailed", ErrorCategory.OperationStopped, unitName));
        }
    }
}
