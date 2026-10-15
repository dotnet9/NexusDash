using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        var root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        var evidence = new VerificationEvidence(root);
        try
        {
            evidence.Phase("native-aot");
            Require(!RuntimeFeature.IsDynamicCodeSupported, "native-aot-required");
            evidence.Phase("languages");
            var resources = Path.Combine(AppContext.BaseDirectory, "I18n");
            if (OperatingSystem.IsMacOS() && !Directory.Exists(resources))
                resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "I18n"));
            var manager = I18nManager.Instance;
            manager.Register(new JsonLangPlugin { ResourceFolder = resources }, CultureInfo.GetCultureInfo("en-US"));
            var labels = new HashSet<string>();
            foreach (var culture in new[] { "en-US", "zh-CN", "zh-Hant", "ja-JP" })
            {
                Require(File.Exists(Path.Combine(resources, "NexusDash", culture + ".json")), "language-file-missing");
                manager.Culture = CultureInfo.GetCultureInfo(culture);
                var text = manager.GetResource(NexusDashL.LanguageMenu);
                Require(!string.IsNullOrWhiteSpace(text) && text != NexusDashL.LanguageMenu, "language-resource-missing");
                labels.Add(text);
            }
            evidence.Write("distinctLanguages=" + labels.Count);
            Require(labels.Count == 4, "languages-not-distinct");
            evidence.Phase("preferences");
            var preferencesPath = Path.Combine(root, "runtime-" + Guid.NewGuid().ToString("N") + ".json");
            new UserPreferencesService(preferencesPath).Update(preferences =>
            {
                preferences.ThemeKey = "system";
                preferences.PaletteKey = "aurora";
                preferences.CultureName = "ja-JP";
            });
            var saved = new UserPreferencesService(preferencesPath).Load();
            Require(saved.ThemeKey == "system" && saved.PaletteKey == "aurora" && saved.CultureName == "ja-JP", "preferences-roundtrip-failed");
            var telemetry = OperatingSystem.IsWindows() ? VerifyWindowsTelemetry(evidence) : "";
            var result = $"NativeAOT runtime verification passed; version={typeof(Program).Assembly.GetName().Version}; languages=4; preferences=isolated{telemetry}";
            File.WriteAllText(Path.Combine(root, "runtime-verification.txt"), result);
            evidence.Write("status=passed");
            Console.WriteLine(result);
        }
        catch (Exception exception)
        {
            // 只保存固定代码和异常类型，不写入未知消息、路径或采样对象。
            var code = exception is VerificationFailure failure ? failure.Code
                : exception is OperationCanceledException ? "cancelled" : "unexpected";
            evidence.Write("status=failed;code=" + code + ";exceptionType=" + exception.GetType().FullName + ";hresult=" + exception.HResult.ToString(CultureInfo.InvariantCulture));
            throw;
        }
    }

    private static string VerifyWindowsTelemetry(VerificationEvidence evidence)
    {
        var runner = new ProcessCommandRunner();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        evidence.Phase("hardware");
        var hardware = new HardwareInfoService(runner).CaptureAsync(cancellation.Token).GetAwaiter().GetResult();
        var items = hardware.Sections.SelectMany(section => section.Items).ToArray();
        var cpuName = items.Any(item => item.NameKey == NexusDashL.HardwareCpuName && !string.IsNullOrWhiteSpace(item.Value));
        var cores = items.Where(item => item.NameKey == NexusDashL.HardwarePhysicalCores)
            .Select(item => int.TryParse(item.Value, out var value) ? value : 0).FirstOrDefault();
        var memory = items.Any(item => item.NameKey == NexusDashL.HardwareTotalMemory && !string.IsNullOrWhiteSpace(item.Value));
        var firmware = hardware.Sections.Any(section => section.TitleKey == NexusDashL.HardwareFirmware && section.Items.Count > 0);
        evidence.Write(FormattableString.Invariant($"sections={hardware.Sections.Count};items={items.Length};cpuNamePresent={cpuName};physicalCores={cores};memoryPresent={memory};firmwarePresent={firmware}"));
        Require(cpuName && cores > 0 && memory && firmware, "hardware-incomplete");
        evidence.Phase("processes");
        var telemetry = new ProcessTelemetryService(runner);
        var processes = telemetry.GetProcessesAsync(cancellation.Token).GetAwaiter().GetResult();
        var commandLines = processes.Count(process => !string.IsNullOrWhiteSpace(process.CommandLine));
        var services = telemetry.WindowsServiceMetadataCount;
        var windowsProcesses = processes.Count(process => process.Category == ProcessCategory.WindowsProcess);
        var validSample = processes.Any(process => process.WorkingSetBytes > 0 && process.ParentPid.HasValue && double.IsFinite(process.CpuPercent) && process.CpuPercent >= 0);
        evidence.Write(FormattableString.Invariant($"processCount={processes.Count};commandLines={commandLines};services={services};windowsProcesses={windowsProcesses};validSample={validSample}"));
        Require(processes.Count > 0 && commandLines > 0 && services > 0 && windowsProcesses > 0 && validSample, "process-telemetry-incomplete");
        evidence.Phase("system");
        using var monitor = new SystemMonitorService(runner);
        var metrics = monitor.GetMetricsAsync().GetAwaiter().GetResult();
        var memoryValid = metrics.Memory.TotalBytes > 0 && metrics.Memory.AvailableBytes <= metrics.Memory.TotalBytes;
        var cpuValid = double.IsFinite(metrics.Cpu.TotalUsage) && metrics.Cpu.TotalUsage >= 0 && metrics.Cpu.TotalUsage <= 100;
        evidence.Write(FormattableString.Invariant($"memoryValid={memoryValid};cpuValid={cpuValid}"));
        Require(memoryValid && cpuValid, "system-telemetry-invalid");
        // 仅输出计数，避免暴露命令行、硬件标识或进程名称。
        return string.Create(CultureInfo.InvariantCulture,
            $"; windowsHardware={hardware.Sections.Count}/{items.Length}; physicalCores={cores}; firmware=true; processCount={processes.Count}; processCommandLines={commandLines}; processServices={services}; windowsProcesses={windowsProcesses}; memoryBytes={metrics.Memory.TotalBytes}");
    }

    private static void Require(bool passed, string code)
    {
        if (!passed) throw new VerificationFailure(code);
    }

    private sealed class VerificationFailure(string code) : InvalidOperationException(code)
    {
        public string Code { get; } = code;
    }

    private sealed class VerificationEvidence
    {
        private readonly string _path;
        private readonly Stopwatch _elapsed = Stopwatch.StartNew();
        private string _phase = "initialization";
        public VerificationEvidence(string root)
        {
            _path = Path.Combine(root, "runtime-diagnostics.txt");
            File.WriteAllText(_path, "");
        }
        public void Phase(string phase) { _phase = phase; Write("status=started"); }
        public void Write(string counts) => File.AppendAllText(_path,
            "phase=" + _phase + ";elapsedMilliseconds=" + _elapsed.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + ";" + counts + Environment.NewLine);
    }
}
