using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AMDAutoTuning.Models;
using Microsoft.Win32;

namespace AMDAutoTuning.Services
{
    /// <summary>
    /// Verifica os pre-requisitos externos do app (runtime nativo, winget, WMI e o
    /// stack AMD) antes da auditoria. Tambem instala dependencias via winget.
    ///
    /// Regra: <see cref="CheckAll"/> nunca lanca excecao — uma dependencia que nao
    /// pode ser inspecionada vira Missing com a mensagem do erro, nunca um crash.
    /// </summary>
    public class DependencyChecker
    {
        private const string VcRedistWingetId = "Microsoft.VCRedist.2015+.x64";

        // ------------------------------------------------------------------
        // Catalogo
        // ------------------------------------------------------------------

        /// <summary>Monta a lista de dependencias (sem verificar) na ordem de exibicao.</summary>
        public List<DependencyItem> BuildCatalog() => new()
        {
            new DependencyItem
            {
                Id = "WMI",
                Name = "WMI — Instrumentação de Gerenciamento do Windows",
                Description = "Consultado para ler CPU, GPU, RAM, placa-mãe, ReBAR e monitores. Sem ele, a auditoria não funciona.",
                Kind = DependencyKind.Required,
                CanRepair = true,
                RepairLabel = "♻ Iniciar serviço"
            },
            new DependencyItem
            {
                Id = "VCREDIST_X64",
                Name = "Microsoft Visual C++ 2015–2022 Redistributable (x64)",
                Description = "Bibliotecas nativas exigidas por instaladores, drivers de chipset e componentes de terceiros.",
                Kind = DependencyKind.Required,
                WingetId = VcRedistWingetId,
                DownloadUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                DownloadLabel = "🌐 Baixar (Microsoft)"
            },
            new DependencyItem
            {
                Id = "WINGET",
                Name = "Windows Package Manager (winget)",
                Description = "Usado pelos botões de instalação automática desta tela. Sem ele, use os links de download direto.",
                Kind = DependencyKind.Recommended,
                DownloadUrl = "https://aka.ms/getwinget",
                DownloadLabel = "🌐 Baixar (App Installer)"
            },
            new DependencyItem
            {
                Id = "AMD_GPU_DRIVER",
                Name = "Driver AMD Radeon (Adrenalin)",
                Description = "Driver de vídeo proprietário com suporte a RDNA, SAM/ReBAR e aceleração de mídia.",
                Kind = DependencyKind.Required,
                DownloadUrl = "https://www.amd.com/pt/support/download/drivers.html",
                DownloadLabel = "🌐 Baixar (AMD)"
            },
            new DependencyItem
            {
                Id = "AMD_CHIPSET",
                Name = "Driver de Chipset AMD (AM5)",
                Description = "Instala o AMD PPM, responsável pelo agendamento CPPC dos núcleos Gold/Silver no Zen 4.",
                Kind = DependencyKind.Recommended,
                DownloadUrl = "https://www.amd.com/pt/support/download/chipsets/am5.html",
                DownloadLabel = "🌐 Baixar (AMD)"
            },
            new DependencyItem
            {
                Id = "RYZEN_MASTER",
                Name = "AMD Ryzen Master",
                Description = "Necessário apenas para os guias de Curve Optimizer e PBO. Não bloqueia a auditoria.",
                Kind = DependencyKind.Optional,
                DownloadUrl = "https://www.amd.com/pt/products/software/ryzen-master.html",
                DownloadLabel = "🌐 Baixar (AMD)"
            }
        };

        /// <summary>Verifica todas as dependencias e devolve uma lista nova (seguro para rodar em thread de fundo).</summary>
        public List<DependencyItem> CheckAll()
        {
            var items = BuildCatalog();
            foreach (var item in items)
            {
                CheckItem(item);
            }
            return items;
        }

        private void CheckItem(DependencyItem item)
        {
            try
            {
                switch (item.Id)
                {
                    case "WMI": CheckWmi(item); break;
                    case "VCREDIST_X64": CheckVcRedist(item); break;
                    case "WINGET": CheckWinget(item); break;
                    case "AMD_GPU_DRIVER": CheckAmdGpuDriver(item); break;
                    case "AMD_CHIPSET": CheckAmdChipset(item); break;
                    case "RYZEN_MASTER": CheckRyzenMaster(item); break;
                    default:
                        item.State = DependencyState.Pending;
                        item.DetectedValue = "Verificação não implementada.";
                        break;
                }
            }
            catch (Exception ex)
            {
                item.State = DependencyState.Missing;
                item.DetectedValue = $"Não foi possível verificar: {ex.Message}";
            }
        }

        // ------------------------------------------------------------------
        // Detectores
        // ------------------------------------------------------------------

        private static void CheckWmi(DependencyItem item)
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption, BuildNumber FROM Win32_OperatingSystem");
            using var results = searcher.Get();
            string caption = string.Empty;
            string build = string.Empty;

            foreach (ManagementObject obj in results)
            {
                caption = obj["Caption"]?.ToString() ?? string.Empty;
                build = obj["BuildNumber"]?.ToString() ?? string.Empty;
                break;
            }

            if (string.IsNullOrEmpty(caption))
            {
                item.State = DependencyState.Missing;
                item.DetectedValue = "Consulta WMI não retornou dados.";
                return;
            }

            item.State = DependencyState.Installed;
            item.DetectedValue = $"{caption} (Build {build})";
        }

        private static void CheckVcRedist(DependencyItem item)
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64");
            if (key == null)
            {
                item.State = DependencyState.Missing;
                item.DetectedValue = "Chave HKLM\\SOFTWARE\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes\\X64 ausente.";
                return;
            }

            bool installed = false;
            var rawInstalled = key.GetValue("Installed");
            if (rawInstalled != null)
            {
                installed = rawInstalled switch
                {
                    int i => i == 1,
                    long l => l == 1,
                    _ => rawInstalled.ToString() == "1"
                };
            }

            string version = key.GetValue("Version")?.ToString() ?? "versão desconhecida";

            if (installed)
            {
                item.State = DependencyState.Installed;
                item.DetectedValue = $"Redistributable {version} presente.";
            }
            else
            {
                item.State = DependencyState.Missing;
                item.DetectedValue = "Chave de registro encontrada, mas sem a flag Installed = 1.";
            }
        }

        private static void CheckWinget(DependencyItem item)
        {
            string? path = FindWingetPath();
            string version = string.Empty;

            if (path != null && TryRun(path, "--version", out version, 20000) && version.Length > 0)
            {
                item.State = DependencyState.Installed;
                item.DetectedValue = $"winget {version} ({path})";
                return;
            }

            // Fallback: alias do WindowsApps resolvido pelo proprio ShellExecute/PATH
            if (TryRun("winget", "--version", out version, 20000) && version.Length > 0)
            {
                item.State = DependencyState.Installed;
                item.DetectedValue = $"winget {version}";
                return;
            }

            item.State = DependencyState.Missing;
            item.DetectedValue = "winget.exe não localizado no PATH nem em %LOCALAPPDATA%\\Microsoft\\WindowsApps.";
        }

        private static void CheckAmdGpuDriver(DependencyItem item)
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion, DriverDate FROM Win32_VideoController");

            string? amdGpu = null;
            string amdDriver = string.Empty;
            string? basicAdapter = null;

            foreach (ManagementObject obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? string.Empty;
                if (name.Length == 0) continue;

                if (name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Basic Render", StringComparison.OrdinalIgnoreCase))
                {
                    basicAdapter ??= name;
                    continue;
                }

                bool isAmd = name.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("AMD", StringComparison.OrdinalIgnoreCase);

                if (!isAmd) continue;

                amdGpu = name;
                amdDriver = obj["DriverVersion"]?.ToString() ?? string.Empty;
                break;
            }

            if (amdGpu != null)
            {
                item.State = DependencyState.Installed;
                item.DetectedValue = string.IsNullOrEmpty(amdDriver)
                    ? $"{amdGpu} (versão do driver indisponível)"
                    : $"{amdGpu} · driver {amdDriver}";
                return;
            }

            if (basicAdapter != null)
            {
                item.State = DependencyState.Missing;
                item.DetectedValue = $"Somente adaptador genérico ativo: {basicAdapter}.";
                return;
            }

            item.State = DependencyState.NotApplicable;
            item.DetectedValue = "Nenhuma GPU AMD detectada nesta máquina.";
        }

        private static void CheckAmdChipset(DependencyItem item)
        {
            bool isAmdCpu;
            using (var searcherCpu = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_Processor"))
            {
                isAmdCpu = searcherCpu.Get().Cast<ManagementObject>()
                    .Any(o => (o["Manufacturer"]?.ToString() ?? string.Empty)
                        .Contains("AMD", StringComparison.OrdinalIgnoreCase));
            }

            if (!isAmdCpu)
            {
                item.State = DependencyState.NotApplicable;
                item.DetectedValue = "Processador não é AMD — dependência não se aplica.";
                return;
            }

            using var ppmKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\amdppm");
            bool ppmInstalled = ppmKey != null;

            string ppmDriver = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"System32\drivers\amdppm.sys");

            if (ppmInstalled || File.Exists(ppmDriver))
            {
                item.State = DependencyState.Installed;
                item.DetectedValue = "AMD PPM (Power Management Provisioning) presente — agendamento CPPC ativo.";
            }
            else
            {
                item.State = DependencyState.Missing;
                item.DetectedValue = "amdppm.sys ausente: recurso de chipset AMD possivelmente não instalado.";
            }
        }

        private static void CheckRyzenMaster(DependencyItem item)
        {
            const string marker = "Ryzen Master";

            foreach (var hive in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
            {
                using var uninstall = Registry.LocalMachine.OpenSubKey(hive);
                if (uninstall == null) continue;

                foreach (var subName in uninstall.GetSubKeyNames())
                {
                    using var sub = uninstall.OpenSubKey(subName);
                    string displayName = sub?.GetValue("DisplayName")?.ToString() ?? string.Empty;
                    if (displayName.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    {
                        string version = sub?.GetValue("DisplayVersion")?.ToString() ?? "versão desconhecida";
                        item.State = DependencyState.Installed;
                        item.DetectedValue = $"{displayName} ({version})";
                        return;
                    }
                }
            }

            string? programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string path = Path.Combine(programFiles, @"AMD\RyzenMaster\bin");
            if (Directory.Exists(path))
            {
                item.State = DependencyState.Installed;
                item.DetectedValue = $"Instalado em {path}.";
                return;
            }

            item.State = DependencyState.Missing;
            item.DetectedValue = "Ryzen Master não localizado. Necessário apenas para os guias de PBO/Curve Optimizer.";
        }

        // ------------------------------------------------------------------
        // Instalacao / reparo
        // ------------------------------------------------------------------

        /// <summary>
        /// Instala a dependencia via winget (silencioso) e revalida o item.
        /// Retorna sucesso com base na deteccao real, nunca apenas no exit code.
        /// </summary>
        public async Task<(bool success, string message)> InstallViaWingetAsync(DependencyItem item, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(item.WingetId))
            {
                return (false, $"{item.Name}: instalação automatizada não disponível. Use o download oficial.");
            }

            string arguments = $"install --id {item.WingetId} -e --silent --accept-package-agreements " +
                               "--accept-source-agreements --disable-interactivity";

            log($"winget {arguments}");

            var psi = new ProcessStartInfo("winget", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log(e.Data); };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                process.Dispose();
                return (false, $"Não foi possível executar o winget: {ex.Message}");
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { /* processo já encerrado */ }
                process.Dispose();
                return (false, "Tempo esgotado (10 min) aguardando o winget. Verifique manualmente.");
            }

            int exitCode = process.ExitCode;
            process.Dispose();

            // Revalida a dependencia: exit code do winget não é garantia de instalação.
            CheckItem(item);

            if (item.IsInstalled)
            {
                return (true, $"{item.Name} instalado e validado com sucesso.");
            }

            return (false, $"winget terminou com código {exitCode}, mas a dependência ainda não foi detectada.");
        }

        /// <summary>Repara uma dependencia com acao conhecida (por ora, apenas o servico WMI).</summary>
        public (bool success, string message) Repair(DependencyItem item)
        {
            if (item.Id != "WMI")
            {
                return (false, "Nenhum reparo automático disponível para esta dependência.");
            }

            TryRun("net", "start winmgmt", out string output, 30000);

            // Nao mutamos o item aqui: este metodo roda em thread de fundo e o
            // MainWindow revalida tudo em seguida, ja na thread de UI.
            if (IsWmiFunctional())
            {
                return (true, "Serviço WMI iniciado com sucesso.");
            }

            return (false,
                "Não foi possível iniciar o serviço WMI automaticamente.\n\n" +
                "Abra um Prompt de Comando como Administrador e execute:\n" +
                "    winmgmt /verifyrepository\n\n" +
                "Se o repositório estiver inconsistente, use 'winmgmt /salvagerepository' e reinicie o computador." +
                (string.IsNullOrWhiteSpace(output) ? string.Empty : $"\n\nDetalhe: {output}"));
        }

        private static bool IsWmiFunctional()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT BuildNumber FROM Win32_OperatingSystem");
                using var results = searcher.Get();
                foreach (ManagementObject obj in results)
                {
                    return obj["BuildNumber"] != null;
                }
            }
            catch
            {
                // WMI indisponível ou corrompido.
            }

            return false;
        }

        // ------------------------------------------------------------------
        // Helpers de processo
        // ------------------------------------------------------------------

        private static string? FindWingetPath()
        {
            // 1. PATH
            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim(), "winget.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch
                {
                    // Entrada de PATH inválida: ignora.
                }
            }

            // 2. Alias do usuário
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string alias = Path.Combine(localAppData, @"Microsoft\WindowsApps\winget.exe");
            if (File.Exists(alias)) return alias;

            // 3. Pacote versionado do App Installer
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string windowsApps = Path.Combine(programFiles, "WindowsApps");
            try
            {
                if (Directory.Exists(windowsApps))
                {
                    foreach (var dir in Directory.GetDirectories(windowsApps, "Microsoft.DesktopAppInstaller_*"))
                    {
                        string candidate = Path.Combine(dir, "winget.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            catch
            {
                // Acesso negado em WindowsApps: sem problema, os outros caminhos cobrem.
            }

            return null;
        }

        private static bool TryRun(string fileName, string arguments, out string output, int timeoutMs = 15000)
        {
            output = string.Empty;
            try
            {
                var psi = new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(true); } catch { }
                    return false;
                }

                string text = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
                output = text.Trim();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
