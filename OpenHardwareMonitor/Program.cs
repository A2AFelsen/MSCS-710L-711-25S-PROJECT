﻿using System;
using System.Diagnostics;
using System.Management; // Add reference to System.Management
using System.Security.Principal;
using System.Timers;
using OpenHardwareMonitor.Hardware;

namespace OpenHardwareMonitorExample
{
    class Program
    {
        private static Computer computer;
        private static PerformanceCounter cpuCounter; // Performance counter for overall CPU usage

        static void Main(string[] args)
        {
            // Check if the program is running as administrator
            if (!IsRunningAsAdministrator())
            {
                // Relaunch the program as administrator
                RelaunchAsAdministrator();
                return;
            }

            // Initialize the Computer object
            computer = new Computer
            {
                CPUEnabled = true,
                GPUEnabled = true,
                RAMEnabled = true,
                FanControllerEnabled = true,
                HDDEnabled = true
            };

            // Open the hardware monitoring session
            computer.Open();

            // Initialize the CPU performance counter
            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

            // Set up a timer to run every 30 seconds
            Timer timer = new Timer(30000); // 30,000 milliseconds = 30 seconds
            timer.Elapsed += OnTimedEvent; // Attach the event handler
            timer.AutoReset = true; // Ensure the timer repeats
            timer.Enabled = true; // Start the timer

            Console.WriteLine("Press Enter to exit the program.");
            Console.ReadLine();

            // Clean up
            timer.Stop();
            computer.Close();
            DatabaseHelper.CloseConnection();
        }

        private static void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            Console.WriteLine($"Reading sensor data at {DateTime.Now}");

            float? temperature = null;
            float? power = null;
            float? usage = null;
            float? coreSpeed = null;
            float? memorySpeed = null;

            // Read and display hardware sensor data
            foreach (var hardwareItem in computer.Hardware)
            {
                Console.WriteLine($"Hardware: {hardwareItem.Name}");
                hardwareItem.Update();

                foreach (var sensor in hardwareItem.Sensors)
                {
                    switch (sensor.SensorType)
                    {
                        case SensorType.Temperature:
                            temperature = sensor.Value;
                            Console.WriteLine($"  Temperature Sensor: {sensor.Name}, Value: {sensor.Value} °C");
                            break;
                        case SensorType.Load:
                            usage = sensor.Value;
                            Console.WriteLine($"  Load Sensor: {sensor.Name}, Value: {sensor.Value} %");
                            break;
                        case SensorType.Power:
                            power = sensor.Value;
                            Console.WriteLine($"  Power Sensor: {sensor.Name}, Value: {sensor.Value} W");
                            break;
                        case SensorType.Clock:
                            coreSpeed = sensor.Value;
                            Console.WriteLine($"  Clock Speed Sensor: {sensor.Name}, Value: {sensor.Value} MHz");
                            break;
                        default:
                            Console.WriteLine($"  Sensor: {sensor.Name}, Value: {sensor.Value}");
                            break;
                    }
                }
            }

            // Get and display CPU usage
            float cpuUsage = cpuCounter.NextValue(); // Get the current CPU usage
            System.Threading.Thread.Sleep(100); // Wait for the counter to get a valid value
            cpuUsage = cpuCounter.NextValue(); // Get the updated CPU usage
            Console.WriteLine($"\nCPU Usage: {cpuUsage:F2} %");
            DatabaseHelper.InsertMetrics(temperature, usage, power, coreSpeed, memorySpeed);

            // List all running processes
            Console.WriteLine("\nRunning Processes:");
            Process[] processes = Process.GetProcesses();
            foreach (Process process in processes)
            {
                try
                {
                    Console.WriteLine($"  Process: {process.ProcessName} (ID: {process.Id})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error accessing process {process.ProcessName}: {ex.Message}");
                }
            }

            // Retrieve and display hardware serial numbers using WMI
            Console.WriteLine("\nHardware Serial Numbers:");
            GetHardwareSerialNumbers();

            // Retrieve and display RAM information
            Console.WriteLine("\nRAM Information:");
            GetRAMInformation();

            Console.WriteLine(); // Add a blank line for readability
        }

        private static void GetHardwareSerialNumbers()
        {
            // Get CPU Serial Number
            string cpuSerialNumber = GetWMISerialNumber("Win32_Processor", "ProcessorId");
            Console.WriteLine($"  CPU Serial Number: {cpuSerialNumber}");

            // Get Motherboard Serial Number
            string motherboardSerialNumber = GetWMISerialNumber("Win32_BaseBoard", "SerialNumber");
            Console.WriteLine($"  Motherboard Serial Number: {motherboardSerialNumber}");

            // Get GPU Serial Number (if available)
            string gpuSerialNumber = GetWMISerialNumber("Win32_VideoController", "PNPDeviceID");
            Console.WriteLine($"  GPU Serial Number: {gpuSerialNumber}");
        }

        private static void GetRAMInformation()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    // Total physical memory in kilobytes (KB)
                    ulong totalMemoryKB = Convert.ToUInt64(obj["TotalVisibleMemorySize"]);
                    // Free physical memory in kilobytes (KB)
                    ulong freeMemoryKB = Convert.ToUInt64(obj["FreePhysicalMemory"]);

                    // Convert KB to GB
                    double totalMemoryGB = totalMemoryKB / 1048576.0;
                    double freeMemoryGB = freeMemoryKB / 1048576.0;
                    double usedMemoryGB = totalMemoryGB - freeMemoryGB;

                    Console.WriteLine($"  Total RAM: {totalMemoryGB:F2} GB");
                    Console.WriteLine($"  Used RAM: {usedMemoryGB:F2} GB");
                    Console.WriteLine($"  Available RAM: {freeMemoryGB:F2} GB");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error retrieving RAM information: {ex.Message}");
            }
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
                Console.WriteLine($"  Error retrieving {wmiClass} serial number: {ex.Message}");
            }
            return "Not Available";
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
            procInfo.Verb = "runas"; // This triggers the UAC prompt

            try
            {
                Process.Start(procInfo);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // The user declined the UAC prompt
                Console.WriteLine("You must run this program as an administrator.");
            }

            Environment.Exit(0); // Close the current instance
        }
    }
}