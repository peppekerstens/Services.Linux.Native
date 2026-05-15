// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Tmds.DBus.Protocol;

namespace Microsoft.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Get, "Service", DefaultParameterSetName = "Default")]
    [OutputType(typeof(LinuxServiceInfo))]
    public sealed class GetServiceCommand : PSCmdlet
    {
        [Parameter(Position = 0, ParameterSetName = "Default",
            ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("ServiceName")]
        public string[]? Name { get; set; }

        [Parameter(ParameterSetName = "InputObject", ValueFromPipeline = true)]
        public LinuxServiceInfo[]? InputObject { get; set; }

        private readonly List<string> _names = new();

        protected override void ProcessRecord()
        {
            if (InputObject is not null)
                foreach (var s in InputObject) _names.Add(s.Name);
            else if (Name is not null)
                _names.AddRange(Name);
        }

        protected override void EndProcessing()
        {
            string[]? patterns = _names.Count > 0 ? _names.ToArray() : null;
            try
            {
                foreach (var svc in SystemdHelper.GetServices(patterns))
                    WriteObject(svc);
            }
            catch (DBusExceptionBase ex)
            {
                WriteError(new ErrorRecord(ex, "DBusError", ErrorCategory.ResourceUnavailable, patterns));
            }
        }
    }
}
