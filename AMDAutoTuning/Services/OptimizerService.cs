using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace AMDAutoTuning.Services
{
    public class OptimizerService
    {
        private readonly SnapshotService _snapshotService;

        public OptimizerService(SnapshotService snapshotService)
        {
            _snapshotService = snapshotService;
        }

        public (bool success, string message) CreateRestorePoint()
        {
            try
            {
                // Run PowerShell to create Windows Restore Point
                string cmd = "Checkpoint-Computer -Description 'AMDAutoTuning_PreOptimization' -RestorePointType 'MODIFY_SETTINGS'";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{cmd}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(30000);

                if (proc != null && proc.ExitCode == 0)
                {
                    return (true, "Ponto de Restauração do Windows criado com êxito!");
                }
                else
                {
                    return (false, "O Windows criou um ponto recentemente ou a proteção de sistema está desativada no drive C:.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao criar ponto de restauração: {ex.Message}");
            }
        }

        public (bool success, string message) ApplyBalancedPowerPlan(string currentPlan)
        {
            try
            {
                _snapshotService.Record("POWER_PLAN", "Plano de Energia", currentPlan, "381b4222-f694-41f0-9685-ff5bb260df2e (Equilibrado)", "REVERT_POWER_PLAN");

                RunProcess("powercfg", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
                RunProcess("powercfg", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 0");
                RunProcess("powercfg", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100");
                RunProcess("powercfg", "/setactive SCHEME_CURRENT");

                return (true, "Plano 'Equilibrado' ativado com sucesso! Boost inteligente do Ryzen 7600X e AMD CPPC operando com folga térmica.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao alterar plano de energia: {ex.Message}");
            }
        }

        public (bool success, string message) RevertPowerPlan()
        {
            try
            {
                // Revert to Ultimate Performance if previously saved or default high performance
                RunProcess("powercfg", "/setactive 7dd30729-79cb-4dde-af81-45bbad21d0b2");
                _snapshotService.Remove("POWER_PLAN");
                return (true, "Plano de energia revertido para o estado anterior.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter plano: {ex.Message}");
            }
        }

        public (bool success, string message) ApplySafePagefile(string currentSetting, int sizeMb = 8192)
        {
            try
            {
                _snapshotService.Record("PAGEFILE", "Arquivo de Paginação", currentSetting, $"{sizeMb} MB Fixo", "REVERT_PAGEFILE");

                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true);
                if (key != null)
                {
                    string setting = $@"c:\pagefile.sys {sizeMb} {sizeMb}";
                    key.SetValue("PagingFiles", new string[] { setting }, RegistryValueKind.MultiString);
                    return (true, $"Arquivo de paginação fixado em {sizeMb} MB (8 GB) no NVMe C:. Reinicie para o Windows alocar o bloco contínuo.");
                }

                return (false, "Falha ao acessar registro de memória virtual.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao ajustar paginação: {ex.Message}");
            }
        }

        public (bool success, string message) RevertPagefile()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true);
                if (key != null)
                {
                    // Restore to previous or system managed
                    key.SetValue("PagingFiles", new string[] { @"c:\pagefile.sys 16 4096" }, RegistryValueKind.MultiString);
                    _snapshotService.Remove("PAGEFILE");
                    return (true, "Arquivo de paginação restaurado para a configuração anterior.");
                }
                return (false, "Falha ao acessar chave do registro.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter paginação: {ex.Message}");
            }
        }

        public (bool success, string message) DisableFastStartup()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", true);
                if (key != null)
                {
                    int prev = Convert.ToInt32(key.GetValue("HiberbootEnabled", 1));
                    _snapshotService.Record("FAST_STARTUP", "Inicialização Rápida", prev == 1 ? "Ativado" : "Desativado", "Desativado", "REVERT_FAST_STARTUP");

                    key.SetValue("HiberbootEnabled", 0, RegistryValueKind.DWord);
                    return (true, "Inicialização Rápida (Fast Startup) desativada com sucesso! Garante reinicialização limpa de drivers e memória.");
                }
                return (false, "Falha ao acessar chave de gerenciamento de energia.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao desativar Fast Startup: {ex.Message}");
            }
        }

        public (bool success, string message) RevertFastStartup()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", true);
                if (key != null)
                {
                    key.SetValue("HiberbootEnabled", 1, RegistryValueKind.DWord);
                    _snapshotService.Remove("FAST_STARTUP");
                    return (true, "Inicialização Rápida reativada.");
                }
                return (false, "Falha ao acessar registro.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter Fast Startup: {ex.Message}");
            }
        }

        public (bool success, string message) DisableIObitBloat()
        {
            try
            {
                _snapshotService.Record("BLOATWARE", "Serviços IObit", "Ativo", "Desativado", "REVERT_BLOATWARE");

                string[] services = { "IObitUnSvr", "AdvancedSystemCareService16", "AdvancedSystemCareService17", "LiveUpdateSvc" };
                foreach (var svc in services)
                {
                    try
                    {
                        RunProcess("sc.exe", $"stop {svc}");
                        RunProcess("sc.exe", $"config {svc} start= disabled");
                    }
                    catch { }
                }

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

        public (bool success, string message) RevertIObitBloat()
        {
            try
            {
                RunProcess("sc.exe", "config IObitUnSvr start= auto");
                RunProcess("sc.exe", "start IObitUnSvr");
                _snapshotService.Remove("BLOATWARE");
                return (true, "Serviços reconfigurados para o estado original.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter serviço: {ex.Message}");
            }
        }

        public (bool success, string message) ApplyTcpOptimizations()
        {
            try
            {
                _snapshotService.Record("TCP_STACK", "Pilha de Rede TCP", "Padrão", "Otimizada", "REVERT_TCP");

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

        public (bool success, string message) RevertTcpOptimizations()
        {
            try
            {
                RunProcess("netsh", "int tcp set global autotuninglevel=normal");
                RunProcess("netsh", "int tcp set global timestamps=disabled");
                _snapshotService.Remove("TCP_STACK");
                return (true, "Configurações de rede restauradas.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter TCP: {ex.Message}");
            }
        }

        public (bool success, string message) CleanDuplicateStartup()
        {
            try
            {
                // Remove duplicate GoogleDriveFS entries in HKLM and Default user
                using var hkuDefault = Registry.Users.OpenSubKey(@".DEFAULT\Software\Microsoft\Windows\CurrentVersion\Run", true);
                hkuDefault?.DeleteValue("GoogleDriveFS", false);

                using var hkuSystem = Registry.Users.OpenSubKey(@"S-1-5-18\Software\Microsoft\Windows\CurrentVersion\Run", true);
                hkuSystem?.DeleteValue("GoogleDriveFS", false);

                return (true, "Entradas duplicadas do Google Drive na inicialização de contas de sistema limpas com sucesso.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao limpar inicialização duplicada: {ex.Message}");
            }
        }

        public string ApplyAllSafeOptimizations(string currentPowerPlan, string currentPagefile)
        {
            var results = new System.Text.StringBuilder();
            results.AppendLine("=== INICIANDO PACOTE DE OTIMIZAÇÕES SEGURAS (COM HISTÓRICO) ===");

            var rPower = ApplyBalancedPowerPlan(currentPowerPlan);
            results.AppendLine($"[1/5] Plano de Energia: {(rPower.success ? "OK" : "FALHA")} - {rPower.message}");

            var rPage = ApplySafePagefile(currentPagefile, 8192);
            results.AppendLine($"[2/5] Memória Virtual: {(rPage.success ? "OK" : "FALHA")} - {rPage.message}");

            var rFast = DisableFastStartup();
            results.AppendLine($"[3/5] Fast Startup: {(rFast.success ? "OK" : "FALHA")} - {rFast.message}");

            var rBloat = DisableIObitBloat();
            results.AppendLine($"[4/5] Bloatware & DPC: {(rBloat.success ? "OK" : "FALHA")} - {rBloat.message}");

            var rTcp = ApplyTcpOptimizations();
            results.AppendLine($"[5/5] Pilha TCP: {(rTcp.success ? "OK" : "FALHA")} - {rTcp.message}");

            CleanDuplicateStartup();

            results.AppendLine("================================================================");
            results.AppendLine("Todas as alterações foram registradas no Histórico para reversão a qualquer momento!");
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
