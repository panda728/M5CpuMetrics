using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace M5CpuMetrics {
    class Program {
        public const string NICName = "Intel(R) Wi-Fi 6 AX201 160MHz";
        static readonly PerformanceCounter cpu =
            CreatePC("Processor", "% Processor Time", "_Total");
        static readonly PerformanceCounter mem =
            CreatePC("Memory", "Available MBytes", "");
        static readonly PerformanceCounter net =
            CreatePC("Network Interface", "Bytes Total/sec", GetNICName().Replace("(", "[").Replace(")", "]"));
        static readonly PerformanceCounter hd0 =
            CreatePC("PhysicalDisk", "% Idle Time", "0 C:"); // disk usage (busy) time
        static readonly float totalMemory =
            GetBtoGB(new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory);

        public static async Task Main(string[] args) {
            try {
                var com = "COM1";
                var rate = 115200;
                if (args.Length == 2) {
                    com = args[0];
                    int.TryParse(args[1], out rate);
                }
                using (var port = new SerialPort(com, rate)) {
                    port.Open();
                    if (!port.IsOpen)
                        throw new ApplicationException("Port open failed");

                    while (true) {
                        var data = GetPer();
                        port.Write(data);
                        Console.WriteLine(data);
                        await Task.Delay(900);
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }
        }

        private static string GetPer() {
            var freeMem = GetMBtoGB(mem.NextValue());
            var usedMemPer = (int)Math.Round(((totalMemory - freeMem) / totalMemory) * 100);
            var cupUsage = cpu.NextValue();
            var cpuPer = (int)Math.Round(cupUsage, 0);
            var hddUsage = hd0.NextValue();
            var hddPer = (int)(100 - hddUsage);
            var netUsage = net.NextValue();
            var (value, unit) = GetSizeSuffix(netUsage, 0);
            var netunit = string.IsNullOrEmpty(unit) ? " " : unit;
            return $"{cpuPer:000}{usedMemPer:000}{value:000}{netunit}{hddPer:000};";
        }

        private static PerformanceCounter CreatePC(string categoryName, string counterName, string instanceName) {
            string machineName = ".";
            if (!PerformanceCounterCategory.Exists(categoryName, machineName))
                throw new ApplicationException($"cannot found category! :{categoryName}");

            if (!PerformanceCounterCategory.CounterExists(counterName, categoryName, machineName))
                throw new ApplicationException($"cannot found category counter! :{counterName}");

            return new PerformanceCounter(categoryName, counterName, instanceName, machineName);

        }
        private static float GetMBtoGB(float value) => (float)(Math.Floor((float)value / 1024 * 10) / 10);
        private static float GetBtoGB(float value) => (float)(Math.Floor((float)value / 1024 / 1024 / 1024 * 10) / 10);

        static readonly string[] SizeSuffixes = { "", "K", "M", "G", "T", "P", "E", "Z", "Y" };
        private static (decimal value, string unit) GetSizeSuffix(float value, int decimalPlaces = 1) {
            if (value < 0) {
                var s = GetSizeSuffix(value * -1, decimalPlaces);
                return (s.value * -1, s.unit);
            }

            int i = 0;
            decimal dValue = (decimal)Math.Round(value, decimalPlaces);
            while (Math.Round(dValue, decimalPlaces) >= 1000) {
                dValue /= 1024;
                i++;
            }

            return (Math.Round(dValue, decimalPlaces), SizeSuffixes[i]);
        }

        private static string GetNICName() {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel) {
                    return ni.Description;
                }
            }
            return NICName;
        }
    }
}
