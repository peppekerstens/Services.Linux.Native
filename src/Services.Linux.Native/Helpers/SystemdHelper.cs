// Copyright (c) peppekerstens.
// Licensed under the MIT License.

using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.ServiceProcess;

using Tmds.DBus.Protocol;

namespace Microsoft.PowerShell.Commands
{
    internal static class SystemdDBus
    {
        internal const string Destination  = "org.freedesktop.systemd1";
        internal const string ManagerPath  = "/org/freedesktop/systemd1";
        internal const string ManagerIface = "org.freedesktop.systemd1.Manager";
        internal const string UnitIface    = "org.freedesktop.systemd1.Unit";
        internal const string PropsIface   = "org.freedesktop.DBus.Properties";
    }

    internal static class SystemdHelper
    {
        // PS runspace has no SynchronizationContext; blocking .GetAwaiter().GetResult() is safe here.
        internal static DBusConnection OpenSystem()
        {
            var conn = new DBusConnection(DBusAddress.System!);
            conn.ConnectAsync().GetAwaiter().GetResult();
            return conn;
        }

        internal static string ResolveUnitName(string name)
            => name.Contains('.') ? name : name + ".service";

        internal static IEnumerable<LinuxServiceController> GetServices(string[]? namePatterns)
            => GetServicesAsync(namePatterns).GetAwaiter().GetResult();

        private static async Task<List<LinuxServiceController>> GetServicesAsync(string[]? namePatterns)
        {
            using var conn = OpenSystem();
            var activeUnits = new Dictionary<string, LinuxServiceController>(StringComparer.OrdinalIgnoreCase);

            var msg1 = BuildCall(conn, "ListUnits");
            {
                var reply = await conn.CallMethodAsync(msg1, static (Message m, object? _) =>
                {
                    var list = new List<(string name, string desc, string active, string sub)>();
                    var reader = m.GetBodyReader();
                    var arrayEnd = reader.ReadArrayStart(DBusType.Struct);
                    while (reader.HasNext(arrayEnd))
                    {
                        reader.AlignStruct();
                        string name   = reader.ReadString();
                        string desc   = reader.ReadString();
                        reader.ReadString();
                        string active = reader.ReadString();
                        string sub    = reader.ReadString();
                        reader.ReadString();
                        reader.ReadObjectPath();
                        reader.ReadUInt32();
                        reader.ReadString();
                        reader.ReadObjectPath();
                        if (name.EndsWith(".service", StringComparison.OrdinalIgnoreCase))
                            list.Add((name, desc, active, sub));
                    }
                    return list;
                }).ConfigureAwait(false);

                foreach (var (name, desc, active, sub) in reply)
                {
                    activeUnits[name] = new LinuxServiceController(
                        serviceName: name,
                        displayName: desc,
                        status: LinuxServiceController.MapStatus(active, sub),
                        startType: ServiceStartupType.Manual,
                        activeState: active,
                        subState: sub);
                }
            }

            var msg2 = BuildCall(conn, "ListUnitFiles");
            {
                var reply = await conn.CallMethodAsync(msg2, static (Message m, object? _) =>
                {
                    var list = new List<(string file, string state)>();
                    var reader = m.GetBodyReader();
                    var arrayEnd = reader.ReadArrayStart(DBusType.Struct);
                    while (reader.HasNext(arrayEnd))
                    {
                        reader.AlignStruct();
                        string file  = reader.ReadString();
                        string state = reader.ReadString();
                    if (file.EndsWith(".service", StringComparison.OrdinalIgnoreCase)
                        && !file.Contains("@."))
                        list.Add((file, state));
                    }
                    return list;
                }).ConfigureAwait(false);

                foreach (var (file, state) in reply)
                {
                    string unitName = System.IO.Path.GetFileName(file);
                    var startType = LinuxServiceController.MapStartupType(state);
                    if (activeUnits.TryGetValue(unitName, out var existing))
                    {
                        activeUnits[unitName] = new LinuxServiceController(
                            serviceName: existing.ServiceName,
                            displayName: existing.DisplayName,
                            status: existing.Status,
                            startType: startType,
                            activeState: existing.ActiveState,
                            subState: existing.SubState);
                    }
                    else
                    {
                        activeUnits[unitName] = new LinuxServiceController(
                            serviceName: unitName,
                            displayName: unitName,
                            status: ServiceControllerStatus.Stopped,
                            startType: startType,
                            activeState: "inactive",
                            subState: "dead");
                    }
                }
            }

            var result = new List<LinuxServiceController>();
            foreach (var svc in activeUnits.Values)
            {
                if (MatchesPatterns(svc, namePatterns))
                    result.Add(svc);
            }
            return result;
        }

        private static bool MatchesPatterns(LinuxServiceController svc, string[]? patterns)
        {
            if (patterns is null || patterns.Length == 0) return true;
            foreach (var pattern in patterns)
            {
                if (WildcardPattern.ContainsWildcardCharacters(pattern))
                {
                    var wp = new WildcardPattern(pattern, WildcardOptions.IgnoreCase);
                    if (wp.IsMatch(svc.ServiceName))
                        return true;
                }
                else
                {
                    string bare = pattern.EndsWith(".service", StringComparison.OrdinalIgnoreCase)
                        ? pattern : pattern + ".service";
                    if (svc.ServiceName.Equals(pattern, StringComparison.OrdinalIgnoreCase)
                        || svc.ServiceName.Equals(bare,    StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        // PS runspace has no SynchronizationContext; blocking .GetAwaiter().GetResult() is safe here.
        internal static void StartUnit(string unitName)
        {
            using var conn = OpenSystem();
            StartUnit(conn, unitName);
        }

        internal static void StartUnit(DBusConnection conn, string unitName)
        {
            UnitActionAsync(conn, "StartUnit", unitName).GetAwaiter().GetResult();
        }

        // PS runspace has no SynchronizationContext; blocking .GetAwaiter().GetResult() is safe here.
        internal static void StopUnit(string unitName)
        {
            using var conn = OpenSystem();
            StopUnit(conn, unitName);
        }

        internal static void StopUnit(DBusConnection conn, string unitName)
        {
            UnitActionAsync(conn, "StopUnit", unitName).GetAwaiter().GetResult();
        }

        // PS runspace has no SynchronizationContext; blocking .GetAwaiter().GetResult() is safe here.
        internal static void RestartUnit(string unitName)
        {
            using var conn = OpenSystem();
            RestartUnit(conn, unitName);
        }

        internal static void RestartUnit(DBusConnection conn, string unitName)
        {
            UnitActionAsync(conn, "RestartUnit", unitName).GetAwaiter().GetResult();
        }

        private static async Task UnitActionAsync(DBusConnection conn, string method, string unitName)
        {
            var msg = BuildCallSS(conn, method, unitName, "replace");
            try
            {
                await conn.CallMethodAsync(msg, static (Message m, object? _) =>
                    m.GetBodyReader().ReadObjectPath()).ConfigureAwait(false);
            }
            catch (DBusExceptionBase ex) when (ex.Message.Contains("InteractiveAuthorizationRequired"))
            {
                throw new InvalidOperationException(
                    $"{method.Replace("Unit", string.Empty)} {unitName} failed: root privileges are required. Use 'sudo pwsh'.", ex);
            }
        }

        // PS runspace has no SynchronizationContext; blocking .GetAwaiter().GetResult() is safe here.
        internal static void EnableUnits(string[] unitNames)
        {
            using var conn = OpenSystem();
            EnableUnits(conn, unitNames);
        }

        internal static void EnableUnits(DBusConnection conn, string[] unitNames)
        {
            EnableUnitsAsync(conn, unitNames).GetAwaiter().GetResult();
        }

        private static async Task EnableUnitsAsync(DBusConnection conn, string[] unitNames)
        {
            var msg = BuildEnableMessage(conn, unitNames, runtime: false, force: false);
            try
            {
                await conn.CallMethodAsync(msg, static (Message m, object? _) => 0).ConfigureAwait(false);
            }
            catch (DBusExceptionBase ex) when (ex.Message.Contains("InteractiveAuthorizationRequired"))
            {
                throw new InvalidOperationException(
                    "EnableUnitFiles failed: root privileges are required. Use 'sudo pwsh'.", ex);
            }
        }

        // PS runspace has no SynchronizationContext; blocking .GetAwaiter().GetResult() is safe here.
        internal static void DisableUnits(string[] unitNames)
        {
            using var conn = OpenSystem();
            DisableUnits(conn, unitNames);
        }

        internal static void DisableUnits(DBusConnection conn, string[] unitNames)
        {
            DisableUnitsAsync(conn, unitNames).GetAwaiter().GetResult();
        }

        private static async Task DisableUnitsAsync(DBusConnection conn, string[] unitNames)
        {
            var msg = BuildDisableMessage(conn, unitNames, runtime: false);
            try
            {
                await conn.CallMethodAsync(msg, static (Message m, object? _) => 0).ConfigureAwait(false);
            }
            catch (DBusExceptionBase ex) when (ex.Message.Contains("InteractiveAuthorizationRequired"))
            {
                throw new InvalidOperationException(
                    "DisableUnitFiles failed: root privileges are required. Use 'sudo pwsh'.", ex);
            }
        }

        // PS runspace has no SynchronizationContext; blocking .GetAwaiter().GetResult() is safe here.
        internal static void DaemonReload()
        {
            using var conn = OpenSystem();
            DaemonReload(conn);
        }

        internal static void DaemonReload(DBusConnection conn)
        {
            var msg = BuildCall(conn, "Reload");
            try
            {
                conn.CallMethodAsync(msg, static (Message m, object? _) => 0).GetAwaiter().GetResult();
            }
            catch (DBusExceptionBase ex) when (ex.Message.Contains("InteractiveAuthorizationRequired"))
            {
                throw new InvalidOperationException(
                    "DaemonReload failed: root privileges are required. Use 'sudo pwsh'.", ex);
            }
        }

        internal static void WriteUnitFile(string unitName, string description, string execStart)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                throw new PlatformNotSupportedException("New-Service is not supported on Windows.");

            string unitDir = IsNonRoot() ? GetUserUnitDir() : "/etc/systemd/system/";
            Directory.CreateDirectory(unitDir);
            string unitPath = System.IO.Path.Combine(unitDir, unitName);
            var lines = new[]
            {
                "[Unit]",
                $"Description={description}",
                "",
                "[Service]",
                $"ExecStart={execStart}",
                "Restart=no",
                "",
                "[Install]",
                "WantedBy=multi-user.target"
            };
            System.IO.File.WriteAllLines(unitPath, lines);
        }

        internal static void RemoveUnitFile(string unitName)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                throw new PlatformNotSupportedException("Remove-Service is not supported on Windows.");

            string unitDir = IsNonRoot() ? GetUserUnitDir() : "/etc/systemd/system/";
            string unitPath = System.IO.Path.Combine(unitDir, unitName);
            if (System.IO.File.Exists(unitPath))
                System.IO.File.Delete(unitPath);
        }

        private static bool IsNonRoot()
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "id",
                    Arguments = "-u",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                })!;
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                return !output.Trim().Equals("0");
            }
            catch { return true; }
        }

        private static string GetUserUnitDir()
        {
            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? System.IO.Path.Combine(
                    Environment.GetEnvironmentVariable("HOME") ?? "/root",
                    ".config");
            return System.IO.Path.Combine(configHome, "systemd", "user");
        }

        private static MessageBuffer BuildCall(DBusConnection conn, string member)
        {
            using var writer = conn.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: SystemdDBus.Destination,
                path:        SystemdDBus.ManagerPath,
                @interface:  SystemdDBus.ManagerIface,
                member:      member);
            return writer.CreateMessage();
        }

        private static MessageBuffer BuildCallSS(DBusConnection conn, string member, string arg1, string arg2)
        {
            using var writer = conn.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: SystemdDBus.Destination,
                path:        SystemdDBus.ManagerPath,
                @interface:  SystemdDBus.ManagerIface,
                signature:   "ss",
                member:      member);
            writer.WriteString(arg1);
            writer.WriteString(arg2);
            return writer.CreateMessage();
        }

        private static MessageBuffer BuildEnableMessage(
            DBusConnection conn, string[] files, bool runtime, bool force)
        {
            using var writer = conn.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: SystemdDBus.Destination,
                path:        SystemdDBus.ManagerPath,
                @interface:  SystemdDBus.ManagerIface,
                signature:   "asbb",
                member:      "EnableUnitFiles");
            writer.WriteArray(files);
            writer.WriteBool(runtime);
            writer.WriteBool(force);
            return writer.CreateMessage();
        }

        private static MessageBuffer BuildDisableMessage(
            DBusConnection conn, string[] files, bool runtime)
        {
            using var writer = conn.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: SystemdDBus.Destination,
                path:        SystemdDBus.ManagerPath,
                @interface:  SystemdDBus.ManagerIface,
                signature:   "asb",
                member:      "DisableUnitFiles");
            writer.WriteArray(files);
            writer.WriteBool(runtime);
            return writer.CreateMessage();
        }
    }
}
