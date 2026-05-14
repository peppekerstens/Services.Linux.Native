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
        public LinuxServiceInfo[]? InputObject { get; set; }

        [Parameter]
        public SwitchParameter PassThru { get; set; }

        protected override void ProcessRecord()
        {
            IEnumerable<LinuxServiceInfo> services;
            if (ParameterSetName == "InputObject" && InputObject is not null)
                services = SystemdHelper.GetServices(
                    System.Array.ConvertAll(InputObject, s => s.Name));
            else if (Name is not null)
                services = SystemdHelper.GetServices(Name);
            else
                return;

            foreach (var svc in services)
                OperateOnService(svc.Name);
        }

        protected abstract void OperateOnService(string unitName);

        protected void EmitServiceInfo(string unitName)
        {
            foreach (var svc in SystemdHelper.GetServices(new[] { unitName }))
                WriteObject(svc);
        }

        protected void WriteDBusError(string unitName, string operation, Exception ex)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException(
                    $"{operation} {unitName} failed: {ex.Message}", ex),
                "DBusOperationFailed", ErrorCategory.OperationStopped, unitName));
        }
    }
}
