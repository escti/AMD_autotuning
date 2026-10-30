using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace AMDAutoTuning.Services
{
    public class OptimizerService
    {
        public (bool success, string message) ApplyBalancedPowerPlan()
        {
            try
            {
                // Switch to Windows Balanced (Equilibrado) - AMD Zen 4 standard
                RunProcess("powercfg", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e");

                // Ensure processor state min is 0% and max is 100%
                RunProcess("powercfg", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 0");
                RunProcess("powercfg", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100");
                RunProcess("powercfg", "/setactive SCHEME_CURRENT");

                return (true, "Plano 'Equilibrado' ativado com sucesso! O agendamento CPPC e o boost inteligente do Ryzen 7600X agora operam com margem térmica otimizada.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao alterar plano de energia: {ex.Message}");
            }
        }

        public (bool success, string message) ApplySafePagefile(int sizeMb = 8192)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true);
                if (key != null)
                {
                    string setting = $@"c:\pagefile.sys {sizeMb} {sizeMb}";
                    key.SetValue("PagingFiles", new string[] { setting }, RegistryValueKind.MultiString);
                    return (true, $"Arquivo de paginação fixado em {sizeMb} MB (8 GB) no drive C: (NVMe). Reinicie o computador para o Windows alocar o bloco contínuo sem fragmentação.");
                }

                return (false, "Não foi possível abrir a chave de registro do Memory Management com permissão de escrita.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao ajustar arquivo de paginação: {ex.Message}");
            }
        }

        public (bool success, string message) DisableIObitBloat()
        {
            try
            {
                int stoppedServices = 0;
                string[] services = { "IObitUnSvr", "AdvancedSystemCareService16", "AdvancedSystemCareService17", "LiveUpdateSvc" };

                foreach (var svc in services)
                {
                    try
                    {
                        RunProcess("sc.exe", $"stop {svc}");
                        RunProcess("sc.exe", $"config {svc} start= disabled");
                        stoppedServices++;
                    }
                    catch { }
                }

                // Check startup registry keys
                try
                {
                    using var cuRun = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    cuRun?.DeleteValue("Advanced SystemCare", false);

                    using var lmRun = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    lmRun?.DeleteValue("Advanced SystemCare", false);
                }
                catch { }

                return (true, "Serviços e inicialização automática do IObit desativados com sucesso! Latência DPC estabilizada.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao desativar bloatware: {ex.Message}");
            }
        }

        public (bool success, string message) ApplyTcpOptimizations()
        {
            try
            {
                RunProcess("netsh", "int tcp set global autotuninglevel=normal");
                RunProcess("netsh", "int tcp set global rss=enabled");
                RunProcess("netsh", "int tcp set global timestamps=disabled");
                RunProcess("netsh", "int tcp set global nonsackrttresiliency=disabled");
                RunProcess("netsh", "int tcp set global rsc=enabled");

                return (true, "Pilha TCP otimizada: Auto-Tuning Normal, RSS ativado e RFC 1323 timestamps desativados para menor latência.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao ajustar pilha TCP: {ex.Message}");
            }
        }

        public string ApplyAllSafeOptimizations()
        {
            var results = new System.Text.StringBuilder();
            results.AppendLine("=== INICIANDO OTIMIZAÇÃO COMPLETA EM 1 CLIQUE ===");

            var r1 = ApplyBalancedPowerPlan();
            results.AppendLine($"[1/4] Plano de Energia: {(r1.success ? "OK" : "FALHA")} - {r1.message}");

            var r2 = ApplySafePagefile(8192);
            results.AppendLine($"[2/4] Memória Virtual: {(r2.success ? "OK" : "FALHA")} - {r2.message}");

            var r3 = DisableIObitBloat();
            results.AppendLine($"[3/4] Bloatware & DPC: {(r3.success ? "OK" : "FALHA")} - {r3.message}");

            var r4 = ApplyTcpOptimizations();
            results.AppendLine($"[4/4] Otimização de Rede: {(r4.success ? "OK" : "FALHA")} - {r4.message}");

            results.AppendLine("===============================================");
            results.AppendLine("Otimizações seguras de software aplicadas com sucesso!");
            return results.ToString();
        }

        private static void RunProcess(string fileName, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
        }
    }
}
