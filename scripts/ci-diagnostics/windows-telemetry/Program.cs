using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using NexusDash;
using NexusDash.Models;
using NexusDash.Services;

// 仅用于云端诊断；禁止序列化硬件值、进程对象、命令参数或原始错误。
if (args is not [var mode, var output] || mode is not ("exact" or "instrumented"))
    return 2;
Directory.CreateDirectory(output);
var evidence = new Evidence(Path.Combine(output, "telemetry.jsonl"));
try
{
    evidence.Write("start", new { mode, framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription });
    // 与正式自检进入遥测阶段前的语言初始化顺序一致。
    var manager = I18nManager.Instance;
    manager.Register(new JsonLangPlugin { ResourceFolder = Path.Combine(AppContext.BaseDirectory, "I18n") }, CultureInfo.GetCultureInfo("en-US"));
    foreach (var culture in new[] { "en-US", "zh-CN", "zh-Hant", "ja-JP" })
    {
        manager.Culture = CultureInfo.GetCultureInfo(culture);
        _ = manager.GetResource(NexusDashL.LanguageMenu);
    }
    evidence.Write("languages-ready", new { count = 4 });

    if (mode == "exact")
    {
        evidence.Stage = "exact-telemetry";
        var verifier = typeof(HardwareInfoService).Assembly.GetType("NexusDash.RuntimeVerification", throwOnError: true)!;
        var method = verifier.GetMethod("VerifyWindowsTelemetry", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new DiagnosticFailure("exact-method-missing");
        _ = method.Invoke(null, null);
    }
    else
    {
        VerifyInstrumented(evidence);
    }
    evidence.Write("passed", new { passed = true });
    return 0;
}
catch (Exception exception)
{
    if (exception is TargetInvocationException { InnerException: { } inner }) exception = inner;
    // 未知异常消息可能包含隐私值，仅记录类型、HResult 和固定错误代码。
    evidence.Write("failed", new
    {
        passed = false,
        exceptionType = exception.GetType().FullName,
        exception.HResult,
        failureCode = exception is DiagnosticFailure failure ? failure.Code : exception.Message switch
        {
            "Windows CIM hardware data is missing CPU, physical cores, memory or firmware." => "hardware-incomplete",
            "Windows process telemetry is missing valid samples, command lines or service metadata." => "process-telemetry-incomplete",
            "Windows system CPU or memory telemetry is invalid." => "system-telemetry-invalid",
            _ => "product-exception"
        },
        stackMethods = new StackTrace(exception, false).GetFrames().Select(frame => frame.GetMethod()?.DeclaringType?.FullName + "." + frame.GetMethod()?.Name).ToArray()
    });
    return 1;
}

static void VerifyInstrumented(Evidence evidence)
{
    var runner = new DiagnosticCommandRunner(evidence);
    // 保留产品的共享时间预算和全部断言，包括必须由 CIM 提供的数据。
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(25));
    evidence.Stage = "hardware";
    evidence.Write("phase-start", new { budgetMilliseconds = 25000 });
    var hardware = new HardwareInfoService(runner).CaptureAsync(cancellation.Token).GetAwaiter().GetResult();
    var items = hardware.Sections.SelectMany(section => section.Items).ToArray();
    var cpuName = items.Any(item => item.NameKey == NexusDashL.HardwareCpuName && !string.IsNullOrWhiteSpace(item.Value));
    var cores = items.Where(item => item.NameKey == NexusDashL.HardwarePhysicalCores)
        .Select(item => int.TryParse(item.Value, out var value) ? value : 0).FirstOrDefault();
    var memory = items.Any(item => item.NameKey == NexusDashL.HardwareTotalMemory && !string.IsNullOrWhiteSpace(item.Value));
    var firmware = hardware.Sections.Any(section => section.TitleKey == NexusDashL.HardwareFirmware && section.Items.Count > 0);
    evidence.Write("hardware-counts", new { sections = hardware.Sections.Count, items = items.Length, cpuNamePresent = cpuName, physicalCores = cores, memoryPresent = memory, firmwarePresent = firmware });
    if (!cpuName || cores <= 0 || !memory || !firmware)
        throw new DiagnosticFailure("hardware-incomplete");

    evidence.Stage = "processes";
    evidence.Write("phase-start", new { });
    var telemetry = new ProcessTelemetryService(runner);
    var processes = telemetry.GetProcessesAsync(cancellation.Token).GetAwaiter().GetResult();
    var commandLines = processes.Count(process => !string.IsNullOrWhiteSpace(process.CommandLine));
    var services = (int)(typeof(ProcessTelemetryService).GetProperty("WindowsServiceMetadataCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(telemetry)!);
    var windowsProcesses = processes.Count(process => process.Category == ProcessCategory.WindowsProcess);
    var validSample = processes.Any(process => process.WorkingSetBytes > 0 && process.ParentPid.HasValue && double.IsFinite(process.CpuPercent) && process.CpuPercent >= 0);
    evidence.Write("process-counts", new { processCount = processes.Count, commandLines, services, windowsProcesses, validSample });
    if (processes.Count == 0 || commandLines == 0 || services == 0 || windowsProcesses == 0 || !validSample)
        throw new DiagnosticFailure("process-telemetry-incomplete");

    evidence.Stage = "system";
    evidence.Write("phase-start", new { });
    using var monitor = new SystemMonitorService(runner);
    var metrics = monitor.GetMetricsAsync().GetAwaiter().GetResult();
    var memoryValid = metrics.Memory.TotalBytes > 0 && metrics.Memory.AvailableBytes <= metrics.Memory.TotalBytes;
    var cpuValid = double.IsFinite(metrics.Cpu.TotalUsage) && metrics.Cpu.TotalUsage >= 0 && metrics.Cpu.TotalUsage <= 100;
    evidence.Write("system-counts", new { memoryValid, cpuValid });
    if (!memoryValid || !cpuValid) throw new DiagnosticFailure("system-telemetry-invalid");
}

sealed class DiagnosticFailure(string code) : Exception
{
    public string Code { get; } = code;
}

sealed class Evidence(string path)
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly object _gate = new();
    public string Stage { get; set; } = "initialization";
    public void Write(string kind, object counts)
    {
        var json = JsonSerializer.Serialize(new { kind, stage = Stage, elapsedMilliseconds = _elapsed.ElapsedMilliseconds, counts });
        lock (_gate)
        {
            File.AppendAllText(path, json + Environment.NewLine);
            Console.WriteLine(json);
        }
    }
}

sealed class DiagnosticCommandRunner(Evidence evidence) : IProcessCommandRunner
{
    private int _commands;
    public string ReadOutput(string fileName, string arguments, int timeoutMilliseconds = 1500, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var command = Interlocked.Increment(ref _commands);
        var elapsed = Stopwatch.StartNew();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        evidence.Write("command-start", new { command, timeoutMilliseconds });
        process.Start();
        evidence.Write("command-started", new { command, startMilliseconds = elapsed.ElapsedMilliseconds });
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMilliseconds);
        try
        {
            process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            var killSucceeded = false;
            try { process.Kill(entireProcessTree: true); killSucceeded = true; } catch { }
            evidence.Write("command-timeout", new
            {
                command, elapsedMilliseconds = elapsed.ElapsedMilliseconds,
                callerCancelled = cancellationToken.IsCancellationRequested, killSucceeded,
                stdoutLength = outputTask.IsCompletedSuccessfully ? (int?)outputTask.Result.Length : null,
                stderrLength = errorTask.IsCompletedSuccessfully ? (int?)errorTask.Result.Length : null
            });
            cancellationToken.ThrowIfCancellationRequested();
            return "";
        }
        var error = errorTask.GetAwaiter().GetResult();
        var result = outputTask.GetAwaiter().GetResult();
        evidence.Write("command-exit", new { command, elapsedMilliseconds = elapsed.ElapsedMilliseconds, exitCode = process.ExitCode, stdoutLength = result.Length, stderrLength = error.Length });
        if (evidence.Stage == "processes" && process.ExitCode == 0)
        {
            // 只统计原始 JSON 的结构，区分 CIM 无结果与产品解析、合并过程丢失数据。
            try
            {
                using var document = JsonDocument.Parse(result);
                var root = document.RootElement;
                var rows = root.TryGetProperty("Services", out var services) && services.ValueKind == JsonValueKind.Array
                    ? services.EnumerateArray().ToArray() : [];
                var processes = root.TryGetProperty("Processes", out var processRows) && processRows.ValueKind == JsonValueKind.Array
                    ? processRows.EnumerateArray().ToArray() : [];
                var processPids = processes.Select(ReadPid).Where(pid => pid > 0).ToHashSet();
                evidence.Write("metadata-json-counts", new
                {
                    processRows = processes.Length,
                    serviceRows = rows.Length,
                    servicesWithPositivePid = rows.Count(row => ReadPid(row) > 0),
                    servicesMatchingProcessPid = rows.Count(row => processPids.Contains(ReadPid(row))),
                    servicesWithDisplayName = rows.Count(row => HasText(row, "DisplayName")),
                    servicesWithName = rows.Count(row => HasText(row, "Name"))
                });
            }
            catch (Exception exception)
            {
                evidence.Write("metadata-json-invalid", new { exceptionType = exception.GetType().FullName });
            }
        }
        return process.ExitCode == 0 ? result : "";
    }

    private static int ReadPid(JsonElement row)
    {
        if (!row.TryGetProperty("ProcessId", out var value)) return -1;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : -1;
    }

    private static bool HasText(JsonElement row, string property)
        => row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString());
}
