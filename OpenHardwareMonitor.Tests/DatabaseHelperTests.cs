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
            var lifetime = TimeSpan.FromDays(365); // 1 year lifetime
            var expectedEndOfLife = timestamp.Add(lifetime);

            // First insert the component
            DatabaseHelper.InsertComponent(serial, "GPU", 8192, 1200f, 7000f);

            // Act
            DatabaseHelper.InsertComponentStatistic(
                serial, timestamp, "Active", 65.5f, 45.0f,
                150.0f, 1350.0f, 7200.0f, 16.0f, lifetime);

            // Assert data was stored
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM component_statistic WHERE serial_number = @serial",
                _testConnection))
            {
                cmd.Parameters.AddWithValue("@serial", serial);
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                Assert.AreEqual(1, count);
            }

            // Verify end_of_life was calculated correctly
            using (var cmd = new SQLiteCommand(
                "SELECT end_of_life FROM component_statistic WHERE serial_number = @serial",
                _testConnection))
            {
                cmd.Parameters.AddWithValue("@serial", serial);
                var actualEndOfLife = Convert.ToDateTime(cmd.ExecuteScalar());
                Assert.AreEqual(expectedEndOfLife.Date, actualEndOfLife.Date);
            }
        }

        [TestMethod]
        public void InsertProcess_StoresDataCorrectly()
        {
            // Arrange
            var timestamp = DateTime.Now;
            var lifetime = TimeSpan.FromDays(30); // 30 days lifetime
            var expectedEndOfLife = timestamp.Add(lifetime);

            // Act
            DatabaseHelper.InsertProcess(1234, timestamp, 25.5f, 1024.0f, lifetime);

            // Assert
            using (var cmd = new SQLiteCommand(
                "SELECT cpu_usage FROM process WHERE pid = 1234",
                _testConnection))
            {
                var usage = Convert.ToSingle(cmd.ExecuteScalar());
                Assert.AreEqual(25.5f, usage);
            }

            // Verify end_of_life was calculated correctly
            using (var cmd = new SQLiteCommand(
                "SELECT end_of_life FROM process WHERE pid = 1234",
                _testConnection))
            {
                var actualEndOfLife = Convert.ToDateTime(cmd.ExecuteScalar());
                Assert.AreEqual(expectedEndOfLife.Date, actualEndOfLife.Date);
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

        [TestMethod]
        public void PruneOldData_RemovesExpiredRecords()
        {
            // Arrange
            var oldTimestamp = DateTime.Now.AddDays(-400); // Older than 1 year
            var recentTimestamp = DateTime.Now.AddDays(-1); // Recent

            // Insert test data
            DatabaseHelper.InsertComponent("OLD", "CPU", null, null, null);
            DatabaseHelper.InsertComponent("RECENT", "GPU", null, null, null);

            // Insert statistics with different timestamps
            DatabaseHelper.InsertComponentStatistic(
                "OLD", oldTimestamp, "Active", 50.0f, 30.0f,
                null, null, null, 16.0f, TimeSpan.FromDays(365));

            DatabaseHelper.InsertComponentStatistic(
                "RECENT", recentTimestamp, "Active", 60.0f, 40.0f,
                null, null, null, 16.0f, TimeSpan.FromDays(365));

            // Insert processes with different timestamps
            DatabaseHelper.InsertProcess(1111, oldTimestamp, 10.0f, 100.0f, TimeSpan.FromDays(365));
            DatabaseHelper.InsertProcess(2222, recentTimestamp, 20.0f, 200.0f, TimeSpan.FromDays(365));

            // Act - prune data older than 1 year
            DatabaseHelper.PruneOldData(TimeSpan.FromDays(365));

            // Assert
            // Verify old records were removed
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM component_statistic WHERE serial_number = 'OLD'",
                _testConnection))
            {
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                Assert.AreEqual(0, count);
            }

            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM process WHERE pid = 1111",
                _testConnection))
            {
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                Assert.AreEqual(0, count);
            }

            // Verify recent records remain
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM component_statistic WHERE serial_number = 'RECENT'",
                _testConnection))
            {
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                Assert.AreEqual(1, count);
            }

            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM process WHERE pid = 2222",
                _testConnection))
            {
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                Assert.AreEqual(1, count);
            }

            // Verify orphaned component was removed
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM component WHERE serial_number = 'OLD'",
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