// Copyright (c) peppekerstens.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.PowerShell.Commands
{
    /// <summary>
    /// Represents a systemd service on Linux. Inherits from Component to align
    /// with Windows ServiceController's type hierarchy (Rule 9).
    /// </summary>
    public sealed class LinuxServiceController : Component
    {
        private string _activeState = string.Empty;
        private string _subState = string.Empty;
        private string _unitFileState = string.Empty;
        private LinuxServiceController[]? _dependentServices;
        private LinuxServiceController[]? _servicesDependedOn;
        private string? _serviceType;
        private LinuxServiceType? _unitType;
        private bool _refreshed;

        /// <summary>
        /// Initializes a new instance with pre-loaded values from enumeration.
        /// </summary>
        public LinuxServiceController(
            string serviceName,
            string displayName,
            string status,
            ServiceStartupType startType,
            string activeState,
            string subState)
            : this(serviceName, displayName, MapStatus(status), startType, activeState, subState)
        {
        }

        /// <summary>
        /// Initializes a new instance with pre-loaded values from enumeration.
        /// </summary>
        public LinuxServiceController(
            string serviceName,
            string displayName,
            ServiceControllerStatus status,
            ServiceStartupType startType,
            string activeState,
            string subState)
        {
            ServiceName = serviceName;
            DisplayName = displayName;
            Status = status;
            StartType = startType;
            _activeState = activeState;
            _subState = subState;
            _unitFileState = MapStartupTypeToUnitFileState(startType);
            _refreshed = true;
        }

        /// <summary>
        /// The name of the service (systemd unit name).
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// The short name of the service (ServiceName without the .service suffix).
        /// </summary>
        public string Name => ServiceName.EndsWith(".service", StringComparison.Ordinal)
            ? ServiceName.Substring(0, ServiceName.Length - 8)
            : ServiceName;

        /// <summary>
        /// The display name (description) of the service.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// The current status of the service.
        /// </summary>
        public ServiceControllerStatus Status { get; private set; }

        /// <summary>
        /// The startup type of the service.
        /// </summary>
        public ServiceStartupType StartType { get; private set; }

        /// <summary>
        /// The systemd active state (e.g. "active", "inactive", "failed").
        /// </summary>
        public string ActiveState
        {
            get
            {
                EnsureRefreshed();
                return _activeState;
            }
        }

        /// <summary>
        /// The systemd sub state (e.g. "running", "dead", "exited").
        /// </summary>
        public string SubState
        {
            get
            {
                EnsureRefreshed();
                return _subState;
            }
        }

        /// <summary>
        /// Whether the service can be stopped.
        /// </summary>
        public bool CanStop
        {
            get
            {
                string state = ActiveState;
                return state is not ("inactive" or "failed" or "maintenance");
            }
        }

        /// <summary>
        /// systemd has no pause/continue concept for services.
        /// </summary>
        public bool CanPauseAndContinue => false;

        /// <summary>
        /// systemd has no shutdown signal for services.
        /// </summary>
        public bool CanShutdown => false;

        /// <summary>
        /// Services that depend on this service (reverse dependencies).
        /// </summary>
        public LinuxServiceController[] DependentServices
        {
            get
            {
                if (_dependentServices is null)
                {
                    string output = RunSystemctl($"list-dependencies --reverse --plain --no-pager --no-legend {ServiceName}");
                    var names = output
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Trim())
                        .Where(name => name.EndsWith(".service", StringComparison.Ordinal) && name != ServiceName)
                        .ToArray();
                    _dependentServices = names.Select(n => new LinuxServiceController(n, n, "Unknown", ServiceStartupType.InvalidValue, "unknown", "unknown")).ToArray();
                }
                return _dependentServices;
            }
        }

        /// <summary>
        /// Services that this service depends on (forward dependencies).
        /// </summary>
        public LinuxServiceController[] ServicesDependedOn
        {
            get
            {
                if (_servicesDependedOn is null)
                {
                    string output = RunSystemctl($"list-dependencies --plain --no-pager --no-legend {ServiceName}");
                    var names = output
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Trim())
                        .Where(name => name.EndsWith(".service", StringComparison.Ordinal) && name != ServiceName)
                        .ToArray();
                    _servicesDependedOn = names.Select(n => new LinuxServiceController(n, n, "Unknown", ServiceStartupType.InvalidValue, "unknown", "unknown")).ToArray();
                }
                return _servicesDependedOn;
            }
        }

        /// <summary>
        /// The systemd unit type (e.g. "simple", "forking", "oneshot").
        /// </summary>
        public ServiceType ServiceType
        {
            get
            {
                if (_serviceType is null)
                {
                    string output = RunSystemctl($"show -p Type --value --no-pager {ServiceName}");
                    _serviceType = output.Trim();
                }
                return _serviceType switch
                {
                    "simple"   => ServiceType.Win32OwnProcess,
                    "forking"  => ServiceType.Win32ShareProcess,
                    "oneshot"  => ServiceType.Win32OwnProcess,
                    "dbus"     => ServiceType.Win32OwnProcess,
                    "notify"   => ServiceType.Win32OwnProcess,
                    "idle"     => ServiceType.Win32OwnProcess,
                    _          => ServiceType.InteractiveProcess,
                };
            }
        }

        /// <summary>
        /// The systemd unit type (e.g. Simple, Forking, Oneshot).
        /// Uses Linux-native enum values instead of Windows ServiceType.
        /// </summary>
        public LinuxServiceType UnitType
        {
            get
            {
                if (_unitType is null)
                {
                    string output = RunSystemctl($"show -p Type --value --no-pager {ServiceName}");
                    string type = output.Trim();
                    _unitType = type switch
                    {
                        "simple"   => LinuxServiceType.Simple,
                        "forking"  => LinuxServiceType.Forking,
                        "oneshot"  => LinuxServiceType.Oneshot,
                        "dbus"     => LinuxServiceType.DBus,
                        "notify"   => LinuxServiceType.Notify,
                        "idle"     => LinuxServiceType.Idle,
                        "exec"     => LinuxServiceType.Exec,
                        _          => LinuxServiceType.Unknown,
                    };
                }
                return _unitType.Value;
            }
        }

        /// <summary>
        /// Always returns "." (localhost).
        /// </summary>
        public string MachineName => ".";

        /// <summary>
        /// Starts the service.
        /// </summary>
        public void Start()
        {
            SystemdHelper.StartUnit(ServiceName);
        }

        /// <summary>
        /// Starts the service. Arguments are ignored on Linux (systemd does not
        /// support passing arguments to services at start time).
        /// </summary>
        public void Start(string[] args)
        {
            // args are ignored on Linux; systemd unit file defines ExecStart.
            SystemdHelper.StartUnit(ServiceName);
        }

        /// <summary>
        /// Stops the service.
        /// </summary>
        public void Stop()
        {
            SystemdHelper.StopUnit(ServiceName);
        }

        /// <summary>
        /// Stops the service. The dep parameter is ignored on Linux.
        /// </summary>
        public void Stop(bool dep)
        {
            SystemdHelper.StopUnit(ServiceName);
        }

        /// <summary>
        /// Refreshes all cached property values by re-querying systemd.
        /// </summary>
        public void Refresh()
        {
            _refreshed = false;
            _dependentServices = null;
            _servicesDependedOn = null;
            _serviceType = null;
            _unitType = null;
        }

        /// <summary>
        /// systemd has no pause concept for services.
        /// </summary>
        public void Pause()
        {
            throw new PlatformNotSupportedException(
                "Pause is not supported on Linux. systemd has no pause/continue concept for services.");
        }

        /// <summary>
        /// systemd has no continue concept for services.
        /// </summary>
        public void Continue()
        {
            throw new PlatformNotSupportedException(
                "Continue is not supported on Linux. systemd has no pause/continue concept for services.");
        }

        /// <summary>
        /// No equivalent on Linux.
        /// </summary>
        public void ExecuteCommand(int command)
        {
            throw new PlatformNotSupportedException(
                "ExecuteCommand is not supported on Linux.");
        }

        /// <summary>
        /// Waits for the service to reach the specified status.
        /// </summary>
        public void WaitForStatus(ServiceControllerStatus status)
        {
            WaitForStatus(status, TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Waits for the service to reach the specified status within the timeout.
        /// </summary>
        public void WaitForStatus(ServiceControllerStatus status, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                Refresh();
                if (Status == status)
                    return;
                Thread.Sleep(100);
            }
            throw new TimeoutException(string.Format(
                CultureInfo.InvariantCulture,
                "Service '{0}' did not reach status '{1}' within {2}.",
                ServiceName, status, timeout));
        }

        /// <summary>
        /// Returns the service name.
        /// </summary>
        public override string ToString() => ServiceName;

        private void EnsureRefreshed()
        {
            if (!_refreshed)
            {
                string output = RunSystemctl($"show -p ActiveState,SubState,UnitFileState --value --no-pager {ServiceName}");
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length >= 3)
                {
                    _activeState = lines[0].Trim();
                    _subState = lines[1].Trim();
                    _unitFileState = lines[2].Trim();
                    Status = MapStatusFromActiveState(_activeState);
                    StartType = MapStartupType(_unitFileState);
                }
                _refreshed = true;
            }
        }

        private static string RunSystemctl(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "systemctl",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var arg in arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                psi.ArgumentList.Add(arg);
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start systemctl {arguments}.");
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(10000);
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"systemctl {arguments} failed (exit {process.ExitCode}): {stderr.Trim()}");
            return stdout;
        }

        private static ServiceControllerStatus MapStatus(string status)
            => status switch
            {
                "Running"         => ServiceControllerStatus.Running,
                "StartPending"    => ServiceControllerStatus.StartPending,
                "StopPending"     => ServiceControllerStatus.StopPending,
                "PausePending"    => ServiceControllerStatus.PausePending,
                "ContinuePending" => ServiceControllerStatus.ContinuePending,
                _                 => ServiceControllerStatus.Stopped,
            };

        /// <summary>
        /// Maps systemd ActiveState + SubState to a status string (used by SystemdHelper).
        /// </summary>
        internal static string MapStatus(string activeState, string subState)
            => (activeState, subState) switch
            {
                ("active", "running")     => "Running",
                ("active", _)             => "StartPending",
                ("activating", _)         => "StartPending",
                ("deactivating", _)       => "StopPending",
                ("reloading", _)          => "ContinuePending",
                _                         => "Stopped",
            };

        /// <summary>
        /// Maps systemd UnitFileState to ServiceStartupType (used by SystemdHelper).
        /// </summary>
        internal static ServiceStartupType MapStartupType(string unitFileState)
            => unitFileState switch
            {
                "enabled"         => ServiceStartupType.Automatic,
                "enabled-runtime" => ServiceStartupType.Automatic,
                "static"          => ServiceStartupType.Manual,
                "disabled"        => ServiceStartupType.Disabled,
                "masked"          => ServiceStartupType.Disabled,
                _                 => ServiceStartupType.Manual,
            };

        private static ServiceControllerStatus MapStatusFromActiveState(string activeState)
            => activeState switch
            {
                "active"     => ServiceControllerStatus.Running,
                "activating" => ServiceControllerStatus.StartPending,
                "deactivating" => ServiceControllerStatus.StopPending,
                "reloading"  => ServiceControllerStatus.ContinuePending,
                _            => ServiceControllerStatus.Stopped,
            };

        private static string MapStartupTypeToUnitFileState(ServiceStartupType startType)
            => startType switch
            {
                ServiceStartupType.Automatic          => "enabled",
                ServiceStartupType.AutomaticDelayedStart => "enabled",
                ServiceStartupType.Manual             => "static",
                ServiceStartupType.Disabled           => "disabled",
                _                                     => "static",
            };
    }
}
