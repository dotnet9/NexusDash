using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using NexusDash.Models;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using NexusDash.Services;

namespace NexusDash;

/// <summary>显式验证正式原生产物的语言、独立偏好和 Windows 实时遥测，不创建窗口。</summary>
internal static class RuntimeVerification
{
    public static void Run(string directory)
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
            throw new InvalidOperationException("Runtime verification requires a NativeAOT executable.");
        var root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        var resources = Path.Combine(AppContext.BaseDirectory, "I18n");
        if (OperatingSystem.IsMacOS() && !Directory.Exists(resources))
            resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "I18n"));
        var manager = I18nManager.Instance;
        manager.Register(new JsonLangPlugin { ResourceFolder = resources }, CultureInfo.GetCultureInfo("en-US"));
        var labels = new HashSet<string>();
        foreach (var culture in new[] { "en-US", "zh-CN", "zh-Hant", "ja-JP" })
        {
            if (!File.Exists(Path.Combine(resources, "NexusDash", culture + ".json")))
                throw new InvalidOperationException("Published language file is missing: " + culture);
            manager.Culture = CultureInfo.GetCultureInfo(culture);
            var text = manager.GetResource(NexusDashL.LanguageMenu);
            if (string.IsNullOrWhiteSpace(text) || text == NexusDashL.LanguageMenu)
                throw new InvalidOperationException("Published language resources are missing: " + culture);
            labels.Add(text);
        }
        if (labels.Count != 4) throw new InvalidOperationException("All four published languages must switch independently.");
        var preferencesPath = Path.Combine(root, "runtime-" + Guid.NewGuid().ToString("N") + ".json");
        new UserPreferencesService(preferencesPath).Update(preferences =>
        {
            preferences.ThemeKey = "system";
            preferences.PaletteKey = "aurora";
            preferences.CultureName = "ja-JP";
        });
        var saved = new UserPreferencesService(preferencesPath).Load();
        if (saved.ThemeKey != "system" || saved.PaletteKey != "aurora" || saved.CultureName != "ja-JP")
            throw new InvalidOperationException("Native appearance preferences did not round-trip.");
        var telemetry = OperatingSystem.IsWindows() ? VerifyWindowsTelemetry() : "";
        var result = $"NativeAOT runtime verification passed; version={typeof(Program).Assembly.GetName().Version}; languages=4; preferences=isolated{telemetry}";
        File.WriteAllText(Path.Combine(root, "runtime-verification.txt"), result);
        Console.WriteLine(result);
    }

    private static string VerifyWindowsTelemetry()
    {
        var runner = new ProcessCommandRunner();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var hardware = new HardwareInfoService(runner).CaptureAsync(cancellation.Token).GetAwaiter().GetResult();
        var items = hardware.Sections.SelectMany(section => section.Items).ToArray();
        var cpuName = items.Any(item => item.NameKey == NexusDashL.HardwareCpuName && !string.IsNullOrWhiteSpace(item.Value));
        var cores = items.Where(item => item.NameKey == NexusDashL.HardwarePhysicalCores)
            .Select(item => int.TryParse(item.Value, out var value) ? value : 0).FirstOrDefault();
        var memory = items.Any(item => item.NameKey == NexusDashL.HardwareTotalMemory && !string.IsNullOrWhiteSpace(item.Value));
        var firmware = hardware.Sections.Any(section => section.TitleKey == NexusDashL.HardwareFirmware && section.Items.Count > 0);
        if (!cpuName || cores <= 0 || !memory || !firmware)
            throw new InvalidOperationException("Windows CIM hardware data is missing CPU, physical cores, memory or firmware.");
        var telemetry = new ProcessTelemetryService(runner);
        var processes = telemetry.GetProcessesAsync(cancellation.Token).GetAwaiter().GetResult();
        var commandLines = processes.Count(process => !string.IsNullOrWhiteSpace(process.CommandLine));
        var services = telemetry.WindowsServiceMetadataCount;
        var windowsProcesses = processes.Count(process => process.Category == ProcessCategory.WindowsProcess);
        if (processes.Count == 0 || commandLines == 0 || services == 0 || windowsProcesses == 0 ||
            !processes.Any(process => process.WorkingSetBytes > 0 && process.ParentPid.HasValue &&
                double.IsFinite(process.CpuPercent) && process.CpuPercent >= 0))
            throw new InvalidOperationException("Windows process telemetry is missing valid samples, command lines or service metadata.");
        using var monitor = new SystemMonitorService(runner);
        var metrics = monitor.GetMetricsAsync().GetAwaiter().GetResult();
        if (metrics.Memory.TotalBytes == 0 || metrics.Memory.AvailableBytes > metrics.Memory.TotalBytes ||
            !double.IsFinite(metrics.Cpu.TotalUsage) || metrics.Cpu.TotalUsage < 0 || metrics.Cpu.TotalUsage > 100)
            throw new InvalidOperationException("Windows system CPU or memory telemetry is invalid.");
        // 仅输出计数，避免暴露命令行、硬件标识或进程名称。
        return string.Create(CultureInfo.InvariantCulture,
            $"; windowsHardware={hardware.Sections.Count}/{items.Length}; physicalCores={cores}; firmware=true; processCount={processes.Count}; processCommandLines={commandLines}; processServices={services}; windowsProcesses={windowsProcesses}; memoryBytes={metrics.Memory.TotalBytes}");
    }
}
