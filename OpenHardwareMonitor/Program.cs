using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private static readonly object collectionLock = new object();

        static void Main(string[] args)
        {
            try
            {
                TimeSpan dataLifetime = TimeSpan.FromDays(365); // Default to 1 year, changed via --lifetime arg
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "init-db")
                    {
                        DatabaseHelper.InitializeDatabase();
                        Console.WriteLine("Database initialized.");
                        return;
                    }
                    else if (args[i] == "clear-db")
                    {
                        DatabaseHelper.InitializeDatabase();
                        DatabaseHelper.ClearDatabase();
                        Console.WriteLine("Database cleared.");
                        return;
                    }
                    else if (args[i] == "--lifetime" && i + 1 < args.Length)
                    {
                        dataLifetime = ParseLifetimeArgument(args[i + 1]);
                        Console.WriteLine($"Data lifetime set to: {dataLifetime.TotalDays} days");
                        System.Threading.Thread.Sleep(5000);
                        i++; // Skip the next argument since we've processed it
                    }
                    else if (args[i] == "prune-now")
                    {
                        // Allow override: "./OpenHardwareMonitor.exe prune-now --lifetime 30d"
                        if (i + 1 < args.Length && args[i + 1] == "--lifetime" && i + 2 < args.Length)
                        {
                            dataLifetime = ParseLifetimeArgument(args[i + 2]);
                            i += 2; // Skip next two args
                        }

                        DatabaseHelper.InitializeDatabase();
                        DatabaseHelper.PruneOldData(dataLifetime);
                        Console.WriteLine($"Pruned data older than {dataLifetime.TotalDays} days.");
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

                // Create and configure sensor collection timer
                Timer timer = new Timer(30000);
                timer.Elapsed += (sender, ElapsedEventArgs) =>
                {
                    lock (collectionLock)
                    {
                        try
                        {
                            OnTimedEvent(sender, ElapsedEventArgs, dataLifetime);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Timer event failed: {ex.Message}");
                        }
                    }
                };
                timer.AutoReset = true;

                // Create and configure pruning timer (runs once per day)
                Timer pruningTimer = new Timer(TimeSpan.FromDays(1).TotalMilliseconds);
                pruningTimer.Elapsed += (sender, e) =>
                {
                    try
                    {
                        Console.WriteLine($"\nPruning data older than {dataLifetime.TotalDays} days...");
                        DatabaseHelper.PruneOldData(dataLifetime);
                        Console.WriteLine("Data pruning completed.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during pruning: {ex.Message}");
                    }
                };
                pruningTimer.AutoReset = true;

                // Initial synchronous collection
                lock (collectionLock)
                {
                    OnTimedEvent(null, null, dataLifetime);
                }

                // Start both timers after first collection completes
                timer.Start();
                pruningTimer.Start();

                Console.WriteLine("Press Enter to exit the program.");
                Console.ReadLine();

                pruningTimer.Stop();
                timer.Stop();
                pruningTimer.Dispose();
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

        private static TimeSpan ParseLifetimeArgument(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg))
                throw new ArgumentException("Lifetime argument cannot be empty");

            char unit = arg[arg.Length - 1]; // Changed from ^1
            string numberPart = arg.Substring(0, arg.Length - 1); // Changed from 0..^1

            if (!int.TryParse(numberPart, out int value))
                throw new ArgumentException("Invalid lifetime format");

            switch (unit)
            {
                case 'd': return TimeSpan.FromDays(value);
                case 'w': return TimeSpan.FromDays(value * 7);
                case 'm': return TimeSpan.FromDays(value * 30);
                case 'y': return TimeSpan.FromDays(value * 365);
                default: throw new ArgumentException("Unknown lifetime unit. Use d, w, m, or y");
            }
        }

        private static void OnTimedEvent(Object source, ElapsedEventArgs e, TimeSpan dataLifetime)
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
                        if (!sensor.Value.HasValue)
                            continue;

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
                            dataLifetime
                        );

                        Console.WriteLine($"Component Statistic: SerialNumber={serialNumber}, Timestamp={timestamp}, MachineState=Active, Temperature={temperature}, load={load}, PowerConsumption={powerConsumption}, CoreSpeed={currentCoreClock}, MemorySpeed={currentMemoryClock}, TotalRAM={GetTotalRAM()}, EndOfLife={timestamp.Add(dataLifetime)}");
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
                foreach (Process process in processes)
                {
                    if (process.Id != 0)
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
                                    dataLifetime
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during data collection: {ex.Message}");
            }
        }

        public static string GetHardwareSerialNumber(IHardware hardware)
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

        public static string GenerateDeterministicSerialNumber(IHardware hardware)
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

        public static Func<string, string, string> GetWmiSerialNumber { get; set; } = DefaultGetWmiSerialNumber;
        private static string DefaultGetWmiSerialNumber(string wmiClass, string propertyName)
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

        public static string GetWMISerialNumber(string wmiClass, string propertyName)
        {
            return GetWmiSerialNumber(wmiClass, propertyName);
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

        public static float GetRamSpeed()
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

        public static Func<bool> IsRunningAsAdministrator = () =>
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        };

        public static Action RelaunchAsAdministrator = () =>
        {
            Console.WriteLine("Program requires administrator privileges. Relaunching...");
            System.Threading.Thread.Sleep(2000);
            ProcessStartInfo procInfo = new ProcessStartInfo();
            procInfo.UseShellExecute = true;
            procInfo.FileName = Process.GetCurrentProcess().MainModule.FileName;
            procInfo.Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1)); // preserve arguments
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
        };
    }
}