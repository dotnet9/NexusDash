using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace NexusDash.Services
{
    public sealed class ProcessCommandRunner : IProcessCommandRunner
    {
        public string ReadOutput(string fileName, string arguments, int timeoutMilliseconds = 1500,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

            process.Start();
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
                TryKill(process);
                cancellationToken.ThrowIfCancellationRequested();
                return "";
            }

            _ = errorTask.GetAwaiter().GetResult();
            var output = outputTask.GetAwaiter().GetResult();
            return process.ExitCode == 0 ? output : "";
        }

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // 外部命令仅用于采样，结束失败不应影响主流程刷新。
            }
        }
    }
}
