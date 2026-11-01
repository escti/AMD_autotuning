using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AMDAutoTuning.Models
{
    public enum CpuArchitecture
    {
        Unknown,
        Zen3_Ryzen5000,
        Zen4_Ryzen7000,
        Zen5_Ryzen9000
    }

    public class CpuCoreInfo : INotifyPropertyChanged
    {
        private int _curveOptimizerOffset = -20;

        public int CoreIndex { get; set; }
        public int LogicalThread1 { get; set; }
        public int LogicalThread2 { get; set; }
        public int MaxPerformancePercent { get; set; }
        public bool IsGold { get; set; }
        public bool IsSilver { get; set; }

        public string CoreLabel => $"Core {CoreIndex}" + (IsGold ? " ⭐ (Gold)" : IsSilver ? " 🥈 (Silver)" : "");

        public string BadgeText => IsGold ? "MELHOR NÚCLEO (GOLD)" : IsSilver ? "SEGUNDO MELHOR (SILVER)" : "PADRÃO";

        public string BadgeColor => IsGold ? "#FACC15" : IsSilver ? "#94A3B8" : "#334155";

        public int CurveOptimizerOffset
        {
            get => _curveOptimizerOffset;
            set
            {
                if (_curveOptimizerOffset != value)
                {
                    _curveOptimizerOffset = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OffsetDisplay));
                }
            }
        }

        public string OffsetDisplay => $"{_curveOptimizerOffset} mV";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Detecta dinamicamente a topologia do silício da máquina local a cada boot,
    /// identificando se é Ryzen 5000 ou 7000, classe de TDP e o ranking de silício (Gold/Silver).
    /// </summary>
    public class CpuTopology
    {
        public string ModelName { get; set; } = "AMD Ryzen Processor";
        public CpuArchitecture Architecture { get; set; } = CpuArchitecture.Unknown;
        public bool IsX3D { get; set; }
        public int PhysicalCores { get; set; } = 6;
        public int LogicalThreads { get; set; } = 12;
        public int DefaultTdpWatts { get; set; } = 105;
        public int StockPptWatts { get; set; } = 142;
        public int StockTdcAmps { get; set; } = 110;
        public int StockEdcAmps { get; set; } = 170;
        public int StockTjMaxCelsius { get; set; } = 95;

        public List<CpuCoreInfo> Cores { get; } = new();

        public CpuCoreInfo? GoldCore => Cores.FirstOrDefault(c => c.IsGold);
        public CpuCoreInfo? SilverCore => Cores.FirstOrDefault(c => c.IsSilver);

        public static CpuTopology Detect()
        {
            var topo = new CpuTopology();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    topo.ModelName = obj["Name"]?.ToString()?.Trim() ?? "AMD Ryzen Processor";
                    topo.PhysicalCores = Convert.ToInt32(obj["NumberOfCores"] ?? 6);
                    topo.LogicalThreads = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 12);
                    break;
                }
            }
            catch { }

            // Identificação de Arquitetura e TDP
            string name = topo.ModelName.ToUpperInvariant();
            topo.IsX3D = name.Contains("X3D");

            if (name.Contains("9") && (name.Contains("9600") || name.Contains("9700") || name.Contains("9800") || name.Contains("9900") || name.Contains("9950")))
            {
                topo.Architecture = CpuArchitecture.Zen5_Ryzen9000;
            }
            else if (name.Contains("7") && (name.Contains("7500") || name.Contains("7600") || name.Contains("7700") || name.Contains("7800") || name.Contains("7900") || name.Contains("7950")))
            {
                topo.Architecture = CpuArchitecture.Zen4_Ryzen7000;
            }
            else if (name.Contains("5") && (name.Contains("5500") || name.Contains("5600") || name.Contains("5700") || name.Contains("5800") || name.Contains("5900") || name.Contains("5950")))
            {
                topo.Architecture = CpuArchitecture.Zen3_Ryzen5000;
            }

            // Definição de limites de TDP de fábrica por modelo
            if (topo.Architecture == CpuArchitecture.Zen3_Ryzen5000)
            {
                topo.StockTjMaxCelsius = 90;
                if (name.Contains("5600G") || name.Contains("5700G") || name.Contains("5600") && !name.Contains("5600X") || name.Contains("5700X"))
                {
                    topo.DefaultTdpWatts = 65;
                    topo.StockPptWatts = 76;
                    topo.StockTdcAmps = 60;
                    topo.StockEdcAmps = 90;
                }
                else
                {
                    // 5600X, 5800X, 5800X3D, 5900X, 5950X
                    topo.DefaultTdpWatts = 105;
                    topo.StockPptWatts = 142;
                    topo.StockTdcAmps = 95;
                    topo.StockEdcAmps = 140;
                }
            }
            else // Zen 4 (ou Zen 5)
            {
                if (topo.IsX3D)
                {
                    topo.DefaultTdpWatts = 120;
                    topo.StockPptWatts = 162;
                    topo.StockTdcAmps = 120;
                    topo.StockEdcAmps = 180;
                    topo.StockTjMaxCelsius = 89; // Proteção térmica obrigatória do 3D V-Cache!
                }
                else if (name.Contains("7600X") || name.Contains("7700X"))
                {
                    topo.DefaultTdpWatts = 105;
                    topo.StockPptWatts = 142;
                    topo.StockTdcAmps = 110;
                    topo.StockEdcAmps = 170;
                    topo.StockTjMaxCelsius = 95;
                }
                else if (name.Contains("7900X") || name.Contains("7950X"))
                {
                    topo.DefaultTdpWatts = 170;
                    topo.StockPptWatts = 230;
                    topo.StockTdcAmps = 160;
                    topo.StockEdcAmps = 225;
                    topo.StockTjMaxCelsius = 95;
                }
                else
                {
                    // 7500F, 7600, 7700, 7900 (65W)
                    topo.DefaultTdpWatts = 65;
                    topo.StockPptWatts = 88;
                    topo.StockTdcAmps = 75;
                    topo.StockEdcAmps = 150;
                    topo.StockTjMaxCelsius = 95;
                }
            }

            // Deteccao Dinamica de Nucleos Gold e Silver a partir do Event Log do Windows
            topo.DetectSiliconQualityDynamically();

            return topo;
        }

        private void DetectSiliconQualityDynamically()
        {
            var corePerfMap = new Dictionary<int, int>(); // logical thread -> perf%

            try
            {
                string query = "*[System[Provider[@Name='Microsoft-Windows-Kernel-Processor-Power'] and (EventID=55)]]";
                var logQuery = new EventLogQuery("System", PathType.LogName, query) { ReverseDirection = true };
                using var reader = new EventLogReader(logQuery);

                for (int i = 0; i < LogicalThreads * 2; i++)
                {
                    using var record = reader.ReadEvent();
                    if (record == null) break;

                    string xml = record.ToXml();
                    var matchNum = Regex.Match(xml, @"<Data Name='Number'>(\d+)</Data>");
                    var matchPerf = Regex.Match(xml, @"<Data Name='MaximumPerformancePercent'>(\d+)</Data>");

                    if (matchNum.Success && matchPerf.Success)
                    {
                        int num = int.Parse(matchNum.Groups[1].Value);
                        int perf = int.Parse(matchPerf.Groups[1].Value);
                        if (!corePerfMap.ContainsKey(num))
                        {
                            corePerfMap[num] = perf;
                        }
                    }

                    if (corePerfMap.Count >= LogicalThreads) break;
                }
            }
            catch { }

            // Mapeia para núcleos físicos (cada 2 threads lógicas = 1 núcleo físico)
            int threadsPerCore = Math.Max(1, LogicalThreads / Math.Max(1, PhysicalCores));

            for (int i = 0; i < PhysicalCores; i++)
            {
                int t1 = i * threadsPerCore;
                int t2 = threadsPerCore > 1 ? t1 + 1 : t1;

                int perf = 100;
                if (corePerfMap.TryGetValue(t1, out int p1)) perf = p1;
                else if (corePerfMap.TryGetValue(t2, out int p2)) perf = p2;

                Cores.Add(new CpuCoreInfo
                {
                    CoreIndex = i,
                    LogicalThread1 = t1,
                    LogicalThread2 = t2,
                    MaxPerformancePercent = perf,
                    CurveOptimizerOffset = -20
                });
            }

            // Identifica Gold e Silver (os maiores percentuais)
            if (Cores.Count > 0)
            {
                var ranked = Cores.OrderByDescending(c => c.MaxPerformancePercent).ToList();
                if (ranked.Count > 0) ranked[0].IsGold = true;
                if (ranked.Count > 1) ranked[1].IsSilver = true;
            }
        }
    }
}
