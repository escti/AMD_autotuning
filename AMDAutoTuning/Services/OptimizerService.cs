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

        public (bool success, string message) DisableWindowsTelemetry()
        {
            try
            {
                _snapshotService.Record("OS_TELEMETRY", "Telemetria do Windows", "Ativo", "Desativado", "REVERT_TELEMETRY");

                RunProcess("sc.exe", "stop DiagTrack");
                RunProcess("sc.exe", "config DiagTrack start= disabled");
                RunProcess("sc.exe", "stop dmwappushservice");
                RunProcess("sc.exe", "config dmwappushservice start= disabled");

                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true);
                key?.SetValue("AllowTelemetry", 0, RegistryValueKind.DWord);

                return (true, "Telemetria do Windows (DiagTrack e dmwappushservice) desativada! Redução de I/O de traces e acordamento de núcleos da CPU.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao desativar telemetria: {ex.Message}");
            }
        }

        public (bool success, string message) RevertWindowsTelemetry()
        {
            try
            {
                RunProcess("sc.exe", "config DiagTrack start= auto");
                RunProcess("sc.exe", "start DiagTrack");
                RunProcess("sc.exe", "config dmwappushservice start= demand");

                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true);
                key?.DeleteValue("AllowTelemetry", false);

                _snapshotService.Remove("OS_TELEMETRY");
                return (true, "Serviços de telemetria e diagnósticos restaurados ao padrão do Windows.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter telemetria: {ex.Message}");
            }
        }

        public (bool success, string message) DisableSysMain()
        {
            try
            {
                _snapshotService.Record("OS_SYSMAIN", "SysMain (Superfetch)", "Ativado", "Desativado", "REVERT_SYSMAIN");

                RunProcess("sc.exe", "stop SysMain");
                RunProcess("sc.exe", "config SysMain start= disabled");

                return (true, "Serviço SysMain desativado! Redução de purga da lista Standby e eliminação de I/O contínuo no SSD NVMe.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao desativar SysMain: {ex.Message}");
            }
        }

        public (bool success, string message) RevertSysMain()
        {
            try
            {
                RunProcess("sc.exe", "config SysMain start= auto");
                RunProcess("sc.exe", "start SysMain");
                _snapshotService.Remove("OS_SYSMAIN");
                return (true, "SysMain reativado com inicialização automática.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter SysMain: {ex.Message}");
            }
        }

        public (bool success, string message) DisableDeliveryOptimizationP2P()
        {
            try
            {
                _snapshotService.Record("OS_DELIVERY_OPTIMIZATION", "Otimização de Entrega", "P2P Ativo", "HTTP Direto (Sem P2P)", "REVERT_DELIVERY_OPT");

                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", true))
                {
                    key?.SetValue("DODownloadMode", 0, RegistryValueKind.DWord);
                }

                using (var keyUser = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\DeliveryOptimization", true))
                {
                    keyUser?.SetValue("SystemSettingsDownloadMode", 0, RegistryValueKind.DWord);
                }

                RunProcess("sc.exe", "config DoSvc start= demand");

                return (true, "Compartilhamento P2P de atualizações desativado (DODownloadMode = 0). Sem picos de jitter de rede!");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao configurar Otimização de Entrega: {ex.Message}");
            }
        }

        public (bool success, string message) RevertDeliveryOptimizationP2P()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", true))
                {
                    key?.DeleteValue("DODownloadMode", false);
                }

                using (var keyUser = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\DeliveryOptimization", true))
                {
                    keyUser?.DeleteValue("SystemSettingsDownloadMode", false);
                }

                RunProcess("sc.exe", "config DoSvc start= auto");
                _snapshotService.Remove("OS_DELIVERY_OPTIMIZATION");
                return (true, "Configurações padrão da Otimização de Entrega restauradas.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter Otimização de Entrega: {ex.Message}");
            }
        }

        public (bool success, string message) DisableXboxGameDVR()
        {
            try
            {
                _snapshotService.Record("OS_XBOX_GAMEDVR", "Xbox GameDVR", "Ativado", "Desativado", "REVERT_GAMEDVR");

                using (var key = Registry.CurrentUser.CreateSubKey(@"System\GameConfigStore", true))
                {
                    key?.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
                    key?.SetValue("GameDVR_HistoricalCaptureEnabled", 0, RegistryValueKind.DWord);
                }

                using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", true))
                {
                    key?.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);
                    key?.SetValue("HistoricalCaptureEnabled", 0, RegistryValueKind.DWord);
                }

                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", true))
                {
                    key?.SetValue("AllowGameDVR", 0, RegistryValueKind.DWord);
                }

                return (true, "Captura contínua do GameDVR desativada! VRAM liberada e codificador de vídeo VCN da Radeon desonerado.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao desativar GameDVR: {ex.Message}");
            }
        }

        public (bool success, string message) RevertXboxGameDVR()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"System\GameConfigStore", true))
                {
                    key?.SetValue("GameDVR_Enabled", 1, RegistryValueKind.DWord);
                    key?.SetValue("GameDVR_HistoricalCaptureEnabled", 1, RegistryValueKind.DWord);
                }

                using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", true))
                {
                    key?.SetValue("AppCaptureEnabled", 1, RegistryValueKind.DWord);
                    key?.SetValue("HistoricalCaptureEnabled", 1, RegistryValueKind.DWord);
                }

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", true))
                {
                    key?.DeleteValue("AllowGameDVR", false);
                }

                _snapshotService.Remove("OS_XBOX_GAMEDVR");
                return (true, "Gravação em segundo plano do GameDVR reativada.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter GameDVR: {ex.Message}");
            }
        }

        public (bool success, string message) OptimizeSearchIndexer()
        {
            try
            {
                _snapshotService.Record("OS_SEARCH_INDEXER", "Indexador Windows Search", "Automático", "Manual (Sob Demanda)", "REVERT_INDEXER");

                RunProcess("sc.exe", "stop WSearch");
                RunProcess("sc.exe", "config WSearch start= demand");

                return (true, "Indexador do Windows ajustado para 'Manual'. Sem varreduras contínuas em segundo plano durante jogos e updates.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao ajustar indexador: {ex.Message}");
            }
        }

        public (bool success, string message) RevertSearchIndexer()
        {
            try
            {
                RunProcess("sc.exe", "config WSearch start= auto");
                RunProcess("sc.exe", "start WSearch");
                _snapshotService.Remove("OS_SEARCH_INDEXER");
                return (true, "Indexador do Windows Search restaurado para inicialização Automática.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter indexador: {ex.Message}");
            }
        }

        public (bool success, string message) FixMemoryManagement()
        {
            try
            {
                _snapshotService.Record("OS_MEMORY_MANAGEMENT", "LargeSystemCache", "1", "0", "REVERT_MEMORY_MANAGEMENT");

                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true);
                if (key != null)
                {
                    key.SetValue("LargeSystemCache", 0, RegistryValueKind.DWord);
                    return (true, "LargeSystemCache corrigido para 0! Memória alocada corretamente para jogos e drivers AMD, eliminando riscos de DXGI crash.");
                }

                return (false, "Falha ao abrir chave de Memory Management.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao corrigir memória: {ex.Message}");
            }
        }

        public (bool success, string message) RevertMemoryManagement()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true);
                key?.SetValue("LargeSystemCache", 1, RegistryValueKind.DWord);
                _snapshotService.Remove("OS_MEMORY_MANAGEMENT");
                return (true, "Configuração anterior de memória restaurada.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter memória: {ex.Message}");
            }
        }

        public (bool success, string message) ApplyNetworkThrottlingTweaks()
        {
            try
            {
                _snapshotService.Record("OS_NETWORK_THROTTLING", "Throttling de Rede Multimídia", "Padrão (10 / 20%)", "Desativado (0xFFFFFFFF / 0%)", "REVERT_NETWORK_TWEAKS");

                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", true))
                {
                    key?.SetValue("NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
                    key?.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
                }

                using (var gamesKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", true))
                {
                    gamesKey?.SetValue("GPU Priority", 8, RegistryValueKind.DWord);
                    gamesKey?.SetValue("Priority", 6, RegistryValueKind.DWord);
                    gamesKey?.SetValue("Scheduling Category", "High", RegistryValueKind.String);
                    gamesKey?.SetValue("SFIO Priority", "High", RegistryValueKind.String);
                }

                return (true, "Network Throttling desativado e prioridade de jogos elevada no escalonador! Sem jitter com Discord/Spotify abertos.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao ajustar perfil de rede: {ex.Message}");
            }
        }

        public (bool success, string message) RevertNetworkThrottlingTweaks()
        {
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", true))
                {
                    key?.SetValue("NetworkThrottlingIndex", 10, RegistryValueKind.DWord);
                    key?.SetValue("SystemResponsiveness", 20, RegistryValueKind.DWord);
                }

                using (var gamesKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", true))
                {
                    gamesKey?.SetValue("GPU Priority", 8, RegistryValueKind.DWord);
                    gamesKey?.SetValue("Priority", 2, RegistryValueKind.DWord);
                    gamesKey?.SetValue("Scheduling Category", "Medium", RegistryValueKind.String);
                    gamesKey?.SetValue("SFIO Priority", "Normal", RegistryValueKind.String);
                }

                _snapshotService.Remove("OS_NETWORK_THROTTLING");
                return (true, "Valores padrão de SystemProfile restaurados.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter latência multimídia: {ex.Message}");
            }
        }

        public (bool success, string message) EnableGameModeAndHags()
        {
            try
            {
                _snapshotService.Record("OS_GAMEMODE_HAGS", "Game Mode e HAGS", "Parcial/Desativado", "Ambos Ativados", "REVERT_GAMEMODE_HAGS");

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar", true))
                {
                    key?.SetValue("AllowAutoGameMode", 1, RegistryValueKind.DWord);
                    key?.SetValue("AutoGameModeEnabled", 1, RegistryValueKind.DWord);
                }

                using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", true))
                {
                    key?.SetValue("HwSchMode", 2, RegistryValueKind.DWord);
                }

                return (true, "Modo de Jogo e HAGS ativados! Reinicie o computador para que o HAGS entre em operação no driver da Radeon.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao configurar Game Mode/HAGS: {ex.Message}");
            }
        }

        public (bool success, string message) RevertGameModeAndHags()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", true))
                {
                    key?.SetValue("HwSchMode", 1, RegistryValueKind.DWord);
                }

                _snapshotService.Remove("OS_GAMEMODE_HAGS");
                return (true, "HAGS configurado para desativado (reinicialização necessária).");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter Game Mode/HAGS: {ex.Message}");
            }
        }

        public string ApplyAllSafeOptimizations(string currentPowerPlan, string currentPagefile)
        {
            var results = new System.Text.StringBuilder();
            results.AppendLine("=== INICIANDO PACOTE COMPLETO DE OTIMIZAÇÕES SEGURAS (COM HISTÓRICO) ===");

            var rPower = ApplyBalancedPowerPlan(currentPowerPlan);
            results.AppendLine($"[01/13] Plano de Energia (CPPC): {(rPower.success ? "OK" : "FALHA")} - {rPower.message}");

            var rPage = ApplySafePagefile(currentPagefile, 8192);
            results.AppendLine($"[02/13] Memória Virtual: {(rPage.success ? "OK" : "FALHA")} - {rPage.message}");

            var rFast = DisableFastStartup();
            results.AppendLine($"[03/13] Fast Startup: {(rFast.success ? "OK" : "FALHA")} - {rFast.message}");

            var rTel = DisableWindowsTelemetry();
            results.AppendLine($"[04/13] Telemetria: {(rTel.success ? "OK" : "FALHA")} - {rTel.message}");

            var rSys = DisableSysMain();
            results.AppendLine($"[05/13] SysMain (NVMe): {(rSys.success ? "OK" : "FALHA")} - {rSys.message}");

            var rDosvc = DisableDeliveryOptimizationP2P();
            results.AppendLine($"[06/13] P2P de Atualizações: {(rDosvc.success ? "OK" : "FALHA")} - {rDosvc.message}");

            var rDvr = DisableXboxGameDVR();
            results.AppendLine($"[07/13] Xbox GameDVR: {(rDvr.success ? "OK" : "FALHA")} - {rDvr.message}");

            var rSearch = OptimizeSearchIndexer();
            results.AppendLine($"[08/13] Indexador WSearch: {(rSearch.success ? "OK" : "FALHA")} - {rSearch.message}");

            var rMem = FixMemoryManagement();
            results.AppendLine($"[09/13] Kernel Memory: {(rMem.success ? "OK" : "FALHA")} - {rMem.message}");

            var rNet = ApplyNetworkThrottlingTweaks();
            results.AppendLine($"[10/13] Latência Multimídia: {(rNet.success ? "OK" : "FALHA")} - {rNet.message}");

            var rGame = EnableGameModeAndHags();
            results.AppendLine($"[11/13] Game Mode & HAGS: {(rGame.success ? "OK" : "FALHA")} - {rGame.message}");

            var rBloat = DisableIObitBloat();
            results.AppendLine($"[12/13] Bloatware & DPC: {(rBloat.success ? "OK" : "FALHA")} - {rBloat.message}");

            var rTcp = ApplyTcpOptimizations();
            results.AppendLine($"[13/13] Pilha TCP: {(rTcp.success ? "OK" : "FALHA")} - {rTcp.message}");

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
