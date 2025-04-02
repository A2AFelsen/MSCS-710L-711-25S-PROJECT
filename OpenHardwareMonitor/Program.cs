using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Security.Principal;
using System.Timers;
using OpenHardwareMonitor.Hardware;

namespace OpenHardwareMonitor
{
    class Program
    {
        private static Computer computer;
        private static PerformanceCounter cpuCounter;

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                if (args[0] == "init-db")
                {
                    DatabaseHelper.InitializeDatabase();
                    Console.WriteLine("Database initialized.");
                    return;
                }
                else if (args[0] == "clear-db")
                {
                    DatabaseHelper.InitializeDatabase(); // Ensure the database exists
                    DatabaseHelper.ClearDatabase();
                    Console.WriteLine("Database cleared.");
                    return;
                }
            }

            if (!IsRunningAsAdministrator())
            {
                RelaunchAsAdministrator();
                return;
            }

            // Initialize the database if it doesn't already exist
            DatabaseHelper.InitializeDatabase();

            computer = new Computer
            {
                CPUEnabled = true,
                GPUEnabled = true,
                RAMEnabled = true,
                FanControllerEnabled = true,
                HDDEnabled = true,
                MainboardEnabled = true
            };

            computer.Open();
            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

            // Create timer but don't start yet
            System.Timers.Timer timer = new System.Timers.Timer(30000);
            timer.Elapsed += OnTimedEvent;
            timer.AutoReset = true;

            // First collection
            OnTimedEvent(null, null);

            // Start timer after first collection
            timer.Enabled = true;

            Console.WriteLine("Press Enter to exit the program.");
            Console.ReadLine();

            timer.Stop();
            timer.Dispose();
            computer.Close();
            DatabaseHelper.CloseConnection(); // Close the database connection when the program exits
        }

        private static void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            DateTime timestamp = DateTime.Now;
            Console.WriteLine($"Reading sensor data at {timestamp}");

            // Collect data for component_statistic and component tables
            foreach (var hardwareItem in computer.Hardware)
            {
                hardwareItem.Update();

                string serialNumber = GetHardwareSerialNumber(hardwareItem);
                string deviceType = hardwareItem.HardwareType.ToString();
                float temperature = 0;
                float powerConsumption = 0;
                float coreSpeed = 0;
                float memorySpeed = 0;
                int vRam = 0;
                float stockCoreSpeed = 0;
                float stockMemorySpeed = 0;

                // Get stock core and memory speeds based on hardware type
                switch (hardwareItem.HardwareType)
                {
                    case HardwareType.CPU:
                        stockCoreSpeed = GetCpuBaseClockSpeed(); // Fetch CPU base clock speed
                        break;
                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAti:
                        (stockCoreSpeed, stockMemorySpeed) = GetGpuBaseClockSpeeds(); // Fetch GPU base clock and memory speeds
                        break;
                    case HardwareType.RAM:
                        stockMemorySpeed = GetRamSpeed(); // Fetch RAM speed
                        break;
                    default:
                        // For other hardware types, set default values or skip
                        break;
                }

                foreach (var sensor in hardwareItem.Sensors)
                {
                    if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                    {
                        Console.WriteLine($"  Temperature Sensor Found: {sensor.Name}, Value: {sensor.Value}");
                        temperature = sensor.Value.Value;
                    }
                    else if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue)
                    {
                        powerConsumption = sensor.Value.Value;
                    }
                    else if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue)
                    {
                        if (hardwareItem.HardwareType == HardwareType.CPU)
                        {
                            coreSpeed = sensor.Value.Value;
                        }
                        else if (hardwareItem.HardwareType == HardwareType.GpuNvidia || hardwareItem.HardwareType == HardwareType.GpuAti)
                        {
                            memorySpeed = sensor.Value.Value;
                        }
                    }
                    else if (sensor.SensorType == SensorType.Data && sensor.Value.HasValue)
                    {
                        if (sensor.Name.Contains("GPU Memory"))
                        {
                            vRam = (int)sensor.Value.Value;
                        }
                    }
                }

                // Check if the component already exists in the database
                if (!DatabaseHelper.ComponentExists(serialNumber))
                {
                    // Insert component data into the database if it doesn't exist
                    DatabaseHelper.InsertComponent(serialNumber, deviceType, vRam, stockCoreSpeed, stockMemorySpeed);
                }

                // Insert component_statistic data into the database
                DatabaseHelper.InsertComponentStatistic(
                    serialNumber, // Use the same serial number for consistency
                    timestamp,
                    "Active", // Machine state (e.g., "Active")
                    temperature,
                    cpuCounter.NextValue(), // CPU usage
                    powerConsumption,
                    coreSpeed,
                    memorySpeed,
                    GetTotalRAM(), // Total RAM in GB
                    timestamp.AddYears(1) // End of life (1 year from now)
                );

                Console.WriteLine($"Component Statistic: SerialNumber={serialNumber}, Timestamp={timestamp}, MachineState=Active, Temperature={temperature}, CPUUsage={cpuCounter.NextValue()}, PowerConsumption={powerConsumption}, CoreSpeed={coreSpeed}, MemorySpeed={memorySpeed}, TotalRAM={GetTotalRAM()}, EndOfLife={timestamp.AddYears(1)}");
                Console.WriteLine($"Component: SerialNumber={serialNumber}, DeviceType={deviceType}, VRAM={vRam}, StockCoreSpeed={stockCoreSpeed}, StockMemorySpeed={stockMemorySpeed}");
            }

            // Collect data for process table
            Process[] processes = Process.GetProcesses();
            foreach (Process process in processes)
            {
                try
                {
                    int pid = process.Id;
                    float processCpuUsage = cpuCounter.NextValue();
                    float memoryUsage = process.WorkingSet64 / 1024f / 1024f; // Convert to MB
                    DateTime processEndOfLife = timestamp.AddYears(1);

                    DatabaseHelper.InsertProcess(pid, timestamp, processCpuUsage, memoryUsage, processEndOfLife);
                    Console.WriteLine($"Process: PID={pid}, Timestamp={timestamp}, CPUUsage={processCpuUsage}, MemoryUsage={memoryUsage}, EndOfLife={processEndOfLife}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accessing process {process.ProcessName}: {ex.Message}");
                }
            }
        }

        private static string GetHardwareSerialNumber(IHardware hardware)
        {
            string serialNumber;
            switch (hardware.HardwareType)
            {
                case HardwareType.CPU:
                    serialNumber = GetWMISerialNumber("Win32_Processor", "ProcessorId");
                    break;
                case HardwareType.GpuNvidia:
                case HardwareType.GpuAti:
                    serialNumber = GetWMISerialNumber("Win32_VideoController", "PNPDeviceID");
                    break;
                case HardwareType.RAM:
                    serialNumber = GetWMISerialNumber("Win32_PhysicalMemory", "SerialNumber");
                    break;
                case HardwareType.Mainboard:
                    serialNumber = GetWMISerialNumber("Win32_BaseBoard", "SerialNumber");
                    break;
                case HardwareType.HDD:
                    serialNumber = GetWMISerialNumber("Win32_DiskDrive", "SerialNumber");
                    break;
                default:
                    serialNumber = "Not Available";
                    break;
            }

            // If the serial number is not available, generate a deterministic identifier
            if (serialNumber == "Not Available" || string.IsNullOrWhiteSpace(serialNumber) || serialNumber == "Default string")
            {
                serialNumber = GenerateDeterministicSerialNumber(hardware);
            }

            return serialNumber;
        }

        private static string GenerateDeterministicSerialNumber(IHardware hardware)
        {
            // Use a combination of hardware properties to create a consistent identifier
            string identifier = $"{hardware.HardwareType}-{hardware.Name}";

            // Hash the identifier to ensure it's a consistent length and format
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identifier));
                return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 16); // Use first 16 chars of hash
            }
        }

        private static float GetTotalRAM()
        {
            float totalRam = 0;
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    ulong totalMemoryKB = Convert.ToUInt64(obj["TotalVisibleMemorySize"]);
                    totalRam = totalMemoryKB / 1024f / 1024f; // Convert KB to GB
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving RAM information: {ex.Message}");
            }
            return totalRam;
        }

        private static string GetWMISerialNumber(string wmiClass, string propertyName)
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {wmiClass}");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return obj[propertyName]?.ToString() ?? "Not Available";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving {wmiClass} serial number: {ex.Message}");
            }
            return "Not Available";
        }

        private static float GetCpuBaseClockSpeed()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return Convert.ToSingle(obj["MaxClockSpeed"]) / 1000f; // Convert MHz to GHz
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving CPU base clock speed: {ex.Message}");
            }
            return 0; // Default value if retrieval fails
        }

        private static (float coreSpeed, float memorySpeed) GetGpuBaseClockSpeeds()
        {
            // Example: Use predefined values or fetch from a library like NVAPI/ADL
            return (1200f, 7000f); // Placeholder values
        }

        private static float GetRamSpeed()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Speed FROM Win32_PhysicalMemory");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return Convert.ToSingle(obj["Speed"]); // Speed in MHz
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving RAM speed: {ex.Message}");
            }
            return 0; // Default value if retrieval fails
        }

        private static bool IsRunningAsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RelaunchAsAdministrator()
        {
            ProcessStartInfo procInfo = new ProcessStartInfo();
            procInfo.UseShellExecute = true;
            procInfo.FileName = Process.GetCurrentProcess().MainModule.FileName;
            procInfo.Verb = "runas";

            try
            {
                Process.Start(procInfo);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Console.WriteLine("You must run this program as an administrator.");
            }

            Environment.Exit(0);
        }
    }
}