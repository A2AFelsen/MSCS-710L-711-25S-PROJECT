using System;
using System.Data.SQLite; // If this is not working, install SQLite via "NuGet" package manager
// Tools -> NuGet Package Manager -> Manage NuGet Packages for Solution -> Browse for System.Data.SQLite

namespace OpenHardwareMonitorExample
{
    static class DatabaseHelper
    {
        private static SQLiteConnection dbConnection;

        public static void InitializeDatabase()
        {
            dbConnection = new SQLiteConnection("Data Source=metrics.db;Version=3;");
            dbConnection.Open();

            string createTableQuery = @"CREATE TABLE IF NOT EXISTS component_statistic (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                temperature REAL,
                usage REAL,
                power_consumption REAL,
                core_speed REAL,
                memory_speed REAL
            );";
            using (var command = new SQLiteCommand(createTableQuery, dbConnection))
            {
                command.ExecuteNonQuery();
            }
        }

        public static void InsertMetrics(float? temperature, float? usage, float? power, float? coreSpeed, float? memorySpeed)
        {
            string insertQuery = "INSERT INTO component_statistic (temperature, usage, power_consumption, core_speed, memory_speed) VALUES (@temperature, @usage, @power, @coreSpeed, @memorySpeed)";
            using (var command = new SQLiteCommand(insertQuery, dbConnection))
            {
                command.Parameters.AddWithValue("@temperature", (object)temperature ?? DBNull.Value);
                command.Parameters.AddWithValue("@usage", (object)usage ?? DBNull.Value);
                command.Parameters.AddWithValue("@power", (object)power ?? DBNull.Value);
                command.Parameters.AddWithValue("@coreSpeed", (object)coreSpeed ?? DBNull.Value);
                command.Parameters.AddWithValue("@memorySpeed", (object)memorySpeed ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

        public static void CloseConnection()
        {
            dbConnection?.Close();
        }
    }
}
