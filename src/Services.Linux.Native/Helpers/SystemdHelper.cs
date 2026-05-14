using System.Management.Automation;

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

        internal const int LU_Name        = 0;
        internal const int LU_Desc        = 1;
        internal const int LU_LoadState   = 2;
        internal const int LU_ActiveState = 3;
        internal const int LU_SubState    = 4;
    }

    internal static class SystemdHelper
    {
        private static DBusConnection OpenSystem()
        {
            var conn = new DBusConnection(DBusAddress.System!);
            conn.ConnectAsync().GetAwaiter().GetResult();
            return conn;
        }

        internal static string ResolveUnitName(string name)
            => name.Contains('.') ? name : name + ".service";

        internal static IEnumerable<LinuxServiceInfo> GetServices(string[]? namePatterns)
            => GetServicesAsync(namePatterns).GetAwaiter().GetResult();

        private static async Task<List<LinuxServiceInfo>> GetServicesAsync(string[]? namePatterns)
        {
            using var conn = OpenSystem();
            var activeUnits = new Dictionary<string, LinuxServiceInfo>(StringComparer.OrdinalIgnoreCase);

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
                });

                foreach (var (name, desc, active, sub) in reply)
                {
                    activeUnits[name] = new LinuxServiceInfo
                    {
                        Name        = name,
                        DisplayName = desc,
                        ActiveState = active,
                        SubState    = sub,
                        Status      = LinuxServiceInfo.MapStatus(active, sub),
                        StartType   = ServiceStartupType.Manual,
                    };
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
                        if (file.EndsWith(".service", StringComparison.OrdinalIgnoreCase))
                            list.Add((file, state));
                    }
                    return list;
                });

                foreach (var (file, state) in reply)
                {
                    string unitName = System.IO.Path.GetFileName(file);
                    var startType = LinuxServiceInfo.MapStartupType(state);
                    if (activeUnits.TryGetValue(unitName, out var existing))
                    {
                        existing.StartType = startType;
                    }
                    else
                    {
                        activeUnits[unitName] = new LinuxServiceInfo
                        {
                            Name        = unitName,
                            DisplayName = unitName,
                            ActiveState = "inactive",
                            SubState    = "dead",
                            Status      = "Stopped",
                            StartType   = startType,
                        };
                    }
                }
            }

            var result = new List<LinuxServiceInfo>();
            foreach (var svc in activeUnits.Values)
            {
                if (MatchesPatterns(svc, namePatterns))
                    result.Add(svc);
            }
            return result;
        }

        private static bool MatchesPatterns(LinuxServiceInfo svc, string[]? patterns)
        {
            if (patterns is null || patterns.Length == 0) return true;
            foreach (var pattern in patterns)
            {
                if (WildcardPattern.ContainsWildcardCharacters(pattern))
                {
                    var wp = new WildcardPattern(pattern, WildcardOptions.IgnoreCase);
                    if (wp.IsMatch(svc.Name))
                        return true;
                }
                else
                {
                    string bare = pattern.EndsWith(".service", StringComparison.OrdinalIgnoreCase)
                        ? pattern : pattern + ".service";
                    if (svc.Name.Equals(pattern, StringComparison.OrdinalIgnoreCase)
                        || svc.Name.Equals(bare,    StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        internal static void StartUnit(string unitName)
            => UnitActionAsync("StartUnit", unitName).GetAwaiter().GetResult();

        internal static void StopUnit(string unitName)
            => UnitActionAsync("StopUnit", unitName).GetAwaiter().GetResult();

        internal static void RestartUnit(string unitName)
            => UnitActionAsync("RestartUnit", unitName).GetAwaiter().GetResult();

        private static async Task UnitActionAsync(string method, string unitName)
        {
            using var conn = OpenSystem();
            var msg = BuildCallSS(conn, method, unitName, "replace");
            await conn.CallMethodAsync(msg, static (Message m, object? _) =>
                m.GetBodyReader().ReadObjectPath());
        }

        internal static void EnableUnits(string[] unitNames)
            => EnableUnitsAsync(unitNames).GetAwaiter().GetResult();

        private static async Task EnableUnitsAsync(string[] unitNames)
        {
            using var conn = OpenSystem();
            var msg = BuildEnableMessage(conn, unitNames, runtime: false, force: false);
            await conn.CallMethodAsync(msg, static (Message m, object? _) => 0);
        }

        internal static void DisableUnits(string[] unitNames)
            => DisableUnitsAsync(unitNames).GetAwaiter().GetResult();

        private static async Task DisableUnitsAsync(string[] unitNames)
        {
            using var conn = OpenSystem();
            var msg = BuildDisableMessage(conn, unitNames, runtime: false);
            await conn.CallMethodAsync(msg, static (Message m, object? _) => 0);
        }

        internal static void DaemonReload()
        {
            using var conn = OpenSystem();
            var msg = BuildCall(conn, "Reload");
            conn.CallMethodAsync(msg, static (Message m, object? _) => 0).GetAwaiter().GetResult();
        }

        internal static void WriteUnitFile(string unitName, string description, string execStart)
        {
            string unitPath = $"/etc/systemd/system/{unitName}";
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
            string unitPath = $"/etc/systemd/system/{unitName}";
            if (System.IO.File.Exists(unitPath))
                System.IO.File.Delete(unitPath);
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
