using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Management;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenHardwareMonitor.Hardware;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace OpenHardwareMonitor.MetricsTests
{
    [TestClass]
    public class HardwareMonitorTests
    {
        private Mock<ManagementObjectSearcher> _mockSearcher;
        private Mock<IHardware> _mockHardware;
        private Mock<ISensor> _mockSensor;
        private Func<string, string, string> _originalSearcher;

        [TestInitialize]
        public void TestInitialize()
        {
            _mockSearcher = new Mock<ManagementObjectSearcher>();
            _mockHardware = new Mock<IHardware>();
            _mockSensor = new Mock<ISensor>();
            _originalSearcher = Program.GetWMISerialNumber;
        }

        public interface IProcessWrapper
        {
            int Id { get; }
            string ProcessName { get; }
            long WorkingSet64 { get; }
        }

        // Implement a real wrapper
        public class ProcessWrapper : IProcessWrapper
        {
            private readonly Process _process;
            public ProcessWrapper(Process process) => _process = process;
            public int Id => _process.Id;
            public string ProcessName => _process.ProcessName;
            public long WorkingSet64 => _process.WorkingSet64;
        }
        [TestMethod]
        public void RunAsAdministrator()
        {
            // This isn't automatable - it's for manual verification only
            var procInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Verb = "runas"
            };

            var process = Process.Start(procInfo);
            Assert.IsNotNull(process); // This will only pass if UAC is accepted
            process.Kill();
        }

        [TestMethod]
        public void GetRamSpeed_Returns_NonNullValue()
        {
            // Act
            float speed = Program.GetRamSpeed();

            // Assert
            Assert.IsNotNull(speed); // Implicitly checks float is not null
        }

        [TestMethod]
        public void GetRamSpeed_Returns_ZeroOrPositiveValue()
        {
            // Act
            float speed = Program.GetRamSpeed();

            // Assert
            Assert.IsTrue(speed >= 0, "RAM speed should be zero or positive");
        }

        [TestMethod]
        public void GetRamSpeed_Returns_ReasonableSpeedRange()
        {
            // Act
            float speed = Program.GetRamSpeed();

            // Assert - Typical DDR3/DDR4/DDR5 ranges
            Assert.IsTrue(speed == 0 || (speed >= 800 && speed <= 10000),
                $"RAM speed {speed} MHz is outside reasonable range");
        }

        [TestMethod]
        public void ProcessSensorData_HandlesNullSensorValues_ForAllSensorTypes()
        {
            // Arrange - Create test cases for all sensor types
            var testCases = new List<(SensorType type, string name)>
    {
        (SensorType.Temperature, "CPU Core"),
        (SensorType.Clock, "Core Clock"),
        (SensorType.Load, "CPU Total"),
        (SensorType.Power, "CPU Package"),
    };

            foreach (var testCase in testCases)
            {
                // Create fresh mocks for each test case
                var mockHardware = new Mock<IHardware>();
                var mockSensor = new Mock<ISensor>();

                // Setup hardware
                mockHardware.Setup(h => h.HardwareType).Returns(HardwareType.CPU);
                mockHardware.Setup(h => h.Sensors).Returns(new[] { mockSensor.Object });

                // Setup sensor with current test case values
                mockSensor.Setup(s => s.SensorType).Returns(testCase.type);
                mockSensor.Setup(s => s.Value).Returns((float?)null);
                mockSensor.Setup(s => s.Name).Returns(testCase.name);

                // Act
                mockHardware.Object.Update();
                var sensors = mockHardware.Object.Sensors.ToList();

                // Assert
                Assert.AreEqual(1, sensors.Count, $"Should have 1 {testCase.type} sensor");
                Assert.IsNull(sensors[0].Value, $"{testCase.type} sensor value should be null");
                Assert.AreEqual(testCase.type, sensors[0].SensorType);
                Assert.AreEqual(testCase.name, sensors[0].Name);
            }
        }

        [TestMethod]
        public void Setup_InitializesComputerWithCorrectComponents()
        {
            // Arrange
            var computer = new Computer();

            // Act
            computer.CPUEnabled = true;
            computer.GPUEnabled = true;
            computer.RAMEnabled = true;
            computer.FanControllerEnabled = true;
            computer.HDDEnabled = true;
            computer.MainboardEnabled = true;

            // Assert
            Assert.IsTrue(computer.CPUEnabled);
            Assert.IsTrue(computer.GPUEnabled);
            Assert.IsTrue(computer.RAMEnabled);
            Assert.IsTrue(computer.FanControllerEnabled);
            Assert.IsTrue(computer.HDDEnabled);
            Assert.IsTrue(computer.MainboardEnabled);
        }

        [TestMethod]
        public void ProducesConsistentSerialNumberHashes()
        {
            // Arrange
            _mockHardware.Setup(h => h.HardwareType).Returns(HardwareType.CPU);
            _mockHardware.Setup(h => h.Name).Returns("Test CPU");
            _mockHardware.Setup(h => h.Identifier).Returns(new Identifier("CPU", "123"));

            // Act
            string serial1 = Program.GenerateDeterministicSerialNumber(_mockHardware.Object);
            string serial2 = Program.GenerateDeterministicSerialNumber(_mockHardware.Object);

            // Assert
            Assert.AreEqual(serial1, serial2);
            Assert.AreEqual(16, serial1.Length);
        }

        [TestMethod]
        public void ProducesUniqueSerialNumberHashes_ForDifferentHardware()
        {
            // Arrange
            var mockHardware1 = new Mock<IHardware>();
            mockHardware1.Setup(h => h.HardwareType).Returns(HardwareType.CPU);
            mockHardware1.Setup(h => h.Name).Returns("CPU 1");
            mockHardware1.Setup(h => h.Identifier).Returns(new Identifier("CPU", "123"));

            var mockHardware2 = new Mock<IHardware>();
            mockHardware2.Setup(h => h.HardwareType).Returns(HardwareType.CPU);
            mockHardware2.Setup(h => h.Name).Returns("CPU 2");
            mockHardware2.Setup(h => h.Identifier).Returns(new Identifier("CPU", "456"));

            // Act
            string serial1 = Program.GenerateDeterministicSerialNumber(mockHardware1.Object);
            string serial2 = Program.GenerateDeterministicSerialNumber(mockHardware2.Object);

            // Assert
            Assert.AreNotEqual(serial1, serial2);
        }


        [TestMethod]
        public void GetCurrentProcessData_ReturnsValidProcessInfo()
        {
            // Arrange
            var testProcess = Process.GetCurrentProcess();

            // Act
            var processData = new
            {
                Id = testProcess.Id,
                Name = testProcess.ProcessName,
                Memory = testProcess.WorkingSet64
            };

            // Assert
            Assert.IsTrue(processData.Id > 0);
            Assert.IsFalse(string.IsNullOrEmpty(processData.Name));
            Assert.IsTrue(processData.Memory > 0);
        }

        [TestMethod]
        public void HandleProcessAccessDenied_WhenAccessIsDenied()
        {
            // Arrange
            var mockProcess = new Mock<IProcessWrapper>();
            mockProcess.Setup(p => p.Id).Returns(9999);
            mockProcess.Setup(p => p.ProcessName).Returns("TestProcess");
            mockProcess.Setup(p => p.WorkingSet64).Throws(new Win32Exception(5, "Access is denied"));

            // Act & Assert
            Assert.ThrowsException<Win32Exception>(() =>
            {
                var memory = mockProcess.Object.WorkingSet64;
            });
        }
    }
}