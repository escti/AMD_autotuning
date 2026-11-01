using System;
using System.IO;
using System.Text;
using AMDAutoTuning.Models;

namespace AMDAutoTuning.Services
{
    /// <summary>
    /// Orquestra a geração de roteiros de BIOS, perfis exportáveis e aplicação de
    /// políticas de agendamento de CPPC e EPP no sistema operacional.
    /// </summary>
    public class CpuTunerService
    {
        public static string GenerateBiosGuide(CpuTopology topo, string motherboard, int ppt, int tdc, int edc, int tjMax, int boostMhz, bool isPerCore, int allCoreOffset, int[] perCoreOffsets)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==========================================================================");
            sb.AppendLine($"ROTEIRO DE CONFIGURAÇÃO DE BIOS — {motherboard.ToUpperInvariant()}");
            sb.AppendLine($"PROCESSADOR: {topo.ModelName} ({topo.PhysicalCores}C / {topo.LogicalThreads}T)");
            sb.AppendLine("==========================================================================");
            sb.AppendLine();
            sb.AppendLine("1. Reinicie o computador e pressione [DEL] (ou [F2]) repetidamente para entrar na BIOS.");

            string mbUpper = motherboard.ToUpperInvariant();
            if (mbUpper.Contains("MSI"))
            {
                sb.AppendLine("2. Pressione [F7] para alternar para o modo 'Advanced' (Avançado).");
                sb.AppendLine("3. Clique no menu [OC] (Overclocking) no canto superior esquerdo.");
                sb.AppendLine("4. Role até [Advanced CPU Configuration] -> [AMD Overclocking] -> [Precision Boost Overdrive].");
            }
            else if (mbUpper.Contains("ASUS"))
            {
                sb.AppendLine("2. Pressione [F7] para entrar no modo 'Advanced'.");
                sb.AppendLine("3. Acesse a aba [Ai Tweaker] (ou [Extreme Tweaker]).");
                sb.AppendLine("4. Entre em [Precision Boost Overdrive].");
            }
            else if (mbUpper.Contains("GIGABYTE"))
            {
                sb.AppendLine("2. Pressione [F2] para entrar no modo 'Advanced'.");
                sb.AppendLine("3. Acesse a aba [Tweaker] -> [Advanced CPU Settings] -> [AMD Overclocking].");
                sb.AppendLine("4. Entre em [Precision Boost Overdrive].");
            }
            else
            {
                sb.AppendLine("2. Acesse a aba de Overclocking / Advanced -> [AMD Overclocking] -> [Precision Boost Overdrive].");
            }

            sb.AppendLine();
            sb.AppendLine("5. Configure os parâmetros exatamente como calculado abaixo:");
            sb.AppendLine("   • Precision Boost Overdrive: [Advanced] (ou [Manual])");
            sb.AppendLine("   • PBO Limits: [Manual]");
            sb.AppendLine($"   • PPT Limit: [{ppt} W] (ou {ppt * 1000} mW)");
            sb.AppendLine($"   • TDC Limit: [{tdc} A] (ou {tdc * 1000} mA)");
            sb.AppendLine($"   • EDC Limit: [{edc} A] (ou {edc * 1000} mA)");
            sb.AppendLine($"   • Platform Thermal Throttle Limit (TjMax): [{tjMax}°C]");

            if (boostMhz > 0)
            {
                sb.AppendLine($"   • CPU Boost Clock Override: [Enabled]");
                sb.AppendLine($"   • Max CPU Boost Clock Override: [+{boostMhz}] MHz");
            }
            else
            {
                sb.AppendLine("   • CPU Boost Clock Override: [Disabled] (0 MHz)");
            }

            sb.AppendLine();
            sb.AppendLine("6. Ajuste a Curva de Tensão (Curve Optimizer):");
            sb.AppendLine("   • Curve Optimizer: " + (isPerCore ? "[Per Core]" : "[All Cores]"));
            sb.AppendLine("   • Curve Optimizer Sign: [Negative] (-)");

            if (isPerCore)
            {
                for (int i = 0; i < topo.Cores.Count; i++)
                {
                    var core = topo.Cores[i];
                    int offset = (i < perCoreOffsets.Length) ? perCoreOffsets[i] : allCoreOffset;
                    string note = core.IsGold ? " (⭐ Núcleo Gold - Clocks mais altos)" : core.IsSilver ? " (🥈 Núcleo Silver)" : "";
                    sb.AppendLine($"     - Core {core.CoreIndex}{note}: Magnitude [{Math.Abs(offset)}]");
                }
            }
            else
            {
                sb.AppendLine($"   • All Core Curve Optimizer Magnitude: [{Math.Abs(allCoreOffset)}]");
            }

            sb.AppendLine();
            sb.AppendLine("7. Pressione [F10] para salvar as alterações e reiniciar.");
            sb.AppendLine("8. Ao retornar ao Windows, abra o AMD AutoTuning e execute o Teste de Estresse para validar.");

            return sb.ToString();
        }

        public static (bool success, string message) ApplyWindowsEppTuning(bool isDryRun = false)
        {
            if (isDryRun)
            {
                Logger.Info("[DRY-RUN] Simulação: ajuste de EPP para 33 (Equilibrado Zen 4).", "CPU_TUNER");
                return (true, "[SIMULAÇÃO] Políticas de energia do Ryzen (EPP 33) simuladas com sucesso.");
            }

            try
            {
                // Ajusta o EPP (Energy Performance Preference) para 33% no plano ativo
                // 33 permite ao Zen 3 e Zen 4 dormir núcleos inativos em CC6 sem latência de despertar
                var psi = new System.Diagnostics.ProcessStartInfo("powercfg", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(psi)?.WaitForExit();

                // 36687f9e-e3a5-4dbf-b1dc-15eb381c6863 é o GUID de EPP no Windows
                var psiEpp = new System.Diagnostics.ProcessStartInfo(
                    "powercfg",
                    "/setacvalueindex SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 36687f9e-e3a5-4dbf-b1dc-15eb381c6863 33")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(psiEpp)?.WaitForExit();

                var psiApply = new System.Diagnostics.ProcessStartInfo("powercfg", "/setactive SCHEME_CURRENT")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(psiApply)?.WaitForExit();

                Logger.Info("Plano Equilibrado e EPP configurados para 33 (AMD CPPC otimizado).", "CPU_TUNER");
                return (true, "Plano de energia e política EPP ajustados para 33! Gerenciamento de núcleos Zen ativo.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao aplicar EPP no Windows", ex, "CPU_TUNER");
                return (false, $"Erro ao configurar EPP: {ex.Message}");
            }
        }
    }
}
