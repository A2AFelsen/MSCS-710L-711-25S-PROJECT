using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Data.SQLite;
using System.Reflection;

namespace OpenHardwareMonitor.Tests
{
    [TestClass]
    public class DatabaseHelperTests : IDisposable
    {
        private SQLiteConnection _testConnection;
        private bool _disposed;

        public DatabaseHelperTests()
        {
            // Create in-memory database
            _testConnection = new SQLiteConnection("Data Source=:memory:;Version=3;New=True;");
            _testConnection.Open();

            // Inject into DatabaseHelper using reflection
            var field = typeof(DatabaseHelper).GetField("dbConnection",
                BindingFlags.Static | BindingFlags.NonPublic);
            field.SetValue(null, _testConnection);
            typeof(DatabaseHelper).GetField("isInjectedConnection", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        }

        [TestInitialize]
        public void TestInitialize()
        {
            // Ensure clean state for each test
            DatabaseHelper.InitializeDatabase();
        }

        [TestMethod]
        public void InitializeDatabase_CreatesRequiredTables()
        {
            // Act (already initialized in TestInitialize)

            // Assert tables exist
            using (var cmd = new SQLiteCommand(
                "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name",
                _testConnection))
            {
                var reader = cmd.ExecuteReader();
                var tables = new System.Collections.Generic.List<string>();
                while (reader.Read()) tables.Add(reader.GetString(0));

                CollectionAssert.Contains(tables, "component");
                CollectionAssert.Contains(tables, "component_statistic");
                CollectionAssert.Contains(tables, "process");
            }
        }

        [TestMethod]
        public void ComponentExists_ReturnsFalse_ForNewDatabase()
        {
            // Arrange
            const string testSerial = "NON_EXISTENT_123";

            // Act
            var result = DatabaseHelper.ComponentExists(testSerial);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void InsertComponent_ThenComponentExists_ReturnsTrue()
        {
            // Arrange
            const string serial = "CPU_TEST_001";
            const string type = "CPU";

            // Act
            DatabaseHelper.InsertComponent(serial, type, null, 3.5f, null);
            var exists = DatabaseHelper.ComponentExists(serial);

            // Assert
            Assert.IsTrue(exists);
        }

        [TestMethod]
        public void InsertComponentStatistic_StoresDataCorrectly()
        {
            // Arrange
            const string serial = "GPU_TEST_001";
            var timestamp = DateTime.Now;
            var endOfLife = timestamp.AddYears(1);

            // First insert the component
            DatabaseHelper.InsertComponent(serial, "GPU", 8192, 1200f, 7000f);

            // Act
            DatabaseHelper.InsertComponentStatistic(
                serial, timestamp, "Active", 65.5f, 45.0f,
                150.0f, 1350.0f, 7200.0f, 16.0f, endOfLife);

            // Assert data was stored
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM component_statistic WHERE serial_number = @serial",
                _testConnection))
            {
                cmd.Parameters.AddWithValue("@serial", serial);
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                Assert.AreEqual(1, count);
            }
        }

        [TestMethod]
        public void InsertProcess_StoresDataCorrectly()
        {
            // Arrange
            var timestamp = DateTime.Now;
            var endOfLife = timestamp.AddYears(1);

            // Act
            DatabaseHelper.InsertProcess(1234, timestamp, 25.5f, 1024.0f, endOfLife);

            // Assert
            using (var cmd = new SQLiteCommand(
                "SELECT cpu_usage FROM process WHERE pid = 1234",
                _testConnection))
            {
                var usage = Convert.ToSingle(cmd.ExecuteScalar());
                Assert.AreEqual(25.5f, usage);
            }
        }

        [TestMethod]
        public void ClearDatabase_RemovesAllData()
        {
            // Arrange - add test data
            DatabaseHelper.InsertComponent("TEST", "CPU", null, null, null);

            // Act
            DatabaseHelper.ClearDatabase();

            // Assert
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM component",
                _testConnection))
            {
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                Assert.AreEqual(0, count);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Clean up database
                DatabaseHelper.ClearDatabase();
                DatabaseHelper.CloseConnection();

                _testConnection?.Close();
                _testConnection?.Dispose();

                // Reset static field
                var field = typeof(DatabaseHelper).GetField("dbConnection",
                    BindingFlags.Static | BindingFlags.NonPublic);
                field.SetValue(null, null);
            }
            _disposed = true;
        }
    }
}