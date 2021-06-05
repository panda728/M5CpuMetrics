using System;
using System.Configuration;
using System.Diagnostics;
using System.IO.Ports;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace M5CpuMetrics {
    class Program {
        public const string NICName = "Intel(R) Wi-Fi 6 AX201 160MHz";
        static readonly PerformanceCounter cpu =
            CreatePC("Processor", "% Processor Time", "_Total");
        static readonly PerformanceCounter temp =
            CreatePC("Thermal Zone Information", "Temperature", "\\_TZ.THM0");
        static readonly PerformanceCounter gpu =
            CreatePC("GPU Engine", "Utilization Percentage", "");
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
                var setting = LoadSetting();
                if (args.Length == 2) {
                    setting.COM = args[0];
                    int.TryParse(args[1], out var rate);
                    setting.Rate = rate;
                }

                var core = new MetricsCore(setting);
                while (true) {
                    var result = await core.SendValueAsync();
                    Console.WriteLine(result);
                    await Task.Delay(900);
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
                Console.WriteLine("press enter!");
                Console.ReadLine();
            }
        }

        private static Setting LoadSetting() {
            var setting = new Setting() {
                Meter1 = "CPU",
                Meter1Hue = 32,
                Meter1Color = "Orange",
                Meter2 = "MEM",
                Meter2Hue = 224,
                Meter2Color = "Magenta",
                Meter3 = "NET",
                Meter3Hue = 160,
                Meter3Color = "Cyan",
                Meter4 = "HDD",
                Meter4Hue = 224,
                Meter4Color = "Yellow",
                COM = "COM4",
                Rate = 115200,
            };
            foreach (string key in ConfigurationManager.AppSettings.AllKeys) {
                switch (key) {
                    case "Meter1":
                        setting.Meter1 = ConfigurationManager.AppSettings[key];
                        break;
                    case "Meter1Hue":
                        int.TryParse(ConfigurationManager.AppSettings[key], out var hue1);
                        setting.Meter1Hue = hue1;
                        break;
                    case "Meter1Color":
                        setting.Meter1Color = ConfigurationManager.AppSettings[key];
                        break;
                    case "Meter2":
                        setting.Meter2 = ConfigurationManager.AppSettings[key];
                        break;
                    case "Meter2Hue":
                        int.TryParse(ConfigurationManager.AppSettings[key], out var hue2);
                        setting.Meter2Hue = hue2;
                        break;
                    case "Meter2Color":
                        setting.Meter2Color = ConfigurationManager.AppSettings[key];
                        break;
                    case "Meter3":
                        setting.Meter3 = ConfigurationManager.AppSettings[key];
                        break;
                    case "Meter3Hue":
                        int.TryParse(ConfigurationManager.AppSettings[key], out var hue3);
                        setting.Meter3Hue = hue3;
                        break;
                    case "Meter3Color":
                        setting.Meter3Color = ConfigurationManager.AppSettings[key];
                        break;
                    case "Meter4":
                        setting.Meter4 = ConfigurationManager.AppSettings[key];
                        break;
                    case "Meter4Hue":
                        int.TryParse(ConfigurationManager.AppSettings[key], out var hue4);
                        setting.Meter4Hue = hue4;
                        break;
                    case "Meter4Color":
                        setting.Meter4Color = ConfigurationManager.AppSettings[key];
                        break;
                    case "COM":
                        setting.COM = ConfigurationManager.AppSettings[key];
                        break;
                    case "RATE":
                        int.TryParse(ConfigurationManager.AppSettings[key], out var rate);
                        setting.Rate = rate;
                        break;
                }
            }


            return setting;
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
            var tempC = temp.NextValue() - 273.15;
            var gpuUsage = gpu.NextValue() - 273.15;

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
