using System.Diagnostics;

namespace NexusDash.Services
{
    public sealed class ProcessCommandRunner : IProcessCommandRunner
    {
        public string ReadOutput(string fileName, string arguments, int timeoutMilliseconds = 1500)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMilliseconds))
            {
                TryKill(process);
                return "";
            }

            _ = errorTask.GetAwaiter().GetResult();
            return outputTask.GetAwaiter().GetResult();
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
