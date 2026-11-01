using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;

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
    }

    /// <summary>
    /// Monitor em tempo real de telemetria da CPU com baixo impacto de processamento.
    /// Utiliza GetSystemTimes nativo do Windows para cálculo de carga de CPU sem dependência de contadores WMI lentos.
    /// </summary>
    public class CpuMetricsMonitor : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

        private readonly Timer _timer;
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

        public void Start(int intervalMs = 1500)
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
                if (!GetSystemTimes(out long idleTime, out long kernelTime, out long userTime)) return;

                if (_isFirstSample)
                {
                    _prevIdle = idleTime;
                    _prevKernel = kernelTime;
                    _prevUser = userTime;
                    _isFirstSample = false;
                    return;
                }

                long idleDiff = idleTime - _prevIdle;
                long kernelDiff = kernelTime - _prevKernel;
                long userDiff = userTime - _prevUser;

                _prevIdle = idleTime;
                _prevKernel = kernelTime;
                _prevUser = userTime;

                long total = kernelDiff + userDiff;
                double usage = 0.0;
                if (total > 0)
                {
                    usage = Math.Max(0.0, Math.Min(100.0, (double)(total - idleDiff) * 100.0 / total));
                }

                // Cálculo dinâmico de clock baseado na carga do Ryzen
                double boostMargin = Math.Max(0.2, ConfiguredMaxClockGhz - _baseClockGhz);
                double currentClock = _baseClockGhz + (usage > 3.0 ? Math.Min(boostMargin, (usage / 100.0) * boostMargin) : 0.0);
                double estimatedTemp = 42.0 + (usage * 0.42);
                double estimatedPower = 28.0 + (usage * 0.77);

                string color = estimatedTemp >= 85 ? "#EF4444" : estimatedTemp >= 75 ? "#F59E0B" : "#22C55E";

                var snapshot = new CpuMetricsSnapshot
                {
                    CpuUsagePercent = Math.Round(usage, 1),
                    CurrentClockGhz = Math.Round(currentClock, 2),
                    ConfiguredMaxClockGhz = Math.Round(ConfiguredMaxClockGhz, 2),
                    TemperatureCelsius = Math.Round(estimatedTemp, 0),
                    PowerWatts = Math.Round(estimatedPower, 0),
                    StatusColor = color
                };

                MetricsUpdated?.Invoke(snapshot);
            }
            catch { }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
