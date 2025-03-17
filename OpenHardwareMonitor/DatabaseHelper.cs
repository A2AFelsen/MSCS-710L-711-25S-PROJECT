using System;
using System.Data.SQLite; // If this is not working, install SQLite via "NuGet" package manager
// Tools -> NuGet Package Manager -> Manage NuGet Packages for Solution -> Browse for System.Data.SQLite

namespace OpenHardwareMonitor
{
    static class DatabaseHelper
    {
        private static SQLiteConnection dbConnection;

        public static void InitializeDatabase()
        {
            dbConnection = new SQLiteConnection("Data Source=metrics.db;Version=3;");
            dbConnection.Open();

            // Create component table
            string createComponentTableQuery = @"
                CREATE TABLE IF NOT EXISTS component (
                    serial_number VARCHAR(128) PRIMARY KEY,
                    device_type TEXT NOT NULL,
                    v_ram INTEGER,
                    stock_core_speed REAL,
                    stock_memory_speed REAL
                );";
            ExecuteNonQuery(createComponentTableQuery);

            // Create component_statistic table
            string createComponentStatisticTableQuery = @"
                CREATE TABLE IF NOT EXISTS component_statistic (
                    serial_number VARCHAR(128),
                    timestamp DATETIME,
                    machine_state TEXT NOT NULL,
                    temperature REAL,
                    usage REAL,
                    power_consumption REAL,
                    core_speed REAL,
                    memory_speed REAL,
                    total_ram REAL,
                    end_of_life DATETIME,
                    PRIMARY KEY (serial_number, timestamp),
                    FOREIGN KEY (serial_number) REFERENCES component(serial_number)
                );";
            ExecuteNonQuery(createComponentStatisticTableQuery);

            // Create process table
            string createProcessTableQuery = @"
                CREATE TABLE IF NOT EXISTS process (
                    pid INTEGER,
                    timestamp DATETIME,
                    cpu_usage REAL,
                    memory_usage REAL,
                    end_of_life DATETIME,
                    PRIMARY KEY (pid, timestamp)
                );";
            ExecuteNonQuery(createProcessTableQuery);
        }

        public static bool ComponentExists(string serialNumber)
        {
            string query = "SELECT COUNT(*) FROM component WHERE serial_number = @serialNumber;";
            using (var command = new SQLiteCommand(query, dbConnection))
            {
                command.Parameters.AddWithValue("@serialNumber", serialNumber);
                int count = Convert.ToInt32(command.ExecuteScalar());
                return count > 0;
            }
        }

        public static void InsertComponent(string serialNumber, string deviceType, int? vRam, float? stockCoreSpeed, float? stockMemorySpeed)
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

        public static void InsertComponentStatistic(string serialNumber, DateTime timestamp, string machineState, float temperature, float usage, float? powerConsumption, float? coreSpeed, float? memorySpeed, float totalRam, DateTime endOfLife)
        {
            string insertQuery = @"
                INSERT INTO component_statistic (serial_number, timestamp, machine_state, temperature, usage, power_consumption, core_speed, memory_speed, total_ram, end_of_life)
                VALUES (@serialNumber, @timestamp, @machineState, @temperature, @usage, @powerConsumption, @coreSpeed, @memorySpeed, @totalRam, @endOfLife)";
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

        public static void InsertProcess(int pid, DateTime timestamp, float cpuUsage, float memoryUsage, DateTime endOfLife)
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

        public static void ClearDatabase()
        {
            string[] tables = { "component", "component_statistic", "process" };
            foreach (var table in tables)
            {
                string clearQuery = $"DELETE FROM {table};";
                ExecuteNonQuery(clearQuery);
            }
        }

        public static void CloseConnection()
        {
            dbConnection?.Close();
        }

        private static void ExecuteNonQuery(string query)
        {
            using (var command = new SQLiteCommand(query, dbConnection))
            {
                command.ExecuteNonQuery();
            }
        }
    }
}