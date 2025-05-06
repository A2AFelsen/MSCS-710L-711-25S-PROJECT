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
        private static readonly string LogFileName = "OHM_Log.log"; // Single log file
        private static string FullLogPath;
        private static readonly LogLevel MinimumLogLevel;
        private static readonly long MaxLogFileSize = 5 * 1024 * 1024; // 5MB
        private static readonly int MaxArchivedLogs = 10;
        private static readonly object fileLock = new object();
        private static readonly Mutex globalMutex = new Mutex(false, @"Global\OHM_Logger_Mutex");

        static Logger()
        {
            // Initialize from configuration
            LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Enum.TryParse(ConfigurationManager.AppSettings["MinimumLogLevel"], out MinimumLogLevel);

            // Initialize log file
            InitializeLogFile();

            // Initial log entry
            Info("Logger initialized");
        }

        private static void InitializeLogFile()
        {
            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(LogDirectory);

                // Set full path
                FullLogPath = Path.Combine(LogDirectory, LogFileName);

                // Create empty log file if it doesn't exist
                if (!File.Exists(FullLogPath))
                {
                    using (File.Create(FullLogPath)) { }
                }

                // Clean up old archived logs
                CleanUpOldArchives();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logger initialization failed: {ex.Message}");
            }
        }

        private static void CleanUpOldArchives()
        {
            try
            {
                var archiveFiles = Directory.GetFiles(LogDirectory, "OHM_Log_*.log");
                if (archiveFiles.Length > MaxArchivedLogs)
                {
                    Array.Sort(archiveFiles);
                    for (int i = 0; i < archiveFiles.Length - MaxArchivedLogs; i++)
                    {
                        try { File.Delete(archiveFiles[i]); } catch { }
                    }
                }
            }
            catch { }
        }

        private static void RotateLogFileIfNeeded()
        {
            try
            {
                var fileInfo = new FileInfo(FullLogPath);
                if (fileInfo.Exists && fileInfo.Length > MaxLogFileSize)
                {
                    string archivePath = Path.Combine(
                        LogDirectory,
                        $"OHM_Log_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                    File.Move(FullLogPath, archivePath);
                    using (File.Create(FullLogPath)) { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Log rotation failed: {ex.Message}");
            }
        }

        public static void Log(LogLevel level, string message, Exception exception = null)
        {
            if (level < MinimumLogLevel)
                return;

            string logEntry = FormatLogEntry(level, message, exception);
            string detailedEntry = FormatDetailedEntry(level, message, exception);

            // Write to file
            WriteToFile(detailedEntry);
        }

        private static void WriteToFile(string message)
        {
            bool mutexAcquired = false;
            try
            {
                mutexAcquired = globalMutex.WaitOne(100); // 100ms timeout
                if (!mutexAcquired)
                {
                    Console.WriteLine("Warning: Log write skipped due to mutex timeout");
                    return;
                }

                lock (fileLock)
                {
                    RotateLogFileIfNeeded();
                    File.AppendAllText(FullLogPath, message + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Log write failed: {ex.Message}");
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

        private static string FormatDetailedEntry(LogLevel level, string message, Exception exception)
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