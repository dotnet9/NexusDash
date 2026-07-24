using Avalonia;
using Avalonia.Media;
using System;
using System.IO;
using CodeWF.Log.Core;
using Microsoft.Extensions.Logging;
using ReactiveUI.Avalonia;

namespace NexusDash;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var loggerInitialized = false;
        try
        {
            ConfigureLogger();
            loggerInitialized = true;
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
            if (loggerInitialized)
            {
                Logger.FatalToFile("NexusDash application terminated unexpectedly.", ex);
            }
            else
            {
                LogException(ex);
            }

            throw;
        }
        finally
        {
            if (loggerInitialized)
            {
                Logger.ShutdownAsync().GetAwaiter().GetResult();
            }
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseReactiveUI(builder => builder.WithAvalonia())
            .UsePlatformDetect()
            .With(new Win32PlatformOptions())
            .LogToTrace();

    private static void ConfigureLogger()
    {
        Logger.Initialize(new LoggerOptions
        {
            MinimumLevel = LogLevel.Debug,
            EnableConsole = false,
            RecentEventCapacity = 200,
            File = new FileLogOptions
            {
                DirectoryPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NexusDash"),
                BatchSize = 40,
                MaxFileSizeBytes = 20L * 1024 * 1024,
                TimestampFormat = "HH:mm:ss"
            }
        });
    }

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
