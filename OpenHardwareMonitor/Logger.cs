using System;
using System.IO;
using System.Diagnostics;
using System.Configuration;
using System.Threading;

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
        private static readonly long MaxLogFileSize = 5 * 1024 * 1024; // 5MB
        private static readonly object fileLock = new object();
        private static readonly Mutex globalMutex = new Mutex(false, @"Global\OHM_Logger_Mutex");

        static Logger()
        {
            // Initialize from configuration (App.config)
            LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

            // Parse configuration settings with defaults
            Enum.TryParse(ConfigurationManager.AppSettings["MinimumLogLevel"], out MinimumLogLevel);

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

                // Create new log file with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                FullLogPath = Path.Combine(LogDirectory, $"{LogFilePrefix}{timestamp}{LogFileExtension}");

                // Create file if it doesn't exist (auto-closes handle)
                if (!File.Exists(FullLogPath))
                {
                    using (File.Create(FullLogPath)) { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to configure log file rotation: {ex.Message}");
            }
        }

        private static void RotateLogFileIfNeeded()
        {
            try
            {
                var fileInfo = new FileInfo(FullLogPath);
                if (fileInfo.Exists && fileInfo.Length > MaxLogFileSize)
                {
                    string newFilePath = Path.Combine(
                        LogDirectory,
                        $"{LogFilePrefix}{DateTime.Now:yyyyMMdd_HHmmss}{LogFileExtension}");
                    
                    File.Move(FullLogPath, newFilePath);
                    using (File.Create(FullLogPath)) { } // Create new empty log file
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


            bool mutexAcquired = false;
            try
            {
                // Wait up to 1 second to acquire the global mutex
                mutexAcquired = globalMutex.WaitOne(1000);
                if (!mutexAcquired)
                {
                    Console.WriteLine("Failed to acquire logging mutex");
                    return;
                }

                lock (fileLock)
                {
                    RotateLogFileIfNeeded();
                    
                    // Use File.AppendAllText which handles its own streams
                    File.AppendAllText(FullLogPath, detailedEntry + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
            finally
            {
                if (mutexAcquired)
                    globalMutex.ReleaseMutex();
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
    }
}