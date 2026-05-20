using Avalonia;
using Avalonia.Media;
using System;
using System.IO;
using ReactiveUI.Avalonia;

namespace NexusDash;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp()
                .With(new FontManagerOptions
                {
                    FontFallbacks = [new FontFallback
                    {
                        FontFamily = new FontFamily("Microsoft YaHei")
                    }]
                })
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogException(ex);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseReactiveUI(builder => builder.WithAvalonia())
            .UsePlatformDetect()
            .With(new Win32PlatformOptions())
            .LogToTrace();

    private static void LogException(Exception ex)
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var logDirectory = Path.Combine(homeDirectory, Path.Combine("NexusDash", "AppCrashLogs"));
        Directory.CreateDirectory(logDirectory);

        var logFileName = $"CrashLog_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        var logFilePath = Path.Combine(logDirectory, logFileName);

        File.WriteAllText(logFilePath,
            $"CrashTime: {DateTime.Now}\r\n" +
            $"Exception Type: {ex.GetType().Name}\r\n" +
            $"Exception Message: {ex.Message}\r\n" +
            $"Stack Info: \r\n{ex.StackTrace}");
    }
}
