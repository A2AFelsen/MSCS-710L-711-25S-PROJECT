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
        private static Computer Computer;
        private static PerformanceCounter cpuCounter;

        static void Main(string[] args)
        {
            try
            {
                if (args.Length > 0)
                {
                    try
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
                    catch (DatabaseInitializationException ex)
                    {
                        Console.WriteLine($"Database initialization failed: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }
                        return;
                    }
                    catch (DatabaseOperationException ex)
                    {
                        Console.WriteLine($"Database operation failed: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }
                        return;
                    }
                }

                if (!IsRunningAsAdministrator())
                {
                    RelaunchAsAdministrator();
                    return;
                }

                // Initialize the database if it doesn't already exist
                try
                {
                    DatabaseHelper.InitializeDatabase();
                }
                catch (DatabaseInitializationException ex)
                {
                    Console.WriteLine($"Failed to initialize database: {ex.Message}");
                    Console.WriteLine("The application will exit.");
                    return;
                }

                Computer = new Computer
                {
                    CPUEnabled = true,
                    GPUEnabled = true,
                    RAMEnabled = true,
                    FanControllerEnabled = true,
                    HDDEnabled = true,
                    MainboardEnabled = true
                };

                Computer.Open();
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

                // Create timer but don't start yet
                Timer timer = new Timer(5000);
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
                Computer.Close();
                DatabaseHelper.CloseConnection();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine("The application will exit.");
                Environment.Exit(1);
            }
        }

        private static void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            DateTime timestamp = DateTime.Now;
            Console.WriteLine($"\n=== Reading sensor data at {timestamp} ===");

            try
            {
                // Collect data for component_statistic and component tables
                foreach (var hardwareItem in Computer.Hardware)
                {
                    hardwareItem.Update();

                    string serialNumber = GetHardwareSerialNumber(hardwareItem);
                    string deviceType = hardwareItem.HardwareType.ToString();
                    float temperature = 0;
                    float powerConsumption = 0;
                    float load = 0;
                    float currentCoreClock = 0;
                    float currentMemoryClock = 0;
                    int vRam = 0;
                    float stockCoreSpeed = 0;
                    float stockMemorySpeed = 0;

                    // Get stock speeds based on hardware type
                    switch (hardwareItem.HardwareType)
                    {
                        case HardwareType.CPU:
                            stockCoreSpeed = GetCpuBaseClockSpeed();
                            break;
                        case HardwareType.GpuNvidia:
                        case HardwareType.GpuAti:
                            (stockCoreSpeed, stockMemorySpeed) = GetGpuBaseClockSpeeds();
                            break;
                        case HardwareType.RAM:
                            stockMemorySpeed = GetRamSpeed();
                            break;
                    }

                    // Process all sensors for this hardware
                    Console.WriteLine($"\n{hardwareItem.Name} ({hardwareItem.HardwareType})");
                    foreach (var sensor in hardwareItem.Sensors)
                    {

                        // Temperature monitoring
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            temperature = sensor.Value.Value;
                            Console.WriteLine($"  Temperature: {sensor.Name} = {temperature}°C");
                        }
                        // Power monitoring
                        else if (sensor.SensorType == SensorType.Power)
                        {
                            powerConsumption = sensor.Value.Value;
                            Console.WriteLine($"  Power: {sensor.Name} = {powerConsumption}W");
                        }
                        // Load monitoring
                        else if (sensor.SensorType == SensorType.Load)
                        {
                            load = sensor.Value.Value;
                            Console.WriteLine($"  Load: {sensor.Name} = {load}%");
                        }
                        // Clock monitoring
                        else if (sensor.SensorType == SensorType.Clock)
                        {
                            if (sensor.Name.Contains("Core") || sensor.Name.Contains("GPU Core"))
                            {
                                currentCoreClock = sensor.Value.Value;
                            }
                            else if (sensor.Name.Contains("Memory") || sensor.Name.Contains("GPU Memory"))
                            {
                                currentMemoryClock = sensor.Value.Value;
                            }
                        }
                        // VRAM monitoring
                        else if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("GPU Memory"))
                        {
                            vRam = (int)sensor.Value.Value;
                            Console.WriteLine($"  VRAM: {vRam} MB");
                        }
                    }

                    try
                    {
                        // Insert or update component data
                        if (!DatabaseHelper.ComponentExists(serialNumber))
                        {
                            DatabaseHelper.InsertComponent(
                                serialNumber,
                                deviceType,
                                vRam,
                                stockCoreSpeed,
                                stockMemorySpeed
                            );
                        }

                        // Insert component statistics
                        DatabaseHelper.InsertComponentStatistic(
                            serialNumber,
                            timestamp,
                            "Active",
                            temperature,
                            load,
                            powerConsumption,
                            currentCoreClock,
                            currentMemoryClock,
                            GetTotalRAM(),
                            timestamp.AddYears(1)
                        );

                        Console.WriteLine($"Component Statistic: SerialNumber={serialNumber}, Timestamp={timestamp}, MachineState=Active, Temperature={temperature}, load={load}, PowerConsumption={powerConsumption}, CoreSpeed={currentCoreClock}, MemorySpeed={currentMemoryClock}, TotalRAM={GetTotalRAM()}, EndOfLife={timestamp.AddYears(1)}");
                        Console.WriteLine($"Component: SerialNumber={serialNumber}, DeviceType={deviceType}, VRAM={vRam}, StockCoreSpeed={stockCoreSpeed}, StockMemorySpeed={stockMemorySpeed}");
                    }
                    catch (DatabaseOperationException ex)
                    {
                        Console.WriteLine($"Failed to update database for {deviceType} {serialNumber}: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }
                    }
                }

                // Process monitoring
                Console.WriteLine("\nProcess Monitoring:");
                Process[] processes = Process.GetProcesses();
                //var currentProcessCpuUsage = new Dictionary<int, (TimeSpan cpuTime, DateTime timestamp)>(); <- unused?
                foreach (Process process in processes)
                {
                    try
                    {
                        float cpuUsage = 0;

                        using (var searcher = new ManagementObjectSearcher("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfProc_Process WHERE Name='" + process.ProcessName + "'"))
                        {
                            foreach (var result in searcher.Get())
                            {
                                cpuUsage = float.Parse(result["PercentProcessorTime"].ToString());
                            }
                        }
                        float memoryUsage = process.WorkingSet64 / 1024f / 1024f; // MB

                        try
                        {
                            DatabaseHelper.InsertProcess(
                                process.Id,
                                timestamp,
                                cpuUsage,
                                memoryUsage,
                                timestamp.AddYears(1)
                            );

                            Console.WriteLine($"  PID {process.Id}: CPU={cpuUsage}%, RAM={memoryUsage}MB");
                        }
                        catch (DatabaseOperationException ex)
                        {
                            Console.WriteLine($"  Failed to insert process {process.ProcessName} (PID: {process.Id}): {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error accessing process {process.ProcessName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during data collection: {ex.Message}");
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

            if (string.IsNullOrWhiteSpace(serialNumber) || serialNumber == "Not Available" || serialNumber == "Default string")
            {
                serialNumber = GenerateDeterministicSerialNumber(hardware);
            }

            return serialNumber;
        }

        private static string GenerateDeterministicSerialNumber(IHardware hardware)
        {
            string identifier = $"{hardware.HardwareType}-{hardware.Name}-{hardware.Identifier}";
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identifier));
                return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 16);
            }
        }

        private static float GetTotalRAM()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return Convert.ToUInt64(obj["TotalVisibleMemorySize"]) / 1024f / 1024f;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving RAM information: {ex.Message}");
            }
            return 0;
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
                    return Convert.ToSingle(obj["MaxClockSpeed"]) / 1000f;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving CPU base clock speed: {ex.Message}");
            }
            return 0;
        }

        private static (float coreSpeed, float memorySpeed) GetGpuBaseClockSpeeds()
        {
            // These values could be fetched from GPU-specific APIs for more accuracy
            return (1200f, 7000f); // Default values for most mid-range GPUs
        }

        private static float GetRamSpeed()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Speed FROM Win32_PhysicalMemory");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return Convert.ToSingle(obj["Speed"]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving RAM speed: {ex.Message}");
            }
            return 0;
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