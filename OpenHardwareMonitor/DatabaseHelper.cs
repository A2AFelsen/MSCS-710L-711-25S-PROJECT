using System;
using System.Data.SQLite; // If this is not working, install SQLite via "NuGet" package manager
// Tools -> NuGet Package Manager -> Manage NuGet Packages for Solution -> Browse for System.Data.SQLite

namespace OpenHardwareMonitor
{
    internal static class DatabaseHelper
    {
        private static SQLiteConnection dbConnection;
        private static readonly object _lock = new object();

        public static void InitializeDatabase()
        {
            try
            {
                lock (_lock)
                {
                    dbConnection = new SQLiteConnection("Data Source=metrics.db;Version=3;FailIfMissing=False;");
                    dbConnection.Open();

                    // Create component table
                    ExecuteNonQueryWithRetry(@"
                        CREATE TABLE IF NOT EXISTS component (
                            serial_number VARCHAR(128) PRIMARY KEY,
                            device_type TEXT NOT NULL,
                            v_ram INTEGER,
                            stock_core_speed REAL,
                            stock_memory_speed REAL
                        );");

                    // Create component_statistic table
                    ExecuteNonQueryWithRetry(@"
                        CREATE TABLE IF NOT EXISTS component_statistic (
                            serial_number VARCHAR(128),
                            timestamp DATETIME,
                            machine_state TEXT NOT NULL,
                            temperature REAL NOT NULL,
                            usage REAL NOT NULL,
                            power_consumption REAL,
                            core_speed REAL,
                            memory_speed REAL,
                            total_ram REAL,
                            end_of_life DATETIME NOT NULL,
                            PRIMARY KEY (serial_number, timestamp),
                            FOREIGN KEY (serial_number) REFERENCES component(serial_number)
                        );");

                    // Create process table
                    ExecuteNonQueryWithRetry(@"
                        CREATE TABLE IF NOT EXISTS process (
                            pid INTEGER,
                            timestamp DATETIME,
                            cpu_usage REAL NOT NULL,
                            memory_usage REAL NOT NULL,
                            end_of_life DATETIME NOT NULL,
                            PRIMARY KEY (pid, timestamp)
                        );");
                }
            }
            catch (Exception ex)
            {
                throw new DatabaseInitializationException("Failed to initialize database", ex);
            }
        }

        public static bool ComponentExists(string serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
                throw new ArgumentException("Serial number cannot be null or empty", nameof(serialNumber));

            try
            {
                lock (_lock)
                {
                    string query = "SELECT COUNT(*) FROM component WHERE serial_number = @serialNumber;";
                    using (var command = new SQLiteCommand(query, dbConnection))
                    {
                        command.Parameters.AddWithValue("@serialNumber", serialNumber);
                        var result = command.ExecuteScalar();
                        return Convert.ToInt32(result) > 0;
                    }
                }
            }
            catch (SQLiteException ex)
            {
                throw new DatabaseOperationException("Failed to check component existence", ex);
            }
        }

        public static void InsertComponent(string serialNumber, string deviceType, int? vRam, float? stockCoreSpeed, float? stockMemorySpeed)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
                throw new ArgumentException("Serial number cannot be null or empty", nameof(serialNumber));
            if (string.IsNullOrWhiteSpace(deviceType))
                throw new ArgumentException("Device type cannot be null or empty", nameof(deviceType));

            try
            {
                lock (_lock)
                {
                    string insertQuery = @"
                        INSERT OR IGNORE INTO component (serial_number, device_type, v_ram, stock_core_speed, stock_memory_speed)
                        VALUES (@serialNumber, @deviceType, @vRam, @stockCoreSpeed, @stockMemorySpeed)";

                    using (var command = new SQLiteCommand(insertQuery, dbConnection))
                    {
                        command.Parameters.AddWithValue("@serialNumber", serialNumber);
                        command.Parameters.AddWithValue("@deviceType", deviceType);
                        command.Parameters.AddWithValue("@vRam", (object)vRam ?? DBNull.Value);
                        command.Parameters.AddWithValue("@stockCoreSpeed", (object)stockCoreSpeed ?? DBNull.Value);
                        command.Parameters.AddWithValue("@stockMemorySpeed", (object)stockMemorySpeed ?? DBNull.Value);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (SQLiteException ex)
            {
                throw new DatabaseOperationException("Failed to insert component", ex);
            }
        }

        public static void InsertComponentStatistic(string serialNumber, DateTime timestamp, string machineState,
            float temperature, float usage, float? powerConsumption, float? coreSpeed,
            float? memorySpeed, float totalRam, DateTime endOfLife)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
                throw new ArgumentException("Serial number cannot be null or empty", nameof(serialNumber));
            if (string.IsNullOrWhiteSpace(machineState))
                throw new ArgumentException("Machine state cannot be null or empty", nameof(machineState));

            try
            {
                lock (_lock)
                {
                    string insertQuery = @"
                        INSERT OR REPLACE INTO component_statistic
                        (serial_number, timestamp, machine_state, temperature, usage, power_consumption, 
                         core_speed, memory_speed, total_ram, end_of_life)
                        VALUES (@serialNumber, @timestamp, @machineState, @temperature, @usage, 
                                @powerConsumption, @coreSpeed, @memorySpeed, @totalRam, @endOfLife)";

                    using (var command = new SQLiteCommand(insertQuery, dbConnection))
                    {
                        command.Parameters.AddWithValue("@serialNumber", serialNumber);
                        command.Parameters.AddWithValue("@timestamp", timestamp);
                        command.Parameters.AddWithValue("@machineState", machineState);
                        command.Parameters.AddWithValue("@temperature", temperature);
                        command.Parameters.AddWithValue("@usage", usage);
                        command.Parameters.AddWithValue("@powerConsumption", (object)powerConsumption ?? DBNull.Value);
                        command.Parameters.AddWithValue("@coreSpeed", (object)coreSpeed ?? DBNull.Value);
                        command.Parameters.AddWithValue("@memorySpeed", (object)memorySpeed ?? DBNull.Value);
                        command.Parameters.AddWithValue("@totalRam", totalRam);
                        command.Parameters.AddWithValue("@endOfLife", endOfLife);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (SQLiteException ex)
            {
                throw new DatabaseOperationException("Failed to insert component statistic", ex);
            }
        }

        public static void InsertProcess(int pid, DateTime timestamp, float cpuUsage, float memoryUsage, DateTime endOfLife)
        {
            try
            {
                lock (_lock)
                {
                    string insertQuery = @"
                        INSERT INTO process (pid, timestamp, cpu_usage, memory_usage, end_of_life)
                        VALUES (@pid, @timestamp, @cpuUsage, @memoryUsage, @endOfLife)";

                    using (var command = new SQLiteCommand(insertQuery, dbConnection))
                    {
                        command.Parameters.AddWithValue("@pid", pid);
                        command.Parameters.AddWithValue("@timestamp", timestamp);
                        command.Parameters.AddWithValue("@cpuUsage", cpuUsage);
                        command.Parameters.AddWithValue("@memoryUsage", memoryUsage);
                        command.Parameters.AddWithValue("@endOfLife", endOfLife);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (SQLiteException ex)
            {
                throw new DatabaseOperationException("Failed to insert process", ex);
            }
        }

        public static void ClearDatabase()
        {
            try
            {
                lock (_lock)
                {
                    string[] tables = { "component", "component_statistic", "process" };
                    foreach (var table in tables)
                    {
                        ExecuteNonQueryWithRetry($"DELETE FROM {table};");
                    }
                }
            }
            catch (SQLiteException ex)
            {
                throw new DatabaseOperationException("Failed to clear database", ex);
            }
        }

        public static void CloseConnection()
        {
            lock (_lock)
            {
                try
                {
                    dbConnection?.Close();
                    dbConnection?.Dispose();
                    dbConnection = null;
                }
                catch (SQLiteException ex)
                {
                    throw new DatabaseOperationException("Failed to close database connection", ex);
                }
            }
        }

        private static void ExecuteNonQueryWithRetry(string query, int maxRetries = 3)
        {
            int retryCount = 0;
            while (true)
            {
                try
                {
                    ExecuteNonQuery(query);
                    return;
                }
                catch (SQLiteException) when (retryCount < maxRetries)
                {
                    retryCount++;
                    System.Threading.Thread.Sleep(100 * retryCount);
                }
            }
        }

        private static void ExecuteNonQuery(string query)
        {
            using (var command = new SQLiteCommand(query, dbConnection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

    public class DatabaseInitializationException : Exception
    {
        public DatabaseInitializationException(string message, Exception inner)
            : base(message, inner) { }
    }

    public class DatabaseOperationException : Exception
    {
        public DatabaseOperationException(string message, Exception inner)
            : base(message, inner) { }
    }
}
