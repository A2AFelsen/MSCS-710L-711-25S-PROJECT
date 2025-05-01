using System;
using System.IO;
using System.Diagnostics;
using System.Configuration;
using System.Threading;
using OpenHardwareMonitor.Hardware;

namespace OpenHardwareMonitor
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    internal static class Logger
    {
        private static readonly string LogDirectory;
        private static readonly string LogFilePrefix = "OHM_";
        private static readonly string LogFileExtension = ".log";
        private static string FullLogPath;
        private static readonly LogLevel MinimumLogLevel;
        private static readonly bool LogToConsole;
        private static readonly bool LogToFile;
        private static readonly long MaxLogFileSize = 5 * 1024 * 1024; // 5MB
        private static readonly int MaxLogFilesToKeep = 10;
        private static readonly object fileLock = new object();

        static Logger()
        {
            // Initialize from configuration (App.config)
            LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

            // Parse configuration settings with defaults
            Enum.TryParse(ConfigurationManager.AppSettings["MinimumLogLevel"], out MinimumLogLevel);
            bool.TryParse(ConfigurationManager.AppSettings["LogToConsole"], out LogToConsole);
            bool.TryParse(ConfigurationManager.AppSettings["LogToFile"], out LogToFile);

            // Configure log file rotation
            ConfigureLogFileRotation();

            // Initial log entry
            Info("Logger initialized");
        }

        private static void ConfigureLogFileRotation()
        {

            try
            {
                // Ensure log directory exists
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                // Clean up old log files
                CleanUpOldLogFiles();

                // Create new log file with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                FullLogPath = Path.Combine(LogDirectory, $"{LogFilePrefix}{timestamp}{LogFileExtension}");

                if (!File.Exists(FullLogPath)){
                    File.Create(FullLogPath);
                }
               
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to configure log file rotation: {ex.Message}");
            }
        }

        private static void CleanUpOldLogFiles()
        {
            try
            {
                var logFiles = Directory.GetFiles(LogDirectory, $"{LogFilePrefix}*{LogFileExtension}");
                if (logFiles.Length > MaxLogFilesToKeep)
                {
                    Array.Sort(logFiles);
                    for (int i = 0; i < logFiles.Length - MaxLogFilesToKeep; i++)
                    {
                        try
                        {
                            File.Delete(logFiles[i]);
                        }
                        catch { /* Ignore deletion errors */ }
                    }
                }
            }
            catch { /* Ignore cleanup errors */ }
        }

        private static void RotateLogFileIfNeeded()
        {
            try
            {
                if (new FileInfo(FullLogPath).Length > MaxLogFileSize)
                {
                    lock (fileLock)
                    {
                        if (new FileInfo(FullLogPath).Length > MaxLogFileSize)
                        {
                            string newFilePath = Path.Combine(LogDirectory,
                                $"{LogFilePrefix}{DateTime.Now:yyyyMMdd_HHmmss}{LogFileExtension}");
                            File.Move(FullLogPath, newFilePath);
                            File.Create(FullLogPath).Close(); // Create new empty log file
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to rotate log file: {ex.Message}");
            }
        }

        public static void Log(LogLevel level, string message, Exception exception = null)
        {
            if (level < MinimumLogLevel)
                return;

            string logEntry = FormatLogEntry(level, message, exception);
            string detailedEntry = FormatDetailedLogEntry(level, message, exception);

            // Console logging (colored)
            if (LogToConsole)
            {
                ConsoleColor originalColor = Console.ForegroundColor;
                Console.ForegroundColor = GetConsoleColor(level);
                Console.WriteLine(logEntry);
                Console.ForegroundColor = originalColor;
            }
            try
            {
                RotateLogFileIfNeeded();

                lock (fileLock)
                {
                    // Use a StreamWriter with explicit flushing for more reliable logging
                    using (var writer = new StreamWriter(FullLogPath, true))
                    {
                        writer.WriteLine(detailedEntry);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        [Conditional("DEBUG")]
        public static void Debug(string message) => Log(LogLevel.Debug, message);

        public static void Info(string message) => Log(LogLevel.Info, message);
        public static void Warning(string message) => Log(LogLevel.Warning, message);
        public static void Error(string message, Exception ex = null) => Log(LogLevel.Error, message, ex);
        public static void Critical(string message, Exception ex = null) => Log(LogLevel.Critical, message, ex);

        private static string FormatLogEntry(LogLevel level, string message, Exception exception)
        {
            return $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}" +
                   (exception != null ? $" - {exception.Message}" : "");
        }

        private static string FormatDetailedLogEntry(LogLevel level, string message, Exception exception)
        {
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [Thread:{Thread.CurrentThread.ManagedThreadId}] {message}";

            if (exception != null)
            {
                entry += $"{Environment.NewLine}Exception: {exception.GetType().Name}: {exception.Message}{Environment.NewLine}" +
                         $"Stack Trace: {exception.StackTrace}";

                if (exception.InnerException != null)
                {
                    entry += $"{Environment.NewLine}Inner Exception: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}{Environment.NewLine}" +
                             $"Inner Stack Trace: {exception.InnerException.StackTrace}";
                }
            }

            return entry;
        }

        private static ConsoleColor GetConsoleColor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return ConsoleColor.Gray;
                case LogLevel.Info:
                    return ConsoleColor.White;
                case LogLevel.Warning:
                    return ConsoleColor.Yellow;
                case LogLevel.Error:
                    return ConsoleColor.Red;
                case LogLevel.Critical:
                    return ConsoleColor.DarkRed;
                default:
                    return ConsoleColor.White;
            }
        }

        public static void LogHardwareInfo(IHardware hardware)
        {
            if (hardware == null) return;

            Debug($"Hardware detected: {hardware.Name} ({hardware.HardwareType})");
            foreach (var sensor in hardware.Sensors)
            {
                Debug($"  Sensor: {sensor.Name} ({sensor.SensorType}) = {sensor.Value?.ToString() ?? "N/A"}");
            }
            foreach (var subHardware in hardware.SubHardware)
            {
                LogHardwareInfo(subHardware);
            }
        }

        public static void LogSystemInfo()
        {
            try
            {
                Info($"Operating System: {Environment.OSVersion}");
                Info($"System Directory: {Environment.SystemDirectory}");
                Info($"Processor Count: {Environment.ProcessorCount}");
                Info($"System Page Size: {Environment.SystemPageSize} bytes");
                Info($"Working Set: {Environment.WorkingSet / 1024 / 1024} MB");
                Info($"Logging to file: {FullLogPath}");
            }
            catch (Exception ex)
            {
                Error("Failed to log system information", ex);
            }
        }
    }
}