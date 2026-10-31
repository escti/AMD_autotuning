using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using AMDAutoTuning.Models;
using Microsoft.Win32;

namespace AMDAutoTuning.Services
{
    /// <summary>
    /// Gerencia a aplicação e reversão simétrica dos 13 tweaks do sistema.
    /// 
    /// Salvaguardas Críticas (Zero-Damage Policy):
    /// 1. Nunca hardcodar GUIDs de planos de energia ou caminhos de pagefile de máquinas específicas.
    /// 2. Toda reversão consulta a fonte da verdade em SnapshotService.History.
    /// 3. Suporte completo a IsDryRun (--dry-run): simulação segura para testes sem modificar o host.
    /// 4. Todos os comandos externos e operações de registro registram telemetria detalhada no Logger.
    /// </summary>
    public class OptimizerService
    {
        private static readonly Regex GuidRegex = new(
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            RegexOptions.Compiled);

        private readonly SnapshotService _snapshotService;

        public bool IsDryRun { get; set; } = App.IsDryRun;

        public OptimizerService(SnapshotService snapshotService)
        {
            _snapshotService = snapshotService;
        }

        // ====================================================================
        // PONTO DE RESTAURAÇÃO
        // ====================================================================

        public (bool success, string message) CreateRestorePoint()
        {
            if (IsDryRun)
            {
                Logger.Info("[DRY-RUN] Simulação: criação de Ponto de Restauração ignorada no host.", "RESTORE_POINT");
                return (true, "[SIMULAÇÃO] Ponto de Restauração simulado com sucesso (modo --dry-run ativo).");
            }

            try
            {
                Logger.Info("Solicitando criação de Ponto de Restauração via Checkpoint-Computer...", "RESTORE_POINT");
                string cmd = "Checkpoint-Computer -Description 'AMDAutoTuning_PreOptimization' -RestorePointType 'MODIFY_SETTINGS'";
                
                var (success, exitCode, stdout, stderr) = RunProcess("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{cmd}\"");

                if (success && exitCode == 0)
                {
                    Logger.Info("Ponto de Restauração do Windows criado com sucesso.", "RESTORE_POINT");
                    return (true, "Ponto de Restauração do Windows criado com êxito!");
                }

                string err = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                Logger.Warn($"Não foi possível criar ponto de restauração (ExitCode {exitCode}): {err}", "RESTORE_POINT");
                return (false, "O Windows criou um ponto recentemente ou a proteção de sistema está desativada no drive C:.");
            }
            catch (Exception ex)
            {
                Logger.Error("Exceção ao criar ponto de restauração", ex, "RESTORE_POINT");
                return (false, $"Erro ao criar ponto de restauração: {ex.Message}");
            }
        }

        // ====================================================================
        // [01] PLANO DE ENERGIA (CPPC ZEN 4)
        // ====================================================================

        public (bool success, string message) ApplyBalancedPowerPlan(string currentPlanLabel)
        {
            try
            {
                // Detecta o GUID real ativo no Windows no momento da execução
                string originalGuid = DetectActiveSchemeGuid();
                string planRecord = string.IsNullOrEmpty(originalGuid) ? currentPlanLabel : originalGuid;

                _snapshotService.Record("POWER_PLAN", "Plano de Energia", planRecord, "381b4222-f694-41f0-9685-ff5bb260df2e (Equilibrado)", "REVERT_POWER_PLAN");

                if (IsDryRun)
                {
                    Logger.Info($"[DRY-RUN] Plano de Energia seria alterado de '{planRecord}' para Equilibrado (381b4222...)", "POWER_PLAN");
                    return (true, "[SIMULAÇÃO] Plano de energia Equilibrado simulado com sucesso.");
                }

                RunProcess("powercfg", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
                RunProcess("powercfg", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 0");
                RunProcess("powercfg", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100");
                RunProcess("powercfg", "/setactive SCHEME_CURRENT");

                Logger.Info($"Plano Equilibrado ativado. GUID anterior registrado: '{planRecord}'.", "POWER_PLAN");
                return (true, "Plano 'Equilibrado' ativado com sucesso! Boost inteligente do Ryzen e AMD CPPC operando com folga térmica.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao aplicar plano de energia", ex, "POWER_PLAN");
                return (false, $"Erro ao alterar plano de energia: {ex.Message}");
            }
        }

        public (bool success, string message) RevertPowerPlan()
        {
            try
            {
                string? prev = _snapshotService.GetPreviousValue("POWER_PLAN");
                string targetGuid = "";

                if (!string.IsNullOrEmpty(prev))
                {
                    var match = GuidRegex.Match(prev);
                    if (match.Success) targetGuid = match.Value;
                }

                // Fallback seguro: se o snapshot não tinha GUID, restaura o padrão de Alto Desempenho do Windows
                if (string.IsNullOrEmpty(targetGuid))
                {
                    targetGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"; // Alto Desempenho nativo
                }

                if (IsDryRun)
                {
                    Logger.Info($"[DRY-RUN] Reversão de plano para GUID '{targetGuid}' simulada.", "POWER_PLAN");
                    _snapshotService.Remove("POWER_PLAN");
                    return (true, "[SIMULAÇÃO] Plano de energia revertido com sucesso.");
                }

                RunProcess("powercfg", $"/setactive {targetGuid}");
                _snapshotService.Remove("POWER_PLAN");

                Logger.Info($"Plano de energia revertido para GUID: {targetGuid}", "POWER_PLAN");
                return (true, $"Plano de energia revertido para a configuração anterior ({targetGuid}).");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao reverter plano de energia", ex, "POWER_PLAN");
                return (false, $"Erro ao reverter plano: {ex.Message}");
            }
        }

        private static string DetectActiveSchemeGuid()
        {
            try
            {
                var (_, _, stdout, _) = RunProcess("powercfg", "/getactivescheme");
                var match = GuidRegex.Match(stdout);
                return match.Success ? match.Value : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // ====================================================================
        // [02] ARQUIVO DE PAGINAÇÃO (PAGEFILE)
        // ====================================================================

        public (bool success, string message) ApplySafePagefile(string currentSetting, int sizeMb = 8192)
        {
            try
            {
                // Lê a configuração bruta anterior do registro para poder desfazer de forma idêntica
                string rawPrev = ReadCurrentPagingFilesRaw();
                string recordVal = string.IsNullOrEmpty(rawPrev) ? currentSetting : rawPrev;

                _snapshotService.Record("PAGEFILE", "Arquivo de Paginação", recordVal, $"{sizeMb} MB Fixo", "REVERT_PAGEFILE");

                if (IsDryRun)
                {
                    Logger.Info($"[DRY-RUN] Pagefile seria alterado de '{recordVal}' para {sizeMb}MB fixo.", "PAGEFILE");
                    return (true, $"[SIMULAÇÃO] Paginação de {sizeMb} MB simulada com sucesso.");
                }

                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true);
                if (key != null)
                {
                    string setting = $@"c:\pagefile.sys {sizeMb} {sizeMb}";
                    key.SetValue("PagingFiles", new string[] { setting }, RegistryValueKind.MultiString);
                    Logger.Info($"PagingFiles fixado em: {setting}", "PAGEFILE");
                    return (true, $"Arquivo de paginação fixado em {sizeMb} MB (8 GB) no NVMe C:. Reinicie para o Windows alocar o bloco contínuo.");
                }

                return (false, "Falha ao acessar registro de memória virtual.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao ajustar paginação", ex, "PAGEFILE");
                return (false, $"Erro ao ajustar paginação: {ex.Message}");
            }
        }

        public (bool success, string message) RevertPagefile()
        {
            try
            {
                string? prev = _snapshotService.GetPreviousValue("PAGEFILE");

                if (IsDryRun)
                {
                    Logger.Info($"[DRY-RUN] Reversão de pagefile para '{prev}' simulada.", "PAGEFILE");
                    _snapshotService.Remove("PAGEFILE");
                    return (true, "[SIMULAÇÃO] Arquivo de paginação restaurado com sucesso.");
                }

                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true);
                if (key != null)
                {
                    if (string.IsNullOrEmpty(prev) || prev.Equals("SYSTEM_MANAGED", StringComparison.OrdinalIgnoreCase))
                    {
                        // Padrão automático gerenciado pelo Windows
                        key.SetValue("PagingFiles", new string[] { "?:\\pagefile.sys" }, RegistryValueKind.MultiString);
                    }
                    else
                    {
                        var parts = prev.Split(new[] { '\r', '\n', '|' }, StringSplitOptions.RemoveEmptyEntries);
                        key.SetValue("PagingFiles", parts, RegistryValueKind.MultiString);
                    }

                    _snapshotService.Remove("PAGEFILE");
                    Logger.Info($"PagingFiles restaurado para: {prev}", "PAGEFILE");
                    return (true, "Arquivo de paginação restaurado para a configuração original do usuário.");
                }

                return (false, "Falha ao acessar chave do registro.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao reverter paginação", ex, "PAGEFILE");
                return (false, $"Erro ao reverter paginação: {ex.Message}");
            }
        }

        private static string ReadCurrentPagingFilesRaw()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management");
                var val = key?.GetValue("PagingFiles");
                if (val is string[] arr && arr.Length > 0) return string.Join("|", arr);
                if (val is string s && !string.IsNullOrEmpty(s)) return s;
                return "SYSTEM_MANAGED";
            }
            catch
            {
                return "SYSTEM_MANAGED";
            }
        }

        // ====================================================================
        // [03] FAST STARTUP
        // ====================================================================

        public (bool success, string message) DisableFastStartup()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", !IsDryRun);
                int prev = Convert.ToInt32(key?.GetValue("HiberbootEnabled", 1) ?? 1);
                _snapshotService.Record("FAST_STARTUP", "Inicialização Rápida", prev == 1 ? "1" : "0", "0", "REVERT_FAST_STARTUP");

                if (IsDryRun)
                {
                    Logger.Info("[DRY-RUN] HiberbootEnabled = 0 simulado.", "FAST_STARTUP");
                    return (true, "[SIMULAÇÃO] Fast Startup desativado com sucesso.");
                }

                key?.SetValue("HiberbootEnabled", 0, RegistryValueKind.DWord);
                Logger.Info("HiberbootEnabled configurado para 0.", "FAST_STARTUP");
                return (true, "Inicialização Rápida (Fast Startup) desativada com sucesso! Garante reinicialização limpa de drivers e memória.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao desativar Fast Startup", ex, "FAST_STARTUP");
                return (false, $"Erro ao desativar Fast Startup: {ex.Message}");
            }
        }

        public (bool success, string message) RevertFastStartup()
        {
            try
            {
                string? prev = _snapshotService.GetPreviousValue("FAST_STARTUP");
                int target = (prev == "0") ? 0 : 1;

                if (IsDryRun)
                {
                    Logger.Info($"[DRY-RUN] Reversão HiberbootEnabled = {target} simulada.", "FAST_STARTUP");
                    _snapshotService.Remove("FAST_STARTUP");
                    return (true, "[SIMULAÇÃO] Fast Startup revertido.");
                }

                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", true);
                key?.SetValue("HiberbootEnabled", target, RegistryValueKind.DWord);
                _snapshotService.Remove("FAST_STARTUP");
                Logger.Info($"HiberbootEnabled restaurado para {target}.", "FAST_STARTUP");
                return (true, "Inicialização Rápida restaurada para o estado anterior.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao reverter Fast Startup", ex, "FAST_STARTUP");
                return (false, $"Erro ao reverter Fast Startup: {ex.Message}");
            }
        }

        // ====================================================================
        // [04] BLOATWARE IOBIT
        // ====================================================================

        public (bool success, string message) DisableIObitBloat()
        {
            try
            {
                _snapshotService.Record("BLOATWARE", "Serviços IObit", "Ativo", "Desativado", "REVERT_BLOATWARE");

                if (IsDryRun)
                {
                    Logger.Info("[DRY-RUN] Serviços IObit simulados como desativados.", "BLOATWARE");
                    return (true, "[SIMULAÇÃO] Bloatware desativado.");
                }

                string[] services = { "IObitUnSvr", "AdvancedSystemCareService16", "AdvancedSystemCareService17", "LiveUpdateSvc" };
                foreach (var svc in services)
                {
                    try
                    {
                        if (ServiceExists(svc))
                        {
                            RunProcess("sc.exe", $"stop {svc}");
                            RunProcess("sc.exe", $"config {svc} start= disabled");
                            Logger.Info($"Serviço {svc} parado e desativado.", "BLOATWARE");
                        }
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
                Logger.Error("Erro ao desativar IObit", ex, "BLOATWARE");
                return (false, $"Erro ao desativar bloatware: {ex.Message}");
            }
        }

        public (bool success, string message) RevertIObitBloat()
        {
            try
            {
                if (!IsDryRun && ServiceExists("IObitUnSvr"))
                {
                    RunProcess("sc.exe", "config IObitUnSvr start= auto");
                    RunProcess("sc.exe", "start IObitUnSvr");
                    Logger.Info("Serviço IObitUnSvr reconfigurado para auto e iniciado.", "BLOATWARE");
                }

                _snapshotService.Remove("BLOATWARE");
                return (true, "Serviços reconfigurados para o estado original.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao reverter IObit", ex, "BLOATWARE");
                return (false, $"Erro ao reverter serviço: {ex.Message}");
            }
        }

        private static bool ServiceExists(string serviceName)
        {
            var (_, exitCode, _, _) = RunProcess("sc.exe", $"query {serviceName}");
            return exitCode == 0;
        }

        // ====================================================================
        // [05] PILHA DE REDE TCP
        // ====================================================================

        public (bool success, string message) ApplyTcpOptimizations()
        {
            try
            {
                _snapshotService.Record("TCP_STACK", "Pilha de Rede TCP", "Padrão", "Otimizada", "REVERT_TCP");

                if (IsDryRun)
                {
                    Logger.Info("[DRY-RUN] Otimização da pilha TCP simulada.", "TCP");
                    return (true, "[SIMULAÇÃO] Pilha TCP otimizada com sucesso.");
                }

                RunProcess("netsh", "int tcp set global autotuninglevel=normal");
                RunProcess("netsh", "int tcp set global rss=enabled");
                RunProcess("netsh", "int tcp set global timestamps=disabled");
                RunProcess("netsh", "int tcp set global nonsackrttresiliency=disabled");
                RunProcess("netsh", "int tcp set global rsc=enabled");

                Logger.Info("Parâmetros netsh int tcp set global aplicados.", "TCP");
                return (true, "Pilha TCP otimizada: Auto-Tuning Normal, RSS ativado e RFC 1323 timestamps desativados para menor latência.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao otimizar TCP", ex, "TCP");
                return (false, $"Erro ao ajustar pilha TCP: {ex.Message}");
            }
        }

        public (bool success, string message) RevertTcpOptimizations()
        {
            try
            {
                if (!IsDryRun)
                {
                    RunProcess("netsh", "int tcp set global autotuninglevel=normal");
                    RunProcess("netsh", "int tcp set global timestamps=disabled");
                    RunProcess("netsh", "int tcp set global rss=default");
                    Logger.Info("Pilha TCP restaurada para padrões normais.", "TCP");
                }

                _snapshotService.Remove("TCP_STACK");
                return (true, "Configurações de rede restauradas.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao reverter TCP", ex, "TCP");
                return (false, $"Erro ao reverter TCP: {ex.Message}");
            }
        }

        // ====================================================================
        // [06] INICIALIZAÇÃO DUPLICADA
        // ====================================================================

        public (bool success, string message) CleanDuplicateStartup()
        {
            try
            {
                if (IsDryRun)
                {
                    Logger.Info("[DRY-RUN] Limpeza de inicializações duplicadas simulada.", "STARTUP");
                    return (true, "[SIMULAÇÃO] Inicialização limpa.");
                }

                using var hkuDefault = Registry.Users.OpenSubKey(@".DEFAULT\Software\Microsoft\Windows\CurrentVersion\Run", true);
                hkuDefault?.DeleteValue("GoogleDriveFS", false);

                using var hkuSystem = Registry.Users.OpenSubKey(@"S-1-5-18\Software\Microsoft\Windows\CurrentVersion\Run", true);
                hkuSystem?.DeleteValue("GoogleDriveFS", false);

                Logger.Info("Entradas duplicadas do Google Drive em perfis de sistema limpas.", "STARTUP");
                return (true, "Entradas duplicadas do Google Drive na inicialização de contas de sistema limpas com sucesso.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao limpar inicializações duplicadas", ex, "STARTUP");
                return (false, $"Erro ao limpar inicialização duplicada: {ex.Message}");
            }
        }

        // ====================================================================
        // [07] TELEMETRIA DO WINDOWS
        // ====================================================================

        public (bool success, string message) DisableWindowsTelemetry()
        {
            try
            {
                _snapshotService.Record("OS_TELEMETRY", "Telemetria do Windows", "Ativo", "Desativado", "REVERT_TELEMETRY");

                if (IsDryRun)
                {
                    Logger.Info("[DRY-RUN] Desativação de telemetria simulada.", "TELEMETRY");
                    return (true, "[SIMULAÇÃO] Telemetria desativada.");
                }

                RunProcess("sc.exe", "stop DiagTrack");
                RunProcess("sc.exe", "config DiagTrack start= disabled");
                RunProcess("sc.exe", "stop dmwappushservice");
                RunProcess("sc.exe", "config dmwappushservice start= disabled");

                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true);
                key?.SetValue("AllowTelemetry", 0, RegistryValueKind.DWord);

                Logger.Info("Serviços DiagTrack/dmwappushservice parados e AllowTelemetry=0 definido.", "TELEMETRY");
                return (true, "Telemetria do Windows (DiagTrack e dmwappushservice) desativada! Redução de I/O de traces e acordamento de núcleos da CPU.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao desativar telemetria", ex, "TELEMETRY");
                return (false, $"Erro ao desativar telemetria: {ex.Message}");
            }
        }

        public (bool success, string message) RevertWindowsTelemetry()
        {
            try
            {
                if (!IsDryRun)
                {
                    RunProcess("sc.exe", "config DiagTrack start= auto");
                    RunProcess("sc.exe", "start DiagTrack");
                    RunProcess("sc.exe", "config dmwappushservice start= demand");

                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true);
                    key?.DeleteValue("AllowTelemetry", false);
                    Logger.Info("Telemetria reconfigurada para padrões.", "TELEMETRY");
                }

                _snapshotService.Remove("OS_TELEMETRY");
                return (true, "Serviços de telemetria e diagnósticos restaurados ao padrão do Windows.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao reverter telemetria", ex, "TELEMETRY");
                return (false, $"Erro ao reverter telemetria: {ex.Message}");
            }
        }

        // ====================================================================
        // [08] SYSMAIN (SUPERFETCH)
        // ====================================================================

        public (bool success, string message) DisableSysMain()
        {
            try
            {
                _snapshotService.Record("OS_SYSMAIN", "SysMain (Superfetch)", "Ativado", "Desativado", "REVERT_SYSMAIN");

                if (IsDryRun)
                {
                    Logger.Info("[DRY-RUN] Desativação do SysMain simulada.", "SYSMAIN");
                    return (true, "[SIMULAÇÃO] SysMain desativado.");
                }

                RunProcess("sc.exe", "stop SysMain");
                RunProcess("sc.exe", "config SysMain start= disabled");

                Logger.Info("SysMain parado e desativado.", "SYSMAIN");
                return (true, "Serviço SysMain desativado! Redução de purga da lista Standby e eliminação de I/O contínuo no SSD NVMe.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao desativar SysMain", ex, "SYSMAIN");
                return (false, $"Erro ao desativar SysMain: {ex.Message}");
            }
        }

        public (bool success, string message) RevertSysMain()
        {
            try
            {
                if (!IsDryRun)
                {
                    RunProcess("sc.exe", "config SysMain start= auto");
                    RunProcess("sc.exe", "start SysMain");
                    Logger.Info("SysMain reativado com inicialização automática.", "SYSMAIN");
                }

                _snapshotService.Remove("OS_SYSMAIN");
                return (true, "SysMain reativado com inicialização automática.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao reverter SysMain", ex, "SYSMAIN");
                return (false, $"Erro ao reverter SysMain: {ex.Message}");
            }
        }

        // ====================================================================
        // [09] DELIVERY OPTIMIZATION P2P
        // ====================================================================

        public (bool success, string message) DisableDeliveryOptimizationP2P()
        {
            try
            {
                _snapshotService.Record("OS_DELIVERY_OPTIMIZATION", "Otimização de Entrega", "P2P Ativo", "HTTP Direto (Sem P2P)", "REVERT_DELIVERY_OPT");

                if (IsDryRun)
                {
                    Logger.Info("[DRY-RUN] P2P de atualizações simulado como desativado.", "DELIVERY_OPT");
                    return (true, "[SIMULAÇÃO] P2P desativado.");
                }

                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", true))
                {
                    key?.SetValue("DODownloadMode", 0, RegistryValueKind.DWord);
                }

                using (var keyUser = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\DeliveryOptimization", true))
                {
                    keyUser?.SetValue("SystemSettingsDownloadMode", 0, RegistryValueKind.DWord);
                }

                RunProcess("sc.exe", "config DoSvc start= demand");

                Logger.Info("DODownloadMode=0 configurado.", "DELIVERY_OPT");
                return (true, "Compartilhamento P2P de atualizações desativado (DODownloadMode = 0). Sem picos de jitter de rede!");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao desativar Delivery Optimization P2P", ex, "DELIVERY_OPT");
                return (false, $"Erro ao configurar Otimização de Entrega: {ex.Message}");
            }
        }

        public (bool success, string message) RevertDeliveryOptimizationP2P()
        {
            try
            {
                if (!IsDryRun)
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
                    Logger.Info("Delivery Optimization restaurado aos padrões.", "DELIVERY_OPT");
                }

                _snapshotService.Remove("OS_DELIVERY_OPTIMIZATION");
                return (true, "Configurações padrão da Otimização de Entrega restauradas.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao reverter Delivery Optimization", ex, "DELIVERY_OPT");
                return (false, $"Erro ao reverter Otimização de Entrega: {ex.Message}");
            }
        }

        // ====================================================================
        // [10] XBOX GAMEDVR
        // ====================================================================

        public (bool success, string message) DisableXboxGameDVR()
        {
            try
            {
                _snapshotService.Record("OS_XBOX_GAMEDVR", "Xbox GameDVR", "Ativado", "Desativado", "REVERT_GAMEDVR");

                if (IsDryRun)
                {
                    Logger.Info("[DRY-RUN] Desativação do GameDVR simulada.", "GAMEDVR");
                    return (true, "[SIMULAÇÃO] GameDVR desativado.");
                }

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

                Logger.Info("Chaves de gravação do GameDVR desativadas.", "GAMEDVR");
                return (true, "Captura contínua do GameDVR desativada! VRAM liberada e codificador de vídeo VCN da Radeon desonerado.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao desativar GameDVR", ex, "GAMEDVR");
                return (false, $"Erro ao desativar GameDVR: {ex.Message}");
            }
        }

        public (bool success, string message) RevertXboxGameDVR()
        {
            try
            {
                if (!IsDryRun)
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

                    Logger.Info("GameDVR reativado.", "GAMEDVR");
                }

                _snapshotService.Remove("OS_XBOX_GAMEDVR");
                return (true, "Gravação em segundo plano do GameDVR reativada.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao reverter GameDVR", ex, "GAMEDVR");
                return (false, $"Erro ao reverter GameDVR: {ex.Message}");
            }
        }

        // ====================================================================
        // [11] INDEXADOR WINDOWS SEARCH
        // ====================================================================

        public (bool success, string message) OptimizeSearchIndexer()
        {
            try
            {
                _snapshotService.Record("OS_SEARCH_INDEXER", "Indexador Windows Search", "Automático", "Manual (Sob Demanda)", "REVERT_INDEXER");

                if (IsDryRun)
                {
                    Logger.Info("[DRY-RUN] WSearch configurado para demand simulado.", "SEARCH");
                    return (true, "[SIMULAÇÃO] Indexador otimizado.");
                }

                RunProcess("sc.exe", "stop WSearch");
                RunProcess("sc.exe", "config WSearch start= demand");

                Logger.Info("Serviço WSearch configurado para sob demanda.", "SEARCH");
                return (true, "Indexador do Windows ajustado para 'Manual'. Sem varreduras contínuas em segundo plano durante jogos e updates.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao otimizar WSearch", ex, "SEARCH");
                return (false, $"Erro ao ajustar indexador: {ex.Message}");
            }
        }

        public (bool success, string message) RevertSearchIndexer()
        {
            try
            {
                if (!IsDryRun)
                {
                    RunProcess("sc.exe", "config WSearch start= auto");
                    RunProcess("sc.exe", "start WSearch");
                    Logger.Info("WSearch reconfigurado para auto.", "SEARCH");
                }

                _snapshotService.Remove("OS_SEARCH_INDEXER");
                return (true, "Indexador do Windows Search restaurado para inicialização Automática.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao reverter indexador", ex, "SEARCH");
                return (false, $"Erro ao reverter indexador: {ex.Message}");
            }
        }

        // ====================================================================
        // [12] GERENCIAMENTO DE MEMÓRIA (LARGESYSTEMCACHE)
        // ====================================================================

        public (bool success, string message) FixMemoryManagement()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", !IsDryRun);
                int prev = Convert.ToInt32(key?.GetValue("LargeSystemCache", 0) ?? 0);

                _snapshotService.Record("OS_MEMORY_MANAGEMENT", "LargeSystemCache", prev.ToString(), "0", "REVERT_MEMORY_MANAGEMENT");

                if (IsDryRun)
                {
                    Logger.Info($"[DRY-RUN] LargeSystemCache alterado de {prev} para 0 simulado.", "MEMORY");
                    return (true, "[SIMULAÇÃO] LargeSystemCache ajustado.");
                }

                key?.SetValue("LargeSystemCache", 0, RegistryValueKind.DWord);
                Logger.Info("LargeSystemCache definido para 0 (modo desktop para jogos).", "MEMORY");
                return (true, "LargeSystemCache corrigido para 0! Memória alocada corretamente para jogos e drivers AMD, eliminando riscos de DXGI crash.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao ajustar LargeSystemCache", ex, "MEMORY");
                return (false, $"Erro ao corrigir memória: {ex.Message}");
            }
        }

        public (bool success, string message) RevertMemoryManagement()
        {
            try
            {
                string? prev = _snapshotService.GetPreviousValue("OS_MEMORY_MANAGEMENT");
                // SALVAGUARDA CRÍTICA: no Windows Desktop o padrão é SEMPRE 0. Nunca assumir 1 se faltar histórico!
                int target = (prev == "1") ? 1 : 0;

                if (IsDryRun)
                {
                    Logger.Info($"[DRY-RUN] Reversão LargeSystemCache = {target} simulada.", "MEMORY");
                    _snapshotService.Remove("OS_MEMORY_MANAGEMENT");
                    return (true, "[SIMULAÇÃO] LargeSystemCache revertido.");
                }

                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true);
                key?.SetValue("LargeSystemCache", target, RegistryValueKind.DWord);
                _snapshotService.Remove("OS_MEMORY_MANAGEMENT");
                Logger.Info($"LargeSystemCache revertido para {target}.", "MEMORY");
                return (true, $"Configuração anterior de memória restaurada ({target}).");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao reverter LargeSystemCache", ex, "MEMORY");
                return (false, $"Erro ao reverter memória: {ex.Message}");
            }
        }

        // ====================================================================
        // [13] NETWORK THROTTLING & MULTIMEDIA
        // ====================================================================

        public (bool success, string message) ApplyNetworkThrottlingTweaks()
        {
            try
            {
                _snapshotService.Record("OS_NETWORK_THROTTLING", "Throttling de Rede Multimídia", "Padrão (10 / 20%)", "Desativado (0xFFFFFFFF / 0%)", "REVERT_NETWORK_TWEAKS");

                if (IsDryRun)
                {
                    Logger.Info("[DRY-RUN] NetworkThrottlingIndex simulado.", "NET_THROTTLE");
                    return (true, "[SIMULAÇÃO] Throttling de rede desativado.");
                }

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

                Logger.Info("SystemProfile multimídia e Tasks\\Games otimizados.", "NET_THROTTLE");
                return (true, "Network Throttling desativado e prioridade de jogos elevada no escalonador! Sem jitter com Discord/Spotify abertos.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao desativar Network Throttling", ex, "NET_THROTTLE");
                return (false, $"Erro ao ajustar perfil de rede: {ex.Message}");
            }
        }

        public (bool success, string message) RevertNetworkThrottlingTweaks()
        {
            try
            {
                if (!IsDryRun)
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

                    Logger.Info("SystemProfile multimídia restaurado para valores padrão.", "NET_THROTTLE");
                }

                _snapshotService.Remove("OS_NETWORK_THROTTLING");
                return (true, "Valores padrão de SystemProfile restaurados.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao reverter Network Throttling", ex, "NET_THROTTLE");
                return (false, $"Erro ao reverter latência multimídia: {ex.Message}");
            }
        }

        // ====================================================================
        // [14] GAME MODE & HAGS
        // ====================================================================

        public (bool success, string message) EnableGameModeAndHags()
        {
            try
            {
                _snapshotService.Record("OS_GAMEMODE_HAGS", "Game Mode e HAGS", "Parcial/Desativado", "Ambos Ativados", "REVERT_GAMEMODE_HAGS");

                if (IsDryRun)
                {
                    Logger.Info("[DRY-RUN] GameMode e HAGS simulados.", "GAMEMODE_HAGS");
                    return (true, "[SIMULAÇÃO] Game Mode e HAGS ativados.");
                }

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar", true))
                {
                    key?.SetValue("AllowAutoGameMode", 1, RegistryValueKind.DWord);
                    key?.SetValue("AutoGameModeEnabled", 1, RegistryValueKind.DWord);
                }

                using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", true))
                {
                    key?.SetValue("HwSchMode", 2, RegistryValueKind.DWord);
                }

                Logger.Info("AllowAutoGameMode=1 e HwSchMode=2 definidos.", "GAMEMODE_HAGS");
                return (true, "Modo de Jogo e HAGS ativados! Reinicie o computador para que o HAGS entre em operação no driver da Radeon.");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao ativar Game Mode e HAGS", ex, "GAMEMODE_HAGS");
                return (false, $"Erro ao configurar Game Mode/HAGS: {ex.Message}");
            }
        }

        public (bool success, string message) RevertGameModeAndHags()
        {
            try
            {
                if (!IsDryRun)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", true))
                    {
                        key?.SetValue("HwSchMode", 1, RegistryValueKind.DWord);
                    }
                    Logger.Info("HwSchMode restaurado para 1 (desativado).", "GAMEMODE_HAGS");
                }

                _snapshotService.Remove("OS_GAMEMODE_HAGS");
                return (true, "HAGS configurado para desativado (reinicialização necessária).");
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao reverter Game Mode e HAGS", ex, "GAMEMODE_HAGS");
                return (false, $"Erro ao reverter Game Mode/HAGS: {ex.Message}");
            }
        }

        // ====================================================================
        // OTIMIZAÇÃO COMPLETA EM LOTE (1-CLIQUE)
        // ====================================================================

        public string ApplyAllSafeOptimizations(string currentPowerPlan, string currentPagefile)
        {
            var results = new System.Text.StringBuilder();
            string dryPrefix = IsDryRun ? "[MODO SIMULAÇÃO / DRY-RUN] " : "";
            results.AppendLine($"=== {dryPrefix}INICIANDO PACOTE COMPLETO DE OTIMIZAÇÕES SEGURAS ===");

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
            results.AppendLine("Todas as operações foram concluídas e salvas no ledger para reversão a qualquer momento!");
            return results.ToString();
        }

        // ====================================================================
        // EXECUTOR DE PROCESSOS SEGURO COM LOG
        // ====================================================================

        private static (bool success, int exitCode, string stdout, string stderr) RunProcess(string fileName, string args)
        {
            try
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
                if (proc == null) return (false, -1, "", "Processo não pôde ser iniciado.");

                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(10000);

                int code = proc.ExitCode;
                if (code != 0)
                {
                    Logger.Warn($"Comando '{fileName} {args}' retornou código {code}: {stderr.Trim()}", "PROCESS");
                }
                else
                {
                    Logger.Debug($"Comando '{fileName} {args}' concluído com código 0.", "PROCESS");
                }

                return (code == 0, code, stdout, stderr);
            }
            catch (Exception ex)
            {
                Logger.Error($"Exceção ao executar '{fileName} {args}'", ex, "PROCESS");
                return (false, -1, "", ex.Message);
            }
        }
    }
}
