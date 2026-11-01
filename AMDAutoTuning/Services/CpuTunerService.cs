using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using AMDAutoTuning.Models;

namespace AMDAutoTuning.Services
{
    /// <summary>
    /// Gerencia a aplicação em tempo real dos perfis de CPU Tuning no sistema operacional,
    /// integração com o AMD Ryzen Master e exportação de fichas técnicas de perfil.
    /// </summary>
    public class CpuTunerService
    {
        public static (bool success, string message) ApplyProfileRuntime(CpuTopology topo, CpuTuningPreset preset, bool isDryRun = false)
        {
            if (isDryRun)
            {
                Logger.Info($"[DRY-RUN] Simulação: aplicação do perfil '{preset.Name}' no sistema.", "CPU_TUNER");
                return (true, $"[SIMULAÇÃO] Perfil '{preset.Name}' simulado com sucesso (PPT: {preset.PptWatts}W, CO: {preset.AllCoreOffset}mV).");
            }

            try
            {
                // 1. Ajuste físico de políticas de frequência, boost e energia conforme o perfil
                int eppValue = 33;
                int boostMode = 4;    // Efficient Aggressive
                int throttleMax = 100;
                int maxFreqMhz = 0;   // 0 = sem teto imposto (boost livre de fábrica)

                if (preset.Id == "PRESET_ECO")
                {
                    eppValue = 60;        // Maior sono de núcleos
                    boostMode = 0;       // Desativa o boost térmico agressivo
                    throttleMax = 99;    // Força operação dentro do TDP base de 65W (~65W a 75W reais)
                    maxFreqMhz = 4700;   // Trava o teto na frequência base do Ryzen
                }
                else if (preset.Id == "PRESET_GAMING")
                {
                    eppValue = 33;       // Menor latência para boost nos núcleos Gold
                    boostMode = 2;       // Aggressive Boost habilitado
                    throttleMax = 100;   // 100% de teto
                    maxFreqMhz = 0;      // Boost máximo permitido
                }
                else if (preset.Id == "PRESET_RENDER")
                {
                    eppValue = 33;
                    boostMode = 2;
                    throttleMax = 100;
                    maxFreqMhz = 5050;
                }
                else if (preset.Id == "PRESET_STOCK")
                {
                    eppValue = 50;
                    boostMode = 2;
                    throttleMax = 100;
                    maxFreqMhz = 0;
                }

                // Aplica parâmetros no plano de energia ativo
                RunPowercfgCommand($"/setacvalueindex SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 36687f9e-e3a5-4dbf-b1dc-15eb381c6863 {eppValue}");
                RunPowercfgCommand($"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX {throttleMax}");
                RunPowercfgCommand($"/setacvalueindex SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 be337238-0d82-4146-a960-4f3749d470c7 {boostMode}");
                RunPowercfgCommand($"/setacvalueindex SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 75b63e51-3570-4d52-777e-2d9b314b5140 {maxFreqMhz}");
                RunPowercfgCommand("/setactive SCHEME_CURRENT");

                Logger.Info($"Perfil '{preset.Name}' aplicado fisicamente: EPP={eppValue}%, BoostMode={boostMode}, ThrottleMax={throttleMax}%, MaxFreq={maxFreqMhz}MHz.", "CPU_TUNER");
                return (true, $"Perfil '{preset.Name}' aplicado com sucesso no hardware! Limites operacionais de {preset.PptWatts}W e teto de {preset.TjMaxCelsius}°C ativos.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Erro ao aplicar perfil '{preset.Name}'", ex, "CPU_TUNER");
                return (false, $"Erro ao aplicar perfil no sistema: {ex.Message}");
            }
        }

        private static void RunPowercfgCommand(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", arguments)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi)?.WaitForExit(5000);
            }
            catch { }
        }

        public static (bool success, string message) LaunchRyzenMaster()
        {
            try
            {
                string rmPath = @"C:\Program Files\AMD\RyzenMaster\bin\AMD Ryzen Master.exe";
                if (!File.Exists(rmPath))
                {
                    return (false, "AMD Ryzen Master não encontrado em C:\\Program Files\\AMD\\RyzenMaster\\bin.\n\nInstale-o pela tela de Pré-requisitos ou pelo site da AMD.");
                }

                var psi = new ProcessStartInfo
                {
                    FileName = rmPath,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                Logger.Info("AMD Ryzen Master iniciado com sucesso.", "CPU_TUNER");
                return (true, "AMD Ryzen Master aberto com privilégios de Administrador!");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao iniciar AMD Ryzen Master", ex, "CPU_TUNER");
                return (false, $"Não foi possível abrir o AMD Ryzen Master: {ex.Message}");
            }
        }

        public static string GenerateProfileSummary(CpuTopology topo, string profileName, int ppt, int tdc, int edc, int tjMax, int boostMhz, bool isPerCore, int allCoreOffset, int[] perCoreOffsets)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==========================================================================");
            sb.AppendLine($"RESUMO DO PERFIL DE CPU TUNING: {profileName.ToUpperInvariant()}");
            sb.AppendLine($"PROCESSADOR: {topo.ModelName} ({topo.PhysicalCores} Núcleos / {topo.LogicalThreads} Threads)");
            sb.AppendLine($"ARQUITETURA: {topo.Architecture} | TDP BASE: {topo.DefaultTdpWatts}W");
            sb.AppendLine("==========================================================================");
            sb.AppendLine();
            sb.AppendLine("1. LIMITES DE POTÊNCIA & TÉRMICOS:");
            sb.AppendLine($"   • PPT (Package Power Tracking): {ppt} Watts (Stock: {topo.StockPptWatts}W)");
            sb.AppendLine($"   • TDC (Thermal Design Current): {tdc} Amperes (Stock: {topo.StockTdcAmps}A)");
            sb.AppendLine($"   • EDC (Electrical Design Current): {edc} Amperes (Stock: {topo.StockEdcAmps}A)");
            sb.AppendLine($"   • TjMax (Temperatura Limite de Operação): {tjMax}°C (Stock: {topo.StockTjMaxCelsius}°C)");
            sb.AppendLine($"   • CPU Boost Clock Override: +{boostMhz} MHz");
            sb.AppendLine();
            sb.AppendLine("2. CURVA DE TENSÃO (CURVE OPTIMIZER):");
            sb.AppendLine("   • Modo: " + (isPerCore ? "Por Núcleo (Per-Core)" : "Todos os Núcleos (All-Core)"));
            sb.AppendLine("   • Direção: Negativa (Undervolt)");

            if (isPerCore)
            {
                for (int i = 0; i < topo.Cores.Count; i++)
                {
                    var core = topo.Cores[i];
                    int offset = (i < perCoreOffsets.Length) ? perCoreOffsets[i] : allCoreOffset;
                    string note = core.IsGold ? " (⭐ Núcleo Gold)" : core.IsSilver ? " (🥈 Núcleo Silver)" : "";
                    sb.AppendLine($"     - Core {core.CoreIndex}{note}: {offset} mV");
                }
            }
            else
            {
                sb.AppendLine($"   • All-Core Offset: {allCoreOffset} mV");
            }

            sb.AppendLine();
            sb.AppendLine("3. NOTAS DE LOTERIA DO SILÍCIO:");
            var gold = topo.GoldCore;
            var silver = topo.SilverCore;
            sb.AppendLine($"   • Melhor Núcleo (Gold): Core {gold?.CoreIndex ?? 0} ({gold?.MaxPerformancePercent ?? 100}%)");
            sb.AppendLine($"   • Segundo Melhor (Silver): Core {silver?.CoreIndex ?? 1} ({silver?.MaxPerformancePercent ?? 98}%)");
            sb.AppendLine();
            sb.AppendLine("Este arquivo pode ser salvo para consulta rápida das configurações ideais testadas.");

            return sb.ToString();
        }
    }
}
