using NexusDash.Services;

internal static class TelemetryRegression
{
    public static void Run(Action<bool, string> check)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var cancellation = new CancellationTokenSource();
        var recovered = new ControlledRunner(cancellation.Token);
        var rows = new ProcessTelemetryService(recovered).GetProcessesAsync(cancellation.Token).GetAwaiter().GetResult();
        check(rows.Any(row => row.Pid == Environment.ProcessId && row.Name == "Controlled service"), "Empty initial service metadata recovers real supplied service rows");
        check(recovered.Calls == 2 && recovered.RetryBudget == 5000 && recovered.SameToken, "Service retry occurs once with its explicit budget and original token");

        var empty = new ControlledRunner(cancellation.Token) { AlwaysEmpty = true };
        rows = new ProcessTelemetryService(empty).GetProcessesAsync(cancellation.Token).GetAwaiter().GetResult();
        check(empty.Calls == 2 && rows.All(row => row.Name != "Controlled service"), "Persistently empty service metadata stays empty after one retry");

        var present = new ControlledRunner(cancellation.Token) { InitiallyPresent = true };
        rows = new ProcessTelemetryService(present).GetProcessesAsync(cancellation.Token).GetAwaiter().GetResult();
        check(present.Calls == 1 && rows.Any(row => row.Pid == Environment.ProcessId && row.Name == "Controlled service"), "Present initial services avoid a redundant retry");

        using var canceled = new CancellationTokenSource();
        var cancelRunner = new ControlledRunner(canceled.Token) { CancelAfterInitial = canceled };
        var canceledObserved = false;
        try { new ProcessTelemetryService(cancelRunner).GetProcessesAsync(canceled.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { canceledObserved = true; }
        check(canceledObserved && cancelRunner.Calls == 1 && cancelRunner.SameToken, "Cancellation after empty metadata prevents the service retry");
    }

    private sealed class ControlledRunner(CancellationToken expectedToken) : IProcessCommandRunner
    {
        public int Calls { get; private set; }
        public int RetryBudget { get; private set; }
        public bool SameToken { get; private set; } = true;
        public bool AlwaysEmpty { get; init; }
        public bool InitiallyPresent { get; init; }
        public CancellationTokenSource? CancelAfterInitial { get; init; }

        public string ReadOutput(string fileName, string arguments, int timeoutMilliseconds = 1500, CancellationToken cancellationToken = default)
        {
            // 完全受控的命令替身，不启动 PowerShell，也不输出实际进程数据。
            Calls++;
            SameToken &= cancellationToken == expectedToken;
            cancellationToken.ThrowIfCancellationRequested();
            var service = "{\"ProcessId\":" + Environment.ProcessId + ",\"Name\":\"ControlledService\",\"DisplayName\":\"Controlled service\"}";
            if (Calls == 1)
            {
                CancelAfterInitial?.Cancel();
                return "{\"Processes\":[],\"Services\":" + (InitiallyPresent ? "[" + service + "]" : "[]") + "}";
            }
            RetryBudget = timeoutMilliseconds;
            if (Calls > 2) throw new InvalidOperationException("Only one service retry is permitted.");
            return AlwaysEmpty ? "[]" : "[" + service + "]";
        }
    }
}
