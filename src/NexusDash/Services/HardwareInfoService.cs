using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NexusDash.Services
{
    public sealed record HardwareInfoSnapshot(IReadOnlyList<HardwareInfoSectionSnapshot> Sections);

    public sealed record HardwareInfoSectionSnapshot(
        string TitleKey,
        IReadOnlyList<HardwareInfoItemSnapshot> Items);

    public sealed record HardwareInfoItemSnapshot(
        string NameKey,
        string Value,
        string? DisplayName = null);

    public sealed class HardwareInfoService(IProcessCommandRunner processCommandRunner)
    {
        public Task<HardwareInfoSnapshot> CaptureAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => Capture(cancellationToken), cancellationToken);
        }

        private HardwareInfoSnapshot Capture(CancellationToken cancellationToken)
        {
            var sections = new List<HardwareInfoSectionSnapshot>
            {
                CreateOverviewSection()
            };

#if WINDOWS
            if (OperatingSystem.IsWindows())
            {
                AddWindowsHardwareSections(sections, cancellationToken);
            }
#endif

            AddRuntimeFallbackSections(sections);
            AddCommandLineSections(sections);

            return new HardwareInfoSnapshot(sections
                .Where(static section => section.Items.Count > 0)
                .ToArray());
        }

        private static HardwareInfoSectionSnapshot CreateOverviewSection()
        {
            var items = new List<HardwareInfoItemSnapshot>
            {
                Item(NexusDashL.HardwareOperatingSystem, RuntimeInformation.OSDescription),
                Item(NexusDashL.HardwareArchitecture, RuntimeInformation.OSArchitecture.ToString()),
                Item(NexusDashL.HardwareProcessArchitecture, RuntimeInformation.ProcessArchitecture.ToString()),
                Item(NexusDashL.HardwareMachineName, Environment.MachineName),
                Item(NexusDashL.HardwareCurrentUser, Environment.UserName),
                Item(NexusDashL.HardwareRuntime, RuntimeInformation.FrameworkDescription)
            };

            return Section(NexusDashL.HardwareOverview, items);
        }

#if WINDOWS
        private void AddWindowsHardwareSections(
            ICollection<HardwareInfoSectionSnapshot> sections,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 单次限时 CIM 查询避开 System.Management 在 NativeAOT 中失效的 COM 路径。
            const string script = """
                $ErrorActionPreference='SilentlyContinue'; $ProgressPreference='SilentlyContinue'
                [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
                [ordered]@{
                    Win32_Processor=@(Get-CimInstance Win32_Processor -OperationTimeoutSec 5 | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors,ProcessorId)
                    Win32_OperatingSystem=@(Get-CimInstance Win32_OperatingSystem -OperationTimeoutSec 5 | Select-Object TotalVisibleMemorySize,FreePhysicalMemory)
                    Win32_PhysicalMemory=@(Get-CimInstance Win32_PhysicalMemory -OperationTimeoutSec 2 | Select-Object Capacity,Speed,Manufacturer,PartNumber)
                    Win32_VideoController=@(Get-CimInstance Win32_VideoController -OperationTimeoutSec 2 | Select-Object Name,DriverVersion,AdapterRAM)
                    Win32_DiskDrive=@(Get-CimInstance Win32_DiskDrive -OperationTimeoutSec 2 | Select-Object Model,Size,InterfaceType,MediaType,SerialNumber)
                    'Win32_NetworkAdapterConfiguration WHERE IPEnabled = True'=@(Get-CimInstance Win32_NetworkAdapterConfiguration -Filter 'IPEnabled=True' -OperationTimeoutSec 2 | Select-Object Description,MACAddress,IPAddress,DefaultIPGateway,DHCPEnabled)
                    Win32_ComputerSystem=@(Get-CimInstance Win32_ComputerSystem -OperationTimeoutSec 2 | Select-Object Manufacturer,Model)
                    Win32_BIOS=@(Get-CimInstance Win32_BIOS -OperationTimeoutSec 2 | Select-Object Manufacturer,SMBIOSBIOSVersion,@{Name='ReleaseDate';Expression={if($_.ReleaseDate){$_.ReleaseDate.ToString('yyyyMMddHHmmss',[Globalization.CultureInfo]::InvariantCulture)}}},SerialNumber)
                    Win32_BaseBoard=@(Get-CimInstance Win32_BaseBoard -OperationTimeoutSec 2 | Select-Object Manufacturer,Product,SerialNumber)
                    Win32_Battery=@(Get-CimInstance Win32_Battery -OperationTimeoutSec 2 | Select-Object Name,BatteryStatus,EstimatedChargeRemaining)
                } | ConvertTo-Json -Depth 5 -Compress
                """;
            try
            {
                var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                var output = processCommandRunner.ReadOutput("powershell.exe",
                    "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + encoded,
                    timeoutMilliseconds: 15000, cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(output)) return;
                using var document = JsonDocument.Parse(output);
                var hardware = document.RootElement;
                AddProcessorSection(sections, hardware);
                AddMemorySection(sections, hardware);
                AddGraphicsSection(sections, hardware);
                AddStorageSection(sections, hardware);
                AddNetworkSection(sections, hardware);
                AddFirmwareSection(sections, hardware);
                AddBatterySection(sections, hardware);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // CIM 不可用或受限时保留运行时概览。
            }
        }

        private static void AddProcessorSection(ICollection<HardwareInfoSectionSnapshot> sections, JsonElement hardware)
        {
            var processor = QueryWmi(hardware, "Win32_Processor", "Name", "NumberOfCores", "NumberOfLogicalProcessors", "ProcessorId").FirstOrDefault();
            if (processor is null)
            {
                return;
            }

            sections.Add(Section(NexusDashL.HardwareProcessor, new[]
            {
                Item(NexusDashL.HardwareCpuName, GetValue(processor, "Name")),
                Item(NexusDashL.HardwarePhysicalCores, GetValue(processor, "NumberOfCores")),
                Item(NexusDashL.HardwareLogicalProcessors, GetValue(processor, "NumberOfLogicalProcessors")),
                Item(NexusDashL.HardwareProcessorId, GetValue(processor, "ProcessorId"))
            }));
        }

        private static void AddMemorySection(ICollection<HardwareInfoSectionSnapshot> sections, JsonElement hardware)
        {
            var operatingSystem = QueryWmi(hardware, "Win32_OperatingSystem", "TotalVisibleMemorySize", "FreePhysicalMemory").FirstOrDefault();
            var modules = QueryWmi(hardware, "Win32_PhysicalMemory", "Capacity", "Speed", "Manufacturer", "PartNumber");
            var items = new List<HardwareInfoItemSnapshot>();

            if (operatingSystem is not null)
            {
                if (TryParseUInt64(GetValue(operatingSystem, "TotalVisibleMemorySize"), out var totalKb))
                {
                    items.Add(Item(NexusDashL.HardwareTotalMemory, FormatBytes(totalKb * 1024)));
                }

                if (TryParseUInt64(GetValue(operatingSystem, "FreePhysicalMemory"), out var freeKb))
                {
                    items.Add(Item(NexusDashL.HardwareAvailableMemory, FormatBytes(freeKb * 1024)));
                }
            }

            if (modules.Count > 0)
            {
                items.Add(Item(NexusDashL.HardwareMemorySlots, modules.Count.ToString(CultureInfo.CurrentCulture)));
                var index = 1;
                foreach (var module in modules.Take(8))
                {
                    var capacity = TryParseUInt64(GetValue(module, "Capacity"), out var capacityBytes)
                        ? FormatBytes(capacityBytes)
                        : GetValue(module, "Capacity");
                    var speed = GetValue(module, "Speed");
                    var manufacturer = GetValue(module, "Manufacturer");
                    var partNumber = GetValue(module, "PartNumber");
                    items.Add(Item(
                        "",
                        JoinParts(capacity, speed.Length > 0 ? $"{speed} MT/s" : "", manufacturer, partNumber),
                        string.Format(CultureInfo.CurrentCulture, "DIMM {0}", index++)));
                }
            }

            sections.Add(Section(NexusDashL.HardwareMemory, items));
        }

        private static void AddGraphicsSection(ICollection<HardwareInfoSectionSnapshot> sections, JsonElement hardware)
        {
            var adapters = QueryWmi(hardware, "Win32_VideoController", "Name", "DriverVersion", "AdapterRAM");
            if (adapters.Count == 0)
            {
                return;
            }

            var items = new List<HardwareInfoItemSnapshot>();
            var index = 1;
            foreach (var adapter in adapters.Take(8))
            {
                var name = GetValue(adapter, "Name");
                var driverVersion = GetValue(adapter, "DriverVersion");
                var memory = TryParseUInt64(GetValue(adapter, "AdapterRAM"), out var adapterRam)
                    ? FormatBytes(adapterRam)
                    : "";
                items.Add(Item(
                    "",
                    JoinParts(name, memory, driverVersion),
                    string.Format(CultureInfo.CurrentCulture, "GPU {0}", index++)));
            }

            sections.Add(Section(NexusDashL.HardwareGraphics, items));
        }

        private static void AddStorageSection(ICollection<HardwareInfoSectionSnapshot> sections, JsonElement hardware)
        {
            var disks = QueryWmi(hardware, "Win32_DiskDrive", "Model", "Size", "InterfaceType", "MediaType", "SerialNumber");
            if (disks.Count == 0)
            {
                return;
            }

            var items = new List<HardwareInfoItemSnapshot>();
            var index = 1;
            foreach (var disk in disks.Take(16))
            {
                var size = TryParseUInt64(GetValue(disk, "Size"), out var sizeBytes)
                    ? FormatBytes(sizeBytes)
                    : GetValue(disk, "Size");
                items.Add(Item(
                    "",
                    JoinParts(
                        GetValue(disk, "Model"),
                        size,
                        GetValue(disk, "InterfaceType"),
                        GetValue(disk, "MediaType"),
                        GetValue(disk, "SerialNumber")),
                    string.Format(CultureInfo.CurrentCulture, "Disk {0}", index++)));
            }

            sections.Add(Section(NexusDashL.HardwareStorage, items));
        }

        private static void AddNetworkSection(ICollection<HardwareInfoSectionSnapshot> sections, JsonElement hardware)
        {
            var adapters = QueryWmi(
                hardware, "Win32_NetworkAdapterConfiguration WHERE IPEnabled = True",
                "Description",
                "MACAddress",
                "IPAddress",
                "DefaultIPGateway",
                "DHCPEnabled");
            if (adapters.Count == 0)
            {
                return;
            }

            var items = new List<HardwareInfoItemSnapshot>();
            foreach (var adapter in adapters.Take(12))
            {
                var name = GetValue(adapter, "Description");
                items.Add(Item(
                    "",
                    JoinParts(
                        GetValue(adapter, "IPAddress"),
                        GetValue(adapter, "MACAddress"),
                        GetValue(adapter, "DefaultIPGateway"),
                        GetValue(adapter, "DHCPEnabled")),
                    string.IsNullOrWhiteSpace(name) ? "Adapter" : name));
            }

            sections.Add(Section(NexusDashL.HardwareNetwork, items));
        }

        private static void AddFirmwareSection(ICollection<HardwareInfoSectionSnapshot> sections, JsonElement hardware)
        {
            var computerSystem = QueryWmi(hardware, "Win32_ComputerSystem", "Manufacturer", "Model").FirstOrDefault();
            var bios = QueryWmi(hardware, "Win32_BIOS", "Manufacturer", "SMBIOSBIOSVersion", "ReleaseDate", "SerialNumber").FirstOrDefault();
            var baseBoard = QueryWmi(hardware, "Win32_BaseBoard", "Manufacturer", "Product", "SerialNumber").FirstOrDefault();
            var items = new List<HardwareInfoItemSnapshot>();

            if (computerSystem is not null)
            {
                items.Add(Item(
                    NexusDashL.HardwareManufacturer,
                    JoinParts(GetValue(computerSystem, "Manufacturer"), GetValue(computerSystem, "Model"))));
            }

            if (bios is not null)
            {
                items.Add(Item(
                    NexusDashL.HardwareBiosVersion,
                    JoinParts(
                        GetValue(bios, "Manufacturer"),
                        GetValue(bios, "SMBIOSBIOSVersion"),
                        FormatWmiDate(GetValue(bios, "ReleaseDate")),
                        GetValue(bios, "SerialNumber"))));
            }

            if (baseBoard is not null)
            {
                items.Add(Item(
                    NexusDashL.HardwareBaseBoard,
                    JoinParts(GetValue(baseBoard, "Manufacturer"), GetValue(baseBoard, "Product"), GetValue(baseBoard, "SerialNumber"))));
            }

            if (items.Count > 0)
            {
                sections.Add(Section(NexusDashL.HardwareFirmware, items));
            }
        }

        private static void AddBatterySection(ICollection<HardwareInfoSectionSnapshot> sections, JsonElement hardware)
        {
            var batteries = QueryWmi(hardware, "Win32_Battery", "Name", "BatteryStatus", "EstimatedChargeRemaining");
            if (batteries.Count == 0)
            {
                return;
            }

            var items = new List<HardwareInfoItemSnapshot>();
            var index = 1;
            foreach (var battery in batteries.Take(4))
            {
                items.Add(Item(
                    "",
                    JoinParts(
                        GetValue(battery, "Name"),
                        GetValue(battery, "BatteryStatus"),
                        AddPercentSuffix(GetValue(battery, "EstimatedChargeRemaining"))),
                    string.Format(CultureInfo.CurrentCulture, "Battery {0}", index++)));
            }

            sections.Add(Section(NexusDashL.HardwareBattery, items));
        }

        private static IReadOnlyList<IReadOnlyDictionary<string, string>> QueryWmi(
            JsonElement hardware, string wmiClassOrQuery, params string[] properties)
        {
            if (hardware.ValueKind != JsonValueKind.Object ||
                !hardware.TryGetProperty(wmiClassOrQuery, out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return [];
            }
            var rows = new List<IReadOnlyDictionary<string, string>>();
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in properties)
                {
                    row[property] = item.TryGetProperty(property, out var value) ? FormatWmiValue(value) : "";
                }
                rows.Add(row);
            }
            return rows;
        }

        private static string FormatWmiValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => "",
                JsonValueKind.Array => JoinParts(value.EnumerateArray().Select(FormatWmiValue)),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                JsonValueKind.String => value.GetString() ?? "",
                _ => value.GetRawText()
            };
        }
#endif

        private static void AddRuntimeFallbackSections(ICollection<HardwareInfoSectionSnapshot> sections)
        {
            if (sections.Any(static section => section.TitleKey == NexusDashL.HardwareProcessor))
            {
                return;
            }

            sections.Add(Section(NexusDashL.HardwareProcessor, new[]
            {
                Item(NexusDashL.HardwareLogicalProcessors, Environment.ProcessorCount.ToString(CultureInfo.CurrentCulture))
            }));
        }

        private void AddCommandLineSections(ICollection<HardwareInfoSectionSnapshot> sections)
        {
            var items = new List<HardwareInfoItemSnapshot>();
            if (OperatingSystem.IsLinux())
            {
                AddCommandOutput(items, "uname", "uname", "-a");
                AddCommandOutput(items, "lscpu", "lscpu", "");
                AddCommandOutput(items, "lsblk", "lsblk", "-d -o NAME,MODEL,SIZE,TYPE");
            }
            else if (OperatingSystem.IsMacOS())
            {
                AddCommandOutput(items, "sysctl", "sysctl", "-n machdep.cpu.brand_string hw.memsize");
                AddCommandOutput(items, "diskutil", "diskutil", "list");
            }
            else if (OperatingSystem.IsWindows())
            {
                return;
            }

            if (items.Count > 0)
            {
                sections.Add(Section(NexusDashL.HardwareCommandLine, items));
            }
        }

        private void AddCommandOutput(
            ICollection<HardwareInfoItemSnapshot> items,
            string displayName,
            string fileName,
            string arguments)
        {
            var output = ReadCommandOutput(fileName, arguments);
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            items.Add(Item("", SummarizeCommandOutput(output), displayName));
        }

        private string ReadCommandOutput(string fileName, string arguments)
        {
            try
            {
                return processCommandRunner.ReadOutput(fileName, arguments, timeoutMilliseconds: 1800);
            }
            catch
            {
                return "";
            }
        }

        private static HardwareInfoSectionSnapshot Section(
            string titleKey,
            IReadOnlyList<HardwareInfoItemSnapshot> items)
        {
            return new HardwareInfoSectionSnapshot(
                titleKey,
                items.Where(static item => !string.IsNullOrWhiteSpace(item.Value)).ToArray());
        }

        private static HardwareInfoItemSnapshot Item(string nameKey, string value, string? displayName = null)
        {
            return new HardwareInfoItemSnapshot(nameKey, value.Trim(), displayName);
        }

        private static string GetValue(IReadOnlyDictionary<string, string> values, string key)
        {
            return values.TryGetValue(key, out var value) ? value.Trim() : "";
        }

        private static string JoinParts(params string?[] values)
        {
            return JoinParts(values.AsEnumerable());
        }

        private static string JoinParts(IEnumerable<string?> values)
        {
            return string.Join(" | ", values
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string SummarizeCommandOutput(string output)
        {
            return JoinParts(output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(static line => line.Trim())
                .Where(static line => line.Length > 0)
                .Take(6));
        }

        private static string AddPercentSuffix(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : $"{value}%";
        }

        private static bool TryParseUInt64(string text, out ulong value)
        {
            return ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static string FormatBytes(ulong bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
            var value = (double)bytes;
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return $"{value:F1} {units[unit]}";
        }

        private static string FormatWmiDate(string value)
        {
            if (value.Length < 8)
            {
                return value;
            }

            return DateTime.TryParseExact(
                value[..8],
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date)
                ? date.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture)
                : value;
        }
    }
}
