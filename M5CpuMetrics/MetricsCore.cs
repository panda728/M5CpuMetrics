using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Text.Json;
using System.Drawing;

namespace M5CpuMetrics {
    public class MetricsCore : IDisposable {
        private readonly float _totalMemory;
        private readonly (string type, int hue, Color fontColor, PerformanceCounter pc) _meter1;
        private readonly (string type, int hue, Color fontColor, PerformanceCounter pc) _meter2;
        private readonly (string type, int hue, Color fontColor, PerformanceCounter pc) _meter3;
        private readonly (string type, int hue, Color fontColor, PerformanceCounter pc) _meter4;
        private readonly SerialPort _port;

        private enum HsvHue {
            HUE_RED = 0,
            HUE_ORANGE = 32,
            HUE_YELLOW = 64,
            HUE_GREEN = 96,
            HUE_AQUA = 128,
            HUE_BLUE = 160,
            HUE_PURPLE = 192,
            HUE_PINK = 224
        };

        public MetricsCore(Setting set) {
            //string meter1Type = "CPU", int meter1Hue = 32, string meter1Color = "Orange",
            //string meter2Type = "MEM", int meter2Hue = 224, string meter2Color = "Magenta",
            //string meter3Type = "NET", int meter3Hue = 160, string meter3Color = "Cyan",
            //string meter4Type = "HDD", int meter4Hue = 64, string meter4Color = "Yellow") {

            _port = new SerialPort(set.COM, set.Rate);

            _meter1 = (set.Meter1, set.Meter1Hue, Color.FromName(set.Meter1Color), CreatePerCounter(set.Meter1));
            _meter2 = (set.Meter2, set.Meter2Hue, Color.FromName(set.Meter2Color), CreatePerCounter(set.Meter2));
            _meter3 = (set.Meter3, set.Meter3Hue, Color.FromName(set.Meter3Color), CreatePerCounter(set.Meter3));
            _meter4 = (set.Meter4, set.Meter4Hue, Color.FromName(set.Meter4Color), CreatePerCounter(set.Meter4));

            _totalMemory = GetBtoGB(new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory);
        }

        public void Dispose() {
            _port?.Dispose();
            GC.SuppressFinalize(this);
        }

        private float GetBtoGB(float value) => (float)(Math.Floor((float)value / 1024 / 1024 / 1024 * 10) / 10);

        private float GetMBtoGB(float value) => (float)(Math.Floor((float)value / 1024 * 10) / 10);

        private PerformanceCounter CreatePerCounter(string meterType) {
            switch (meterType) {
                case "CPU":
                    return NewPerCounter("Processor", "% Processor Time", "_Total");
                case "MEM":
                    return NewPerCounter("Memory", "Available MBytes", "");
                case "NET":
                    var nicName = GetNICName();
                    if (string.IsNullOrEmpty(nicName))
                        return null;
                    return NewPerCounter("Network Interface", "Bytes Total/sec", nicName.Replace("(", "[").Replace(")", "]"));
                case "HDD":
                    return NewPerCounter("PhysicalDisk", "% Idle Time", "0 C:");
                case "TEMP":
                    return NewPerCounter("Thermal Zone Information", "Temperature", "\\_TZ.THM0");
                default:
                    return null;
            };
        }

        private PerformanceCounter NewPerCounter(string categoryName, string counterName, string instanceName) {
            string machineName = ".";
            if (!PerformanceCounterCategory.Exists(categoryName, machineName))
                throw new ApplicationException($"cannot found category! :{categoryName}");

            if (!PerformanceCounterCategory.CounterExists(counterName, categoryName, machineName))
                throw new ApplicationException($"cannot found category counter! :{counterName}");

            return new PerformanceCounter(categoryName, counterName, instanceName, machineName);
        }

        private static string GetNICName() {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel) {
                    return ni.Description;
                }
            }
            return null;
        }

        public async Task<string> SendValueAsync() {
            //var m1Data = GetMetricsData("1", GetMetrics(_meter1.type, _meter1.pc, _meter1.hue, _meter1.fontColor));
            //var m2Data = GetMetricsData("2", GetMetrics(_meter2.type, _meter2.pc, _meter2.hue, _meter2.fontColor));
            //var m3Data = GetMetricsData("3", GetMetrics(_meter3.type, _meter3.pc, _meter3.hue, _meter3.fontColor));
            //var m4Data = GetMetricsData("4", GetMetrics(_meter4.type, _meter4.pc, _meter4.hue, _meter4.fontColor));
            var m1Data = GetMetrics(_meter1.type, _meter1.pc, _meter1.hue, _meter1.fontColor);
            var m2Data = GetMetrics(_meter2.type, _meter2.pc, _meter2.hue, _meter2.fontColor);
            var m3Data = GetMetrics(_meter3.type, _meter3.pc, _meter3.hue, _meter3.fontColor);
            var m4Data = GetMetrics(_meter4.type, _meter4.pc, _meter4.hue, _meter4.fontColor);

            var data = $"{m1Data.Val:000}{m2Data.Val:000}{m3Data.Val:000}{m3Data.Unt}{m4Data.Val:000};";

            if (!_port.IsOpen)
                _port.Open();
            _port.Write(data);
            //Send(data);
            Debug.WriteLine(data);
            //Send($"{m1Data};");
            //await Task.Delay(300);

            //Send($"{m2Data};");
            //await Task.Delay(300);

            //Send($"{m3Data};");
            //await Task.Delay(300);

            //Send($"{m4Data};");
            //await Task.Delay(300);

            return data;

            //var ms = new Metrics[] {
            //    _meter1.pc != null ? GetMetrics(_meter1.type, _meter1.pc, _meter1.hue, _meter1.fontColor) : new Metrics(),
            //    _meter2.pc != null ? GetMetrics(_meter2.type, _meter2.pc, _meter2.hue, _meter2.fontColor) : new Metrics(),
            //    _meter3.pc != null ? GetMetrics(_meter3.type, _meter3.pc, _meter3.hue, _meter3.fontColor) : new Metrics(),
            //    _meter4.pc != null ? GetMetrics(_meter4.type, _meter4.pc, _meter4.hue, _meter4.fontColor) : new Metrics(),
            //};

            //var json = JsonSerializer.Serialize(ms);
            //try {
            //    _port.Open();
            //    _port.WriteLine(json);
            //    _port.Write(";");
            //    Debug.WriteLine(json);
            //    _port.Close();
            //} catch (Exception ex) {
            //    return ex.Message;
            //}
            //return json;
        }

        private string Send(string data) {
            try {
                _port.Open();
                _port.Write(data);
                _port.Write(new byte[] { 0 }, 0, 1);
                _port.Close();
                Debug.WriteLine(data);
            } catch (Exception ex) {
                return ex.Message;
            }
            return data;
        }

        private string GetMetricsData(string id, Metrics m) {
            if (m == null)
                return "1000000 ";
            return $"{id}{m.Val:000}{m.Per * 100:000}{m.Unt}";
        }

        private Metrics GetMetrics(string meterType, PerformanceCounter pc, int hue, Color color) {
            if (pc == null)
                return null;

            switch (meterType) {
                case "CPU":
                    return CreateMetrics(meterType, pc.NextValue(), hue, color);
                case "MEM":
                    return CreateMemoryMetrics(meterType, pc.NextValue(), hue, color);
                case "NET":
                    return CreateNetMetrics(meterType, pc.NextValue(), hue, color);
                case "HDD":
                    return CreateMetrics(meterType, 100 - pc.NextValue(), hue, color);
                case "TEMP":
                    return CreateCpuTempMetrics(meterType, pc.NextValue(), hue, color);
                default:
                    return new Metrics();
            };
        }

        private Metrics CreateMetrics(string meterType, float value, int hue, Color color) {
            var per = (float)Math.Round(value / 100, 3);
            return new Metrics() { Tit = meterType, Val = (int)value, Unt = "%", Per = per, Hue = hue, Clr = Math.Abs(color.ToArgb()) };
        }

        private Metrics CreateMemoryMetrics(string meterType, float value, int hue, Color color) {
            var freeMem = GetMBtoGB(value);
            var usedMemPer = (float)Math.Round(((_totalMemory - freeMem) / _totalMemory), 3);
            return new Metrics() { Tit = meterType, Val = (int)(usedMemPer * 100), Unt = "G", Per = usedMemPer, Hue = hue, Clr = Math.Abs(color.ToArgb()) };
        }

        private Metrics CreateNetMetrics(string meterType, float value, int hue, Color color) {
            var (netUsage, unit) = GetSizeSuffix(value, 0);
            var netunit = string.IsNullOrEmpty(unit) ? " " : unit;
            var netPer = (float)Math.Round(netUsage / 1000, 3);
            return new Metrics() { Tit = meterType, Val = (int)netUsage, Unt = netunit, Per = netPer, Hue = hue, Clr = Math.Abs(color.ToArgb()) };
        }

        private Metrics CreateCpuTempMetrics(string meterType, float value, int hue, Color color) {
            var c = value - 273.15;
            var per = (float)Math.Round(c / 70 * 100, 0);
            return new Metrics() { Tit = meterType, Val = (int)c, Unt = " ", Per = per, Hue = hue, Clr = Math.Abs(color.ToArgb()) };
        }

        private readonly string[] SizeSuffixes = { "", "K", "M", "G", "T", "P", "E", "Z", "Y" };
        private (decimal value, string unit) GetSizeSuffix(float value, int decimalPlaces = 1) {
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

        private class Metrics {
            public string Tit { get; set; } = "";
            public int Val { get; set; } = 0;
            public string Unt { get; set; } = "";
            public float Per { get; set; } = 0;
            public int Hue { get; set; } = 0;
            public int Clr { get; set; } = 0;
        }
    }
}
