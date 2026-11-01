using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace AMDAutoTuning.Services
{
    public class CpuMetricsSnapshot
    {
        public double CpuUsagePercent { get; set; }
        public double CurrentClockGhz { get; set; } = 4.70;
        public double ConfiguredMaxClockGhz { get; set; } = 5.35;
        public double ClockPercent => ConfiguredMaxClockGhz > 0 ? Math.Min(100.0, (CurrentClockGhz / ConfiguredMaxClockGhz) * 100.0) : 0;
        public double TemperatureCelsius { get; set; } = 45.0;
        public double PowerWatts { get; set; } = 35.0;
        public string StatusColor { get; set; } = "#22C55E";
        public bool IsPhysicalSensor { get; set; } = false;
    }

    /// <summary>
    /// Monitor em tempo real de telemetria da CPU.
    /// Utiliza LibreHardwareMonitorLib para leitura direta dos sensores físicos do processador
    /// (Core Tctl/Tdie, CPU Package Power e Clocks individuais de núcleo).
    /// Possui fallback gracioso para kernel32 se a biblioteca não puder abrir o driver MSR.
    /// </summary>
    public class CpuMetricsMonitor : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

        private readonly Timer _timer;
        private Computer? _computer;
        private IHardware? _cpuHardware;
        private bool _isLhmInitialized;
        private long _prevIdle;
        private long _prevKernel;
        private long _prevUser;
        private bool _isFirstSample = true;
        private double _baseClockGhz = 4.70;

        public double ConfiguredMaxClockGhz { get; set; } = 5.35;

        public event Action<CpuMetricsSnapshot>? MetricsUpdated;

        public CpuMetricsMonitor()
        {
            _timer = new Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
            DetectBaseClock();
            InitializeLhm();
        }

        private void DetectBaseClock()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["MaxClockSpeed"] != null && double.TryParse(obj["MaxClockSpeed"].ToString(), out var mhz))
                    {
                        _baseClockGhz = Math.Round(mhz / 1000.0, 2);
                        ConfiguredMaxClockGhz = _baseClockGhz + 0.65;
                        break;
                    }
                }
            }
            catch { }
        }

        private void InitializeLhm()
        {
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true
                };
                _computer.Open();
                _cpuHardware = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
                _isLhmInitialized = _cpuHardware != null;
                if (_isLhmInitialized)
                {
                    Logger.Info($"LibreHardwareMonitorLib ativado com sucesso: {_cpuHardware?.Name}", "TELEMETRY");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"LibreHardwareMonitorLib não pôde ser ativado: {ex.Message}. Utilizando fallback do sistema.", "TELEMETRY");
                _isLhmInitialized = false;
            }
        }

        public void Start(int intervalMs = 1200)
        {
            _isFirstSample = true;
            _timer.Change(0, intervalMs);
        }

        public void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void OnTimerTick(object? state)
        {
            try
            {
                double temp = 0;
                double power = 0;
                double clock = 0;
                double load = 0;
                bool readSuccess = false;

                if (_isLhmInitialized && _cpuHardware != null)
                {
                    try
                    {
                        _cpuHardware.Update();

                        // 1. TEMPERATURA FÍSICA: Procura Core (Tctl/Tdie) ou CPU Package
                        var tempSensors = _cpuHardware.Sensors.Where(s => s.SensorType == SensorType.Temperature).ToList();
                        var tctl = tempSensors.FirstOrDefault(s => s.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)) ??
                                   tempSensors.FirstOrDefault(s => s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)) ??
                                   tempSensors.FirstOrDefault(s => s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)) ??
                                   tempSensors.FirstOrDefault();

                        if (tctl?.Value != null && tctl.Value > 0)
                        {
                            temp = tctl.Value.Value;
                        }

                        // 2. CONSUMO REAL EM WATTS: CPU Package Power
                        var powerSensors = _cpuHardware.Sensors.Where(s => s.SensorType == SensorType.Power).ToList();
                        var pkgPower = powerSensors.FirstOrDefault(s => s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)) ??
                                       powerSensors.FirstOrDefault();

                        if (pkgPower?.Value != null && pkgPower.Value > 0)
                        {
                            power = pkgPower.Value.Value;
                        }

                        // 3. FREQUÊNCIA REAL DOS NÚCLEOS: maior clock instantâneo dos cores
                        var clockSensors = _cpuHardware.Sensors.Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core")).ToList();
                        if (clockSensors.Count > 0)
                        {
                            var maxCoreClock = clockSensors.Max(s => s.Value ?? 0);
                            if (maxCoreClock > 0)
                            {
                                clock = maxCoreClock / 1000.0;
                            }
                        }

                        // 4. CARGA TOTAL DA CPU
                        var loadSensors = _cpuHardware.Sensors.Where(s => s.SensorType == SensorType.Load).ToList();
                        var cpuTotalLoad = loadSensors.FirstOrDefault(s => s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase)) ??
                                           loadSensors.FirstOrDefault();

                        if (cpuTotalLoad?.Value != null)
                        {
                            load = cpuTotalLoad.Value.Value;
                            readSuccess = true;
                        }
                    }
                    catch { }
                }

                // Fallback gracioso para kernel se o driver de sensores não responder
                if (!readSuccess || temp <= 0)
                {
                    load = ReadKernelUsageFallback();
                    temp = 42.0 + (load * 0.42);
                    power = 28.0 + (load * 0.77);
                    clock = _baseClockGhz + (load > 3.0 ? Math.Min(ConfiguredMaxClockGhz - _baseClockGhz, (load / 100.0) * (ConfiguredMaxClockGhz - _baseClockGhz)) : 0.0);
                }

                if (clock <= 0) clock = _baseClockGhz;

                string color = temp >= 85 ? "#EF4444" : temp >= 75 ? "#F59E0B" : "#22C55E";

                var snapshot = new CpuMetricsSnapshot
                {
                    CpuUsagePercent = Math.Round(load, 1),
                    CurrentClockGhz = Math.Round(clock, 2),
                    ConfiguredMaxClockGhz = Math.Round(ConfiguredMaxClockGhz, 2),
                    TemperatureCelsius = Math.Round(temp, 1),
                    PowerWatts = Math.Round(power, 1),
                    StatusColor = color,
                    IsPhysicalSensor = readSuccess && temp > 0
                };

                MetricsUpdated?.Invoke(snapshot);
            }
            catch { }
        }

        private double ReadKernelUsageFallback()
        {
            if (!GetSystemTimes(out long idleTime, out long kernelTime, out long userTime)) return 0;

            if (_isFirstSample)
            {
                _prevIdle = idleTime;
                _prevKernel = kernelTime;
                _prevUser = userTime;
                _isFirstSample = false;
                return 0;
            }

            long idleDiff = idleTime - _prevIdle;
            long kernelDiff = kernelTime - _prevKernel;
            long userDiff = userTime - _prevUser;

            _prevIdle = idleTime;
            _prevKernel = kernelTime;
            _prevUser = userTime;

            long total = kernelDiff + userDiff;
            if (total <= 0) return 0;
            return Math.Max(0.0, Math.Min(100.0, (double)(total - idleDiff) * 100.0 / total));
        }

        public void Dispose()
        {
            _timer.Dispose();
            try
            {
                _computer?.Close();
            }
            catch { }
        }
    }
}
