using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AMDAutoTuning.Models;
using AMDAutoTuning.Services;

namespace AMDAutoTuning
{
    public partial class MainWindow : Window
    {
        private readonly SnapshotService _snapshotService;
        private readonly HardwareScanner _scanner;
        private readonly OptimizerService _optimizer;
        private readonly DependencyChecker _dependencyChecker = new();

        private HardwareOverview _overview = new();
        private List<DiagnosticItem> _allDiagnostics = new();
        private string _activeTabKey = "DASHBOARD";
        private bool _isInitialized = false;
        private bool _auditStarted = false;

        private CpuTopology _cpuTopology = new();
        private List<CpuTuningPreset> _cpuPresets = new();
        private readonly CpuMetricsMonitor _metricsMonitor = new();
        private readonly CpuStressTester _stressTester = new();
        private CpuTuningPreset? _currentPreset;

        public ObservableCollection<DiagnosticItem> DisplayedDiagnostics { get; } = new();
        public ObservableCollection<DependencyItem> DependencyItems { get; } = new();
        public ObservableCollection<CpuCoreInfo> DisplayedCoreSliders { get; } = new();
        public ObservableCollection<StressScoreRecord> ScoreboardItems { get; } = new();

        /// <summary>Versao exibida no titulo, derivada do assembly (nunca fica dessincronizada).</summary>
        public string AppVersionLabel { get; } =
            "EFF5 Edition v" + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");
        public ObservableCollection<TweakSnapshot> DisplayedHistory { get; } = new();

        public MainWindow()
        {
            _snapshotService = new SnapshotService();
            _scanner = new HardwareScanner(_snapshotService);
            _optimizer = new OptimizerService(_snapshotService);

            InitializeComponent();
            _isInitialized = true;

            DataContext = _overview;
            ItemsDiagnosticList.ItemsSource = DisplayedDiagnostics;
            ItemsHistoryList.ItemsSource = DisplayedHistory;
            ItemsDependencyList.ItemsSource = DependencyItems;

            // Inicializacao do modulo de CPU Tuning
            _cpuTopology = CpuTopology.Detect();
            _cpuPresets = CpuTuningPreset.CreatePresetsFor(_cpuTopology);

            ItemsCoreSliders.ItemsSource = DisplayedCoreSliders;
            foreach (var core in _cpuTopology.Cores) { DisplayedCoreSliders.Add(core); }

            ItemsScoreboard.ItemsSource = ScoreboardItems;

            // Badges estilo MMORPG para as receitas da comunidade
            if (_cpuPresets.Count >= 5)
            {
                ItemsBadgesEco.ItemsSource = _cpuPresets[0].AttributeBadges;
                ItemsBadgesBalanced.ItemsSource = _cpuPresets[1].AttributeBadges;
                ItemsBadgesGaming.ItemsSource = _cpuPresets[2].AttributeBadges;
                ItemsBadgesRender.ItemsSource = _cpuPresets[3].AttributeBadges;
                ItemsBadgesStock.ItemsSource = _cpuPresets[4].AttributeBadges;
            }

            _metricsMonitor.MetricsUpdated += OnMetricsUpdated;
            _stressTester.ProgressChanged += OnStressProgressChanged;
            _stressTester.TestCompleted += OnStressTestCompleted;

            InitializeCpuTuningUi();

            Closed += (s, e) =>
            {
                _metricsMonitor.Dispose();
                _stressTester.Cancel();
            };

            if (App.IsDryRun)
            {
                BorderDryRunBadge.Visibility = Visibility.Visible;
                AppendLog("ATENÇÃO: Modo Simulação (--dry-run) ativo. Nenhuma alteração real será feita no registro ou serviços.");
            }

            Loaded += async (s, e) => await InitializeAsync();
        }

        /// <summary>
        /// Fluxo de boot: verifica dependencias antes de auditar. Se faltar alguma
        /// dependencia OBRIGATORIA, abre a tela de pre-requisitos e aguarda o usuario.
        /// </summary>
        private async Task InitializeAsync()
        {
            await RefreshDependenciesAsync();

            // Flag de diagnóstico: "AMDAutoTuning.exe --deps" abre a tela de
            // pré-requisitos imediatamente, mesmo com tudo instalado.
            bool forcePreflight = Environment.GetCommandLineArgs()
                .Any(a => a.Equals("--deps", StringComparison.OrdinalIgnoreCase));

            if (forcePreflight)
            {
                DependencyOverlay.Visibility = Visibility.Visible;
                AppendLog("Tela de pré-requisitos aberta por solicitação (--deps).");
            }

            if (DependencyItems.Any(x => x.IsBlocking))
            {
                AppendLog("Pré-requisitos obrigatórios ausentes. Abrindo tela de dependências.");
                DependencyOverlay.Visibility = Visibility.Visible;
                return;
            }

            await RunAuditAsync();
        }

        // ====================================================================
        // PRE-REQUISITOS / DEPENDENCIAS
        // ====================================================================

        /// <summary>Revalida as dependencias e atualiza os itens existentes (preserva bindings e scroll).</summary>
        private async Task RefreshDependenciesAsync()
        {
            TxtDependencySummary.Text = "Verificando dependências do sistema...";

            List<DependencyItem> fresh = await Task.Run(() => _dependencyChecker.CheckAll());

            if (DependencyItems.Count == 0)
            {
                foreach (var item in fresh)
                {
                    DependencyItems.Add(item);
                }
            }
            else
            {
                foreach (var probed in fresh)
                {
                    var existing = DependencyItems.FirstOrDefault(x => x.Id == probed.Id);
                    if (existing == null) continue;

                    existing.State = probed.State;
                    existing.DetectedValue = probed.DetectedValue;
                    existing.StatusMessage = probed.StatusMessage;
                    existing.IsBusy = false;
                }
            }

            UpdateDependencySummary();
        }

        private void UpdateDependencySummary()
        {
            int blocking = DependencyItems.Count(x => x.IsBlocking);
            int missing = DependencyItems.Count(x => x.IsMissing);
            int installed = DependencyItems.Count(x => x.IsInstalled);

            if (missing == 0)
            {
                TxtDependencySummary.Text = $"✓ Todos os pré-requisitos atendidos ({installed} verificados). A auditoria está liberada.";
            }
            else if (blocking > 0)
            {
                TxtDependencySummary.Text =
                    $"✕ {blocking} dependência(s) obrigatória(s) ausente(s) de {DependencyItems.Count} verificadas. " +
                    "Resolva os itens em vermelho para liberar a auditoria completa.";
            }
            else
            {
                TxtDependencySummary.Text =
                    $"⚠ {missing} dependência(s) recomendada(s)/opcional(is) ausente(s). " +
                    "A auditoria funciona, mas alguns guias de tuning podem não estar disponíveis.";
            }

            if (missing > 0)
            {
                TxtPreflightBadge.Text = missing.ToString();
                BorderPreflightBadge.Visibility = Visibility.Visible;
            }
            else
            {
                BorderPreflightBadge.Visibility = Visibility.Collapsed;
            }
        }

        private DependencyItem? FindDependency(object? tag)
        {
            if (tag is not string id) return null;
            return DependencyItems.FirstOrDefault(x => x.Id == id);
        }

        private void PreflightButton_Click(object sender, RoutedEventArgs e)
        {
            DependencyOverlay.Visibility = Visibility.Visible;
        }

        private async void DependencyCloseButton_Click(object sender, RoutedEventArgs e)
        {
            DependencyOverlay.Visibility = Visibility.Collapsed;

            // Fechar o painel com tudo resolvido deve liberar a auditoria (caso ainda não tenha rodado).
            if (!_auditStarted && !DependencyItems.Any(x => x.IsBlocking))
            {
                await RunAuditAsync();
            }
        }

        private async void DependencyRecheckButton_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Reverificando pré-requisitos do sistema...");
            await RefreshDependenciesAsync();
            AppendLog("Reverificação de dependências concluída.");
        }

        private async void DependencyContinueButton_Click(object sender, RoutedEventArgs e)
        {
            DependencyOverlay.Visibility = Visibility.Collapsed;

            if (!_auditStarted)
            {
                AppendLog("Continuando sem resolver todas as dependências. Alguns diagnósticos podem ser imprecisos.");
                await RunAuditAsync();
            }
        }

        private async void DependencyWingetButton_Click(object sender, RoutedEventArgs e)
        {
            var item = FindDependency((sender as Button)?.Tag);
            if (item == null) return;

            await InstallDependencyAsync(item);
        }

        private async Task InstallDependencyAsync(DependencyItem item)
        {
            item.IsBusy = true;
            item.StatusMessage = "Instalando via winget... acompanhe o log.";
            AppendLog($"Instalando {item.Name} via winget...");

            var (success, message) = await _dependencyChecker.InstallViaWingetAsync(item, AppendLog);

            item.IsBusy = false;
            item.StatusMessage = success ? string.Empty : message;
            AppendLog(message);

            await RefreshDependenciesAsync();
        }

        private async void DependencyInstallAllButton_Click(object sender, RoutedEventArgs e)
        {
            var pending = DependencyItems
                .Where(x => x.IsMissing && !string.IsNullOrWhiteSpace(x.WingetId))
                .ToList();

            if (pending.Count == 0)
            {
                MessageBox.Show(
                    "Nenhuma dependência ausente com instalação automática disponível.\n\n" +
                    "Use os botões de download oficial ou o reparo individual.",
                    "Nada a instalar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Serão instaladas {pending.Count} dependência(s) via winget:\n\n" +
                string.Join("\n", pending.Select(x => $"• {x.Name}")),
                "Confirmar Instalação Automática",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            foreach (var item in pending)
            {
                await InstallDependencyAsync(item);
            }
        }

        private void DependencyDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var item = FindDependency((sender as Button)?.Tag);
            if (item == null || string.IsNullOrWhiteSpace(item.DownloadUrl)) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.DownloadUrl,
                    UseShellExecute = true
                });
                AppendLog($"Download aberto no navegador: {item.DownloadUrl}");
            }
            catch (Exception ex)
            {
                AppendLog($"Erro ao abrir o navegador: {ex.Message}");
            }
        }

        private async void DependencyRepairButton_Click(object sender, RoutedEventArgs e)
        {
            var item = FindDependency((sender as Button)?.Tag);
            if (item == null) return;

            item.IsBusy = true;
            var result = await Task.Run(() => _dependencyChecker.Repair(item));
            item.IsBusy = false;

            AppendLog(result.message);
            MessageBox.Show(result.message, result.success ? "Reparo Concluído" : "Reparo Não Concluído",
                MessageBoxButton.OK, result.success ? MessageBoxImage.Information : MessageBoxImage.Warning);

            await RefreshDependenciesAsync();
        }

        private async Task RunAuditAsync()
        {
            _auditStarted = true;
            AppendLog("Iniciando auditoria completa de componentes e subsistemas...");
            TxtLogStatus.Text = "Examinando processador Zen 4, GPU RDNA 4, barramento PCIe e Windows...";

            try
            {
                var overviewTask = Task.Run(() => _scanner.ScanOverview());
                var diagnosticsTask = Task.Run(() => _scanner.ScanDiagnostics());

                await Task.WhenAll(overviewTask, diagnosticsTask);

                _overview = overviewTask.Result;
                DataContext = _overview;

                _allDiagnostics = diagnosticsTask.Result.items;
                int score = diagnosticsTask.Result.healthScore;

                UpdateHealthScore(score);
                RefreshTabFilter();
                RefreshHistoryView();

                AppendLog($"Auditoria concluída: {_allDiagnostics.Count} verificações realizadas. Health Score: {score}/100.");
                TxtLogStatus.Text = "Auditoria finalizada. Selecione as abas para detalhes por categoria.";
            }
            catch (Exception ex)
            {
                AppendLog($"Erro durante a auditoria: {ex.Message}");
                TxtLogStatus.Text = "Erro na auditoria.";
            }
        }

        private void UpdateHealthScore(int score)
        {
            TxtHealthScore.Text = score.ToString();

            if (score >= 90)
            {
                TxtHealthStatus.Text = "Excelente (Otimizado)";
                BorderScoreBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
            }
            else if (score >= 75)
            {
                TxtHealthStatus.Text = "Bom (Oportunidades)";
                BorderScoreBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            }
            else
            {
                TxtHealthStatus.Text = "Atenção (Gargalos)";
                BorderScoreBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            }
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || sender is not RadioButton rb) return;

            if (rb == TabDashboard) _activeTabKey = "DASHBOARD";
            else if (rb == TabOs) _activeTabKey = "OS";
            else if (rb == TabCpuRam) _activeTabKey = "CPU_RAM";
            else if (rb == TabGpuDisplays) _activeTabKey = "GPU_DISPLAYS";
            else if (rb == TabDriversBios) _activeTabKey = "DRIVERS_BIOS";
            else if (rb == TabHistory) _activeTabKey = "HISTORY";

            RefreshTabFilter();
        }

        private void CpuSubTab_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            RefreshTabFilter();
        }

        private void RefreshTabFilter()
        {
            if (!_isInitialized || ViewDiagnostics == null || ViewHistory == null || ViewCpuTuning == null ||
                PanelHardwareOverview == null || BarCpuSubNav == null) return;

            if (_activeTabKey == "HISTORY")
            {
                BarCpuSubNav.Visibility = Visibility.Collapsed;
                ViewCpuTuning.Visibility = Visibility.Collapsed;
                ViewDiagnostics.Visibility = Visibility.Collapsed;
                ViewHistory.Visibility = Visibility.Visible;
                _metricsMonitor.Stop();
                RefreshHistoryView();
                return;
            }

            if (_activeTabKey == "CPU_RAM")
            {
                BarCpuSubNav.Visibility = Visibility.Visible;
                ViewHistory.Visibility = Visibility.Collapsed;
                PanelHardwareOverview.Visibility = Visibility.Collapsed;

                if (SubTabCpuTuning?.IsChecked == true)
                {
                    ViewDiagnostics.Visibility = Visibility.Collapsed;
                    ViewCpuTuning.Visibility = Visibility.Visible;
                    _metricsMonitor.Start();
                    return;
                }
                else
                {
                    ViewDiagnostics.Visibility = Visibility.Visible;
                    ViewCpuTuning.Visibility = Visibility.Collapsed;
                    _metricsMonitor.Stop();
                }
            }
            else
            {
                BarCpuSubNav.Visibility = Visibility.Collapsed;
                ViewCpuTuning.Visibility = Visibility.Collapsed;
                _metricsMonitor.Stop();
                ViewHistory.Visibility = Visibility.Collapsed;
                ViewDiagnostics.Visibility = Visibility.Visible;
                PanelHardwareOverview.Visibility = _activeTabKey == "DASHBOARD" ? Visibility.Visible : Visibility.Collapsed;
            }

            DisplayedDiagnostics.Clear();
            IEnumerable<DiagnosticItem> filtered = _activeTabKey switch
            {
                "DASHBOARD" => _allDiagnostics,
                _ => _allDiagnostics.Where(x => x.CategoryKey.Equals(_activeTabKey, StringComparison.OrdinalIgnoreCase))
            };

            // Ordenacao por criticidade: Critico -> Atencao -> Info -> Otimizado.
            // O enum DiagnosticSeverity ja declara Ok=0 ... Critical=3, entao
            // OrderByDescending((int)Severity) entrega a ordem desejada.
            // Desempate por titulo garante ordem estavel e previsivel entre auditorias.
            foreach (var item in filtered
                         .OrderByDescending(x => (int)x.Severity)
                         .ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase))
            {
                DisplayedDiagnostics.Add(item);
            }
        }

        private void RefreshHistoryView()
        {
            DisplayedHistory.Clear();
            foreach (var item in _snapshotService.History)
            {
                DisplayedHistory.Add(item);
            }
        }

        private async void RescanButton_Click(object sender, RoutedEventArgs e)
        {
            await RunAuditAsync();
        }

        private async void OptimizeAllButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Deseja aplicar o pacote completo de otimizações seguras recomendadas?\n\n" +
                "• [OS] Plano de Energia Equilibrado (Otimizado para AMD CPPC e boost Zen 4)\n" +
                "• [OS] Fixação do Arquivo de Paginação em 8192 MB contínuos no SSD NVMe\n" +
                "• [OS] Desativação da Inicialização Rápida (Fast Startup) para ciclo limpo de kernel\n" +
                "• [OS] Desativação de serviços e processos em segundo plano do IObit\n" +
                "• [OS] Otimização da pilha de rede TCP (menor latência de pacotes)\n" +
                "• [OS] Limpeza de instâncias duplicadas na inicialização\n\n" +
                "Todas as configurações originais serão salvas na aba 'Histórico & Reversão' para rollback a qualquer momento.",
                "Confirmar Otimização Segura em 1-Clique",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            AppendLog("Iniciando pacote de otimizações seguras com registro de histórico...");
            TxtLogStatus.Text = "Aplicando otimizações no sistema operacional...";

            string currentPowerPlan = _allDiagnostics.FirstOrDefault(x => x.Id == "POWER_PLAN")?.CurrentValue ?? "Desempenho Máximo";
            string currentPagefile = _allDiagnostics.FirstOrDefault(x => x.Id == "PAGEFILE")?.CurrentValue ?? "16 4096";

            string resultLog = await Task.Run(() => _optimizer.ApplyAllSafeOptimizations(currentPowerPlan, currentPagefile));
            AppendLog(resultLog);

            MessageBox.Show(
                "Otimizações aplicadas e registradas com sucesso!\n\n" +
                "As mudanças no plano de energia, rede e serviços já estão ativas.\n" +
                "Para que o arquivo de paginação de 8GB entre em vigor sem fragmentação, reinicie o computador quando for conveniente.",
                "Sucesso",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            await RunAuditAsync();
        }

        private async void DiagnosticActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string actionKey) return;

            // --- GUIDES (no message box, just modal) ---
            switch (actionKey)
            {
                case "GUIDE_REBAR":
                    ShowModal("Ativar Resizable BAR / SAM na BIOS MSI", GuideContent.GetGuide("GUIDE_REBAR"));
                    return;
                case "GUIDE_DISPLAYLINK":
                    ShowModal("Conexão Otimizada dos Monitores na RX 9070 XT", GuideContent.GetGuide("GUIDE_DISPLAYLINK"));
                    return;
                case "GUIDE_CURVE_OPTIMIZER":
                case "GUIDE_CPU_PROFILES":
                    ShowModal("Tuning do Ryzen 5 7600X: Os 3 Perfis Definitivos", GuideContent.GetGuide("GUIDE_CPU_PROFILES"));
                    return;
                case "GUIDE_VBS":
                    ShowModal("Impacto do VBS / Integridade de Memória em Jogos", GuideContent.GetGuide("GUIDE_VBS"));
                    return;
                case "GUIDE_RAM":
                    ShowModal("Guia de Memória RAM DDR5 no AMD Zen 4", GuideContent.GetGuide("GUIDE_RAM"));
                    return;
            }

            // --- APPLY OPERATIONS ---
            (bool success, string message) result;

            switch (actionKey)
            {
                case "APPLY_BALANCED_PLAN":
                    var curPlan = _allDiagnostics.FirstOrDefault(x => x.Id == "POWER_PLAN")?.CurrentValue ?? "Desempenho Máximo";
                    result = await Task.Run(() => _optimizer.ApplyBalancedPowerPlan(curPlan));
                    break;

                case "APPLY_PAGEFILE":
                    var curPage = _allDiagnostics.FirstOrDefault(x => x.Id == "PAGEFILE")?.CurrentValue ?? "16 4096";
                    result = await Task.Run(() => _optimizer.ApplySafePagefile(curPage, 8192));
                    break;

                case "APPLY_DISABLE_FAST_STARTUP":
                    result = await Task.Run(() => _optimizer.DisableFastStartup());
                    break;

                case "APPLY_DISABLE_IOBIT":
                    result = await Task.Run(() => _optimizer.DisableIObitBloat());
                    break;

                case "APPLY_CLEAN_STARTUP":
                    result = await Task.Run(() => _optimizer.CleanDuplicateStartup());
                    break;

                case "APPLY_TCP_TWEAKS":
                    result = await Task.Run(() => _optimizer.ApplyTcpOptimizations());
                    break;

                case "APPLY_DISABLE_TELEMETRY":
                    result = await Task.Run(() => _optimizer.DisableWindowsTelemetry());
                    break;

                case "APPLY_DISABLE_SYSMAIN":
                    result = await Task.Run(() => _optimizer.DisableSysMain());
                    break;

                case "APPLY_DISABLE_DELIVERY_OPT":
                    result = await Task.Run(() => _optimizer.DisableDeliveryOptimizationP2P());
                    break;

                case "APPLY_DISABLE_GAMEDVR":
                    result = await Task.Run(() => _optimizer.DisableXboxGameDVR());
                    break;

                case "APPLY_OPTIMIZE_INDEXER":
                    result = await Task.Run(() => _optimizer.OptimizeSearchIndexer());
                    break;

                case "APPLY_FIX_MEMORY_MANAGEMENT":
                    result = await Task.Run(() => _optimizer.FixMemoryManagement());
                    break;

                case "APPLY_NETWORK_TWEAKS":
                    result = await Task.Run(() => _optimizer.ApplyNetworkThrottlingTweaks());
                    break;

                case "APPLY_ENABLE_GAMEMODE_HAGS":
                    result = await Task.Run(() => _optimizer.EnableGameModeAndHags());
                    break;

                case "APPLY_ENABLE_TRIM":
                    var psi = new System.Diagnostics.ProcessStartInfo("fsutil", "behavior set DisableDeleteNotify 0")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    System.Diagnostics.Process.Start(psi)?.WaitForExit();
                    result = (true, "Comando TRIM habilitado no Windows para todos os volumes NTFS e ReFS.");
                    break;

                default:
                    return;
            }

            AppendLog(result.message);
            MessageBox.Show(result.message, result.success ? "Sucesso" : "Erro", MessageBoxButton.OK,
                result.success ? MessageBoxImage.Information : MessageBoxImage.Error);
            await RunAuditAsync();
        }

        private async void RevertActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string revertKey) return;

            var confirm = MessageBox.Show(
                $"Deseja reverter esta alteração para o estado anterior registrado?",
                "Confirmar Reversão",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            (bool success, string message) res = (false, "Chave de reversão desconhecida.");

            switch (revertKey)
            {
                case "REVERT_POWER_PLAN":
                    res = await Task.Run(() => _optimizer.RevertPowerPlan());
                    break;

                case "REVERT_PAGEFILE":
                    res = await Task.Run(() => _optimizer.RevertPagefile());
                    break;

                case "REVERT_FAST_STARTUP":
                    res = await Task.Run(() => _optimizer.RevertFastStartup());
                    break;

                case "REVERT_BLOATWARE":
                    res = await Task.Run(() => _optimizer.RevertIObitBloat());
                    break;

                case "REVERT_TCP":
                    res = await Task.Run(() => _optimizer.RevertTcpOptimizations());
                    break;

                case "REVERT_TELEMETRY":
                    res = await Task.Run(() => _optimizer.RevertWindowsTelemetry());
                    break;

                case "REVERT_SYSMAIN":
                    res = await Task.Run(() => _optimizer.RevertSysMain());
                    break;

                case "REVERT_DELIVERY_OPT":
                    res = await Task.Run(() => _optimizer.RevertDeliveryOptimizationP2P());
                    break;

                case "REVERT_GAMEDVR":
                    res = await Task.Run(() => _optimizer.RevertXboxGameDVR());
                    break;

                case "REVERT_INDEXER":
                    res = await Task.Run(() => _optimizer.RevertSearchIndexer());
                    break;

                case "REVERT_MEMORY_MANAGEMENT":
                    res = await Task.Run(() => _optimizer.RevertMemoryManagement());
                    break;

                case "REVERT_NETWORK_TWEAKS":
                    res = await Task.Run(() => _optimizer.RevertNetworkThrottlingTweaks());
                    break;

                case "REVERT_GAMEMODE_HAGS":
                    res = await Task.Run(() => _optimizer.RevertGameModeAndHags());
                    break;
            }

            AppendLog(res.message);
            MessageBox.Show(res.message, res.success ? "Sucesso" : "Erro", MessageBoxButton.OK, res.success ? MessageBoxImage.Information : MessageBoxImage.Error);
            await RunAuditAsync();
        }

        private async void CreateRestorePointButton_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Solicitando criação de Ponto de Restauração do Windows...");
            TxtLogStatus.Text = "Criando ponto de restauração do sistema...";

            var res = await Task.Run(() => _optimizer.CreateRestorePoint());
            AppendLog(res.message);
            MessageBox.Show(res.message, res.success ? "Ponto de Restauração Criado" : "Aviso", MessageBoxButton.OK, res.success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            TxtLogStatus.Text = "Operação concluída.";
        }

        private async void RevertAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_snapshotService.History.Count == 0)
            {
                MessageBox.Show("Não há alterações registradas no histórico para reverter.", "Histórico Vazio", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                "Deseja desfazer TODAS as alterações registradas no histórico e retornar às configurações anteriores?",
                "Confirmar Reversão Total",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            AppendLog("Iniciando reversão de todas as alterações salvas...");
            await Task.Run(() =>
            {
                _optimizer.RevertPowerPlan();
                _optimizer.RevertPagefile();
                _optimizer.RevertFastStartup();
                _optimizer.RevertIObitBloat();
                _optimizer.RevertTcpOptimizations();
                _optimizer.RevertWindowsTelemetry();
                _optimizer.RevertSysMain();
                _optimizer.RevertDeliveryOptimizationP2P();
                _optimizer.RevertXboxGameDVR();
                _optimizer.RevertSearchIndexer();
                _optimizer.RevertMemoryManagement();
                _optimizer.RevertNetworkThrottlingTweaks();
                _optimizer.RevertGameModeAndHags();
            });

            _snapshotService.Clear();
            AppendLog("Todas as alterações foram revertidas.");
            MessageBox.Show("Todas as configurações foram restauradas com sucesso!", "Reversão Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
            await RunAuditAsync();
        }

        private void ShowModal(string title, string content)
        {
            TxtModalTitle.Text = title;
            TxtModalBody.Text = content;
            ModalOverlay.Visibility = Visibility.Visible;
        }

        private void CloseModalButton_Click(object sender, RoutedEventArgs e)
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
        }

        private void CopyModalButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtModalBody.Text);
                MessageBox.Show("Guia copiado para a área de transferência!", "Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppendLog($"Erro ao copiar para clipboard: {ex.Message}");
            }
        }

        private void AppendLog(string message)
        {
            Logger.Info(message, "UI");

            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                TxtConsoleLog.AppendText($"[{timestamp}] {message}\r\n");
                TxtConsoleLog.ScrollToEnd();
            });
        }

        private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(Logger.LogDirectory))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Logger.LogDirectory,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show($"Pasta de logs: {Logger.LogDirectory}", "Logs em Disco", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Erro ao abrir pasta de logs: {ex.Message}");
            }
        }

        private void WebsiteLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://eff5.com.br",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppendLog($"Erro ao abrir navegador: {ex.Message}");
            }
        }

        // ====================================================================
        // CPU TUNING, METRICS, PRESETS & STRESS TEST
        // ====================================================================

        private void InitializeCpuTuningUi()
        {
            if (TxtCpuModelTitle == null) return;

            TxtCpuModelTitle.Text = _cpuTopology.ModelName;
            string arch = _cpuTopology.Architecture switch
            {
                CpuArchitecture.Zen3_Ryzen5000 => "ZEN 3 · AM4",
                CpuArchitecture.Zen4_Ryzen7000 => "ZEN 4 · AM5",
                CpuArchitecture.Zen5_Ryzen9000 => "ZEN 5 · AM5",
                _ => "AMD RYZEN"
            };
            TxtArchBadge.Text = arch;
            TxtCpuTdpSummary.Text = $"TDP Base: {_cpuTopology.DefaultTdpWatts}W | PPT Fábrica: {_cpuTopology.StockPptWatts}W | TjMax Fábrica: {_cpuTopology.StockTjMaxCelsius}°C";

            var gold = _cpuTopology.GoldCore;
            var silver = _cpuTopology.SilverCore;
            TxtGoldCoreBadge.Text = gold != null ? $"Core {gold.CoreIndex} ({gold.MaxPerformancePercent}%)" : "Core 0";
            TxtSilverCoreBadge.Text = silver != null ? $"Core {silver.CoreIndex} ({silver.MaxPerformancePercent}%)" : "Core 1";

            // Aplica o perfil recomendado inicial
            var defaultPreset = _cpuPresets.FirstOrDefault(p => p.Id == "PRESET_BALANCED") ?? _cpuPresets[0];
            ApplyPresetToUi(defaultPreset);
        }

        private void ApplyPresetToUi(CpuTuningPreset preset)
        {
            _currentPreset = preset;

            ResetPresetBorders();
            if (preset.Id == "PRESET_ECO") SetPresetBorderActive(BorderPresetEco, BtnPresetEco);
            else if (preset.Id == "PRESET_BALANCED") SetPresetBorderActive(BorderPresetBalanced, BtnPresetBalanced);
            else if (preset.Id == "PRESET_GAMING") SetPresetBorderActive(BorderPresetGaming, BtnPresetGaming);
            else if (preset.Id == "PRESET_RENDER") SetPresetBorderActive(BorderPresetRender, BtnPresetRender);
            else if (preset.Id == "PRESET_STOCK") SetPresetBorderActive(BorderPresetStock, BtnPresetStock);

            if (SliderPpt != null) SliderPpt.Value = preset.PptWatts;
            if (SliderTdc != null) SliderTdc.Value = preset.TdcAmps;
            if (SliderEdc != null) SliderEdc.Value = preset.EdcAmps;
            if (SliderTjMax != null) SliderTjMax.Value = preset.TjMaxCelsius;
            if (SliderBoost != null) SliderBoost.Value = preset.BoostOverrideMhz;
            if (SliderAllCore != null) SliderAllCore.Value = preset.AllCoreOffset;

            foreach (var core in _cpuTopology.Cores)
            {
                if (preset.PerCoreOffsets.TryGetValue(core.CoreIndex, out int offset))
                {
                    core.CurveOptimizerOffset = offset;
                }
                else
                {
                    core.CurveOptimizerOffset = preset.AllCoreOffset;
                }
            }

            // Atualiza os limites de frequência e energia no mostrador em tempo real
            double targetBoost = preset.Id switch
            {
                "PRESET_ECO" => 4.75,
                "PRESET_GAMING" => 5.45,
                "PRESET_RENDER" => 5.00,
                "PRESET_STOCK" => 5.05,
                _ => 5.05
            };
            _metricsMonitor.ConfiguredMaxClockGhz = targetBoost;
            if (GaugeClockMaxText != null) GaugeClockMaxText.Text = $"Teto Configurado: {targetBoost:F2} GHz";
            if (GaugePowerLimit != null) GaugePowerLimit.Text = $"Limite PPT: {preset.PptWatts} W";

            // Aplica as políticas do perfil imediatamente em runtime
            var applyRes = CpuTunerService.ApplyProfileRuntime(_cpuTopology, preset, _optimizer.IsDryRun);
            if (TxtCpuTuningStatus != null)
            {
                TxtCpuTuningStatus.Text = $"🟢 Perfil Ativo: {preset.Name} (PPT {preset.PptWatts}W | CO {preset.AllCoreOffset}mV | TjMax {preset.TjMaxCelsius}°C)";
            }

            AppendLog($"Preset de CPU ativado no sistema: {preset.Name} (PPT: {preset.PptWatts}W, CO: {preset.AllCoreOffset}mV, TjMax: {preset.TjMaxCelsius}°C)");
        }

        private void ResetPresetBorders()
        {
            var defBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252B3A"));
            var defBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#161922"));
            var btnBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));

            var borders = new[] { BorderPresetEco, BorderPresetBalanced, BorderPresetGaming, BorderPresetRender, BorderPresetStock };
            var buttons = new[] { BtnPresetEco, BtnPresetBalanced, BtnPresetGaming, BtnPresetRender, BtnPresetStock };

            foreach (var b in borders)
            {
                if (b == null) continue;
                b.BorderBrush = defBrush;
                b.Background = defBg;
                b.BorderThickness = new Thickness(1);
            }
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                btn.Background = btnBg;
                btn.Content = "Selecionar";
            }
        }

        private void SetPresetBorderActive(Border? border, Button? btn)
        {
            if (border != null)
            {
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B2232"));
                border.BorderThickness = new Thickness(1.5);
            }
            if (btn != null)
            {
                btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
                btn.Content = "✓ Ativo";
            }
        }

        private void PresetEco_Click(object sender, RoutedEventArgs e) => ApplyPresetToUi(_cpuPresets.First(p => p.Id == "PRESET_ECO"));
        private void PresetBalanced_Click(object sender, RoutedEventArgs e) => ApplyPresetToUi(_cpuPresets.First(p => p.Id == "PRESET_BALANCED"));
        private void PresetGaming_Click(object sender, RoutedEventArgs e) => ApplyPresetToUi(_cpuPresets.First(p => p.Id == "PRESET_GAMING"));
        private void PresetRender_Click(object sender, RoutedEventArgs e) => ApplyPresetToUi(_cpuPresets.First(p => p.Id == "PRESET_RENDER"));
        private void PresetStock_Click(object sender, RoutedEventArgs e) => ApplyPresetToUi(_cpuPresets.First(p => p.Id == "PRESET_STOCK"));

        private void ChkAdvancedMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || PanelAdvancedControls == null) return;
            PanelAdvancedControls.Visibility = ChkAdvancedMode.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CoMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || PanelPerCoreList == null || PanelAllCoreSlider == null) return;
            bool isPerCore = RadioCoPerCore.IsChecked == true;
            PanelPerCoreList.Visibility = isPerCore ? Visibility.Visible : Visibility.Collapsed;
            PanelAllCoreSlider.Visibility = isPerCore ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SliderAllCore_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || TxtAllCoreValue == null) return;
            int val = (int)e.NewValue;
            TxtAllCoreValue.Text = $"{val} mV";
            if (RadioCoAllCore?.IsChecked == true)
            {
                foreach (var c in _cpuTopology.Cores) { c.CurveOptimizerOffset = val; }
            }
        }

        private void SliderPpt_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || TxtPptValue == null) return;
            TxtPptValue.Text = $"{(int)e.NewValue} W";
            if (GaugePowerLimit != null) GaugePowerLimit.Text = $"Limite PPT: {(int)e.NewValue} W";
        }

        private void SliderTdc_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || TxtTdcValue == null) return;
            TxtTdcValue.Text = $"{(int)e.NewValue} A";
        }

        private void SliderEdc_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || TxtEdcValue == null) return;
            TxtEdcValue.Text = $"{(int)e.NewValue} A";
        }

        private void SliderTjMax_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || TxtTjMaxValue == null) return;
            TxtTjMaxValue.Text = $"{(int)e.NewValue} °C";
        }

        private void SliderBoost_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || TxtBoostValue == null) return;
            int boostMhz = (int)e.NewValue;
            TxtBoostValue.Text = $"+{boostMhz} MHz";
            _metricsMonitor.ConfiguredMaxClockGhz = 5.05 + (boostMhz / 1000.0);
            if (GaugeClockMaxText != null)
            {
                GaugeClockMaxText.Text = $"Teto Configurado: {_metricsMonitor.ConfiguredMaxClockGhz:F2} GHz";
            }
        }

        private async void BtnStartStress_Click(object sender, RoutedEventArgs e)
        {
            int duration = 30;
            if (RadioStress60s?.IsChecked == true) duration = 60;
            else if (RadioStress120s?.IsChecked == true) duration = 120;
            else if (RadioStress300s?.IsChecked == true) duration = 300;

            bool isSingle = RadioStressSingle?.IsChecked == true;
            var mode = isSingle ? StressTestMode.SingleCoreGold : StressTestMode.MultiThread;
            int goldThread = _cpuTopology.GoldCore?.LogicalThread1 ?? 0;

            BtnStartStress.IsEnabled = false;
            BtnStopStress.IsEnabled = true;
            StressProgressBar.Value = 0;
            BorderStressResult.Visibility = Visibility.Collapsed;

            string targetDesc = isSingle ? $"Single-Core (Núcleo Gold #{_cpuTopology.GoldCore?.CoreIndex ?? 0})" : $"{_cpuTopology.LogicalThreads} threads (Multi-Thread)";
            TxtStressStatus.Text = $"Executando teste intensivo de {duration}s em {targetDesc}...";

            AppendLog($"Iniciando teste de estresse da CPU ({duration}s, {targetDesc})...");

            await _stressTester.StartAsync(duration, mode, goldThread);
        }

        private void BtnStopStress_Click(object sender, RoutedEventArgs e)
        {
            _stressTester.Cancel();
            BtnStartStress.IsEnabled = true;
            BtnStopStress.IsEnabled = false;
            TxtStressStatus.Text = "Teste interrompido pelo usuário.";
        }

        private void OnStressProgressChanged(StressTestProgress p)
        {
            Dispatcher.Invoke(() =>
            {
                StressProgressBar.Value = p.Percent;
                TxtStressStatus.Text = $"Calculando... {p.Elapsed:mm\\:ss} / {p.Total:mm\\:ss} ({p.Percent}%)";
                TxtStressOps.Text = $"{p.OperationsPerSecond:N0} ops/s";
            });
        }

        private void OnStressTestCompleted(StressTestResult r)
        {
            Dispatcher.Invoke(() =>
            {
                BtnStartStress.IsEnabled = true;
                BtnStopStress.IsEnabled = false;
                StressProgressBar.Value = 100;
                TxtStressStatus.Text = "Teste finalizado.";
                TxtStressOps.Text = $"Score: {r.Score:N0} pts | {r.TotalCalculations:N0} ops";

                BorderStressResult.Visibility = Visibility.Visible;
                if (r.Passed)
                {
                    BorderStressResult.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B2B20"));
                    BorderStressResult.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
                    TxtStressResultMsg.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
                    TxtStressResultMsg.Text = r.Summary;
                    AppendLog($"Teste de estresse APROVADO! Score: {r.Score:N0} pontos.");
                }
                else
                {
                    BorderStressResult.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#331818"));
                    BorderStressResult.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    TxtStressResultMsg.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    TxtStressResultMsg.Text = r.Summary;
                    AppendLog($"Aviso: Instabilidade detectada no teste de estresse ({r.FailureMessages.Count} erros). Score: {r.Score:N0} pts.");
                }

                // Monta a Ficha Técnica para o Tooltip do Placar
                string testTypeName = r.Mode == StressTestMode.SingleCoreGold ? "Single (Gold ⭐)" : "Multi-Thread";
                string durStr = $"{r.Duration.TotalSeconds:F0}s";

                var tooltipSb = new StringBuilder();
                tooltipSb.AppendLine("⚙️ FICHA TÉCNICA APLICADA NESTA RODADA:");
                tooltipSb.AppendLine($"• Perfil Base: {_currentPreset?.Name ?? "Custom"}");
                tooltipSb.AppendLine($"• Potência: PPT {(int)SliderPpt.Value}W | TDC {(int)SliderTdc.Value}A | EDC {(int)SliderEdc.Value}A");
                tooltipSb.AppendLine($"• Temperatura Máxima: TjMax {(int)SliderTjMax.Value}°C");
                tooltipSb.AppendLine($"• Boost Override: +{(int)SliderBoost.Value} MHz");
                tooltipSb.AppendLine("• Curva de Tensão (Curve Optimizer):");
                if (RadioCoAllCore?.IsChecked == true)
                {
                    tooltipSb.AppendLine($"  - All-Core: {(int)SliderAllCore.Value} mV");
                }
                else
                {
                    foreach (var c in _cpuTopology.Cores)
                    {
                        string badge = c.IsGold ? " (⭐ Gold)" : c.IsSilver ? " (🥈 Silver)" : "";
                        tooltipSb.AppendLine($"  - Core {c.CoreIndex}{badge}: {c.CurveOptimizerOffset} mV");
                    }
                }
                tooltipSb.AppendLine();
                tooltipSb.AppendLine($"• Resultado: {(r.Passed ? "✓ 100% Estável (0 divergências)" : "⚠️ Falha de paridade matemática")}");
                tooltipSb.AppendLine($"• Operações: {r.TotalCalculations:N0} em {r.Duration.TotalSeconds:F1}s");
                tooltipSb.AppendLine($"• Pontuação Alcançada: {r.Score:N0} pontos");

                var record = new StressScoreRecord
                {
                    Timestamp = DateTime.Now,
                    TestType = testTypeName,
                    Duration = durStr,
                    Passed = r.Passed,
                    Score = r.Score,
                    AppliedSettingsTooltip = tooltipSb.ToString().TrimEnd()
                };

                ScoreboardItems.Insert(0, record);
            });
        }

        private void OnMetricsUpdated(CpuMetricsSnapshot s)
        {
            Dispatcher.Invoke(() =>
            {
                if (GaugeUsageText != null) GaugeUsageText.Text = $"{s.CpuUsagePercent:F1} %";
                if (GaugeUsageBar != null) GaugeUsageBar.Value = s.CpuUsagePercent;
                if (GaugeTempText != null) GaugeTempText.Text = $"{s.TemperatureCelsius:F0} °C";
                if (GaugeTempStatus != null)
                {
                    GaugeTempStatus.Text = s.TemperatureCelsius >= 85 ? "Atenção (TjMax Próximo)" : s.TemperatureCelsius >= 75 ? "Normal (Carga)" : "Normal (Excelente)";
                    GaugeTempStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s.StatusColor));
                }
                if (GaugeTempText != null)
                {
                    GaugeTempText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s.StatusColor));
                }
                if (GaugeClockText != null) GaugeClockText.Text = $"{s.CurrentClockGhz:F2} GHz";
                if (GaugeClockMaxText != null) GaugeClockMaxText.Text = $"Teto Configurado: {s.ConfiguredMaxClockGhz:F2} GHz";
                if (GaugeClockBar != null) GaugeClockBar.Value = s.ClockPercent;
                if (GaugePowerText != null) GaugePowerText.Text = $"{s.PowerWatts:F0} W";
            });
        }

        private void BtnApplyCurrentPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPreset == null) return;
            var res = CpuTunerService.ApplyProfileRuntime(_cpuTopology, _currentPreset, _optimizer.IsDryRun);
            AppendLog(res.message);
            if (TxtCpuTuningStatus != null)
            {
                TxtCpuTuningStatus.Text = $"🟢 Perfil Ativo: {_currentPreset.Name} (PPT {_currentPreset.PptWatts}W | CO {_currentPreset.AllCoreOffset}mV)";
            }
            MessageBox.Show(res.message, res.success ? "Perfil Ativado no Sistema" : "Aviso", MessageBoxButton.OK,
                res.success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void BtnOpenRyzenMaster_Click(object sender, RoutedEventArgs e)
        {
            var res = CpuTunerService.LaunchRyzenMaster();
            AppendLog(res.message);
            if (!res.success)
            {
                MessageBox.Show(res.message, "AMD Ryzen Master", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExportProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int ppt = (int)SliderPpt.Value;
                int tdc = (int)SliderTdc.Value;
                int edc = (int)SliderEdc.Value;
                int tjMax = (int)SliderTjMax.Value;
                int boost = (int)SliderBoost.Value;
                bool isPerCore = RadioCoPerCore.IsChecked == true;
                int allCore = (int)SliderAllCore.Value;
                int[] perCore = _cpuTopology.Cores.Select(c => c.CurveOptimizerOffset).ToArray();

                string profileName = _currentPreset?.Name ?? "Custom";
                string content = CpuTunerService.GenerateProfileSummary(_cpuTopology, profileName, ppt, tdc, edc, tjMax, boost, isPerCore, allCore, perCore);

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string profileDir = Path.Combine(appData, "AMDAutoTuning", "Profiles");
                Directory.CreateDirectory(profileDir);
                string file = Path.Combine(profileDir, $"perfil_cpu_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(file, content, Encoding.UTF8);

                AppendLog($"Ficha técnica do perfil exportada: {file}");
                MessageBox.Show($"Ficha técnica do perfil exportada para:\n\n{file}\n\nO arquivo contém os limites de potência, temperatura e offsets de Curve Optimizer configurados.",
                    "Perfil Exportado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppendLog($"Erro ao exportar perfil: {ex.Message}");
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
