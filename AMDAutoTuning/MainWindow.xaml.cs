using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

        private HardwareOverview _overview = new();
        private List<DiagnosticItem> _allDiagnostics = new();
        private string _activeTabKey = "DASHBOARD";

        public ObservableCollection<DiagnosticItem> DisplayedDiagnostics { get; } = new();
        public ObservableCollection<TweakSnapshot> DisplayedHistory { get; } = new();

        public MainWindow()
        {
            InitializeComponent();

            _snapshotService = new SnapshotService();
            _scanner = new HardwareScanner(_snapshotService);
            _optimizer = new OptimizerService(_snapshotService);

            DataContext = _overview;
            ItemsDiagnosticList.ItemsSource = DisplayedDiagnostics;
            ItemsHistoryList.ItemsSource = DisplayedHistory;

            Loaded += async (s, e) => await RunAuditAsync();
        }

        private async Task RunAuditAsync()
        {
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
            if (sender is not RadioButton rb) return;

            if (rb == TabDashboard) _activeTabKey = "DASHBOARD";
            else if (rb == TabOs) _activeTabKey = "OS";
            else if (rb == TabCpuRam) _activeTabKey = "CPU_RAM";
            else if (rb == TabGpuDisplays) _activeTabKey = "GPU_DISPLAYS";
            else if (rb == TabDriversBios) _activeTabKey = "DRIVERS_BIOS";
            else if (rb == TabHistory) _activeTabKey = "HISTORY";

            RefreshTabFilter();
        }

        private void RefreshTabFilter()
        {
            if (_activeTabKey == "HISTORY")
            {
                ViewDiagnostics.Visibility = Visibility.Collapsed;
                ViewHistory.Visibility = Visibility.Visible;
                RefreshHistoryView();
                return;
            }

            ViewHistory.Visibility = Visibility.Collapsed;
            ViewDiagnostics.Visibility = Visibility.Visible;

            // Overview cards are only shown in Dashboard tab
            PanelHardwareOverview.Visibility = _activeTabKey == "DASHBOARD" ? Visibility.Visible : Visibility.Collapsed;

            DisplayedDiagnostics.Clear();
            IEnumerable<DiagnosticItem> filtered = _activeTabKey switch
            {
                "DASHBOARD" => _allDiagnostics,
                _ => _allDiagnostics.Where(x => x.CategoryKey.Equals(_activeTabKey, StringComparison.OrdinalIgnoreCase))
            };

            foreach (var item in filtered)
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

            switch (actionKey)
            {
                case "APPLY_BALANCED_PLAN":
                    var curPlan = _allDiagnostics.FirstOrDefault(x => x.Id == "POWER_PLAN")?.CurrentValue ?? "Desempenho Máximo";
                    var rPower = await Task.Run(() => _optimizer.ApplyBalancedPowerPlan(curPlan));
                    AppendLog(rPower.message);
                    MessageBox.Show(rPower.message, rPower.success ? "Sucesso" : "Erro", MessageBoxButton.OK, rPower.success ? MessageBoxImage.Information : MessageBoxImage.Error);
                    await RunAuditAsync();
                    break;

                case "APPLY_PAGEFILE":
                    var curPage = _allDiagnostics.FirstOrDefault(x => x.Id == "PAGEFILE")?.CurrentValue ?? "16 4096";
                    var rPage = await Task.Run(() => _optimizer.ApplySafePagefile(curPage, 8192));
                    AppendLog(rPage.message);
                    MessageBox.Show(rPage.message, rPage.success ? "Sucesso" : "Erro", MessageBoxButton.OK, rPage.success ? MessageBoxImage.Information : MessageBoxImage.Error);
                    await RunAuditAsync();
                    break;

                case "APPLY_DISABLE_FAST_STARTUP":
                    var rFast = await Task.Run(() => _optimizer.DisableFastStartup());
                    AppendLog(rFast.message);
                    MessageBox.Show(rFast.message, rFast.success ? "Sucesso" : "Erro", MessageBoxButton.OK, rFast.success ? MessageBoxImage.Information : MessageBoxImage.Error);
                    await RunAuditAsync();
                    break;

                case "APPLY_DISABLE_IOBIT":
                    var rBloat = await Task.Run(() => _optimizer.DisableIObitBloat());
                    AppendLog(rBloat.message);
                    MessageBox.Show(rBloat.message, rBloat.success ? "Sucesso" : "Erro", MessageBoxButton.OK, rBloat.success ? MessageBoxImage.Information : MessageBoxImage.Error);
                    await RunAuditAsync();
                    break;

                case "APPLY_CLEAN_STARTUP":
                    var rClean = await Task.Run(() => _optimizer.CleanDuplicateStartup());
                    AppendLog(rClean.message);
                    MessageBox.Show(rClean.message, rClean.success ? "Sucesso" : "Erro", MessageBoxButton.OK, rClean.success ? MessageBoxImage.Information : MessageBoxImage.Error);
                    await RunAuditAsync();
                    break;

                case "APPLY_TCP_TWEAKS":
                    var rTcp = await Task.Run(() => _optimizer.ApplyTcpOptimizations());
                    AppendLog(rTcp.message);
                    MessageBox.Show(rTcp.message, rTcp.success ? "Sucesso" : "Erro", MessageBoxButton.OK, rTcp.success ? MessageBoxImage.Information : MessageBoxImage.Error);
                    await RunAuditAsync();
                    break;

                case "APPLY_ENABLE_TRIM":
                    var psi = new System.Diagnostics.ProcessStartInfo("fsutil", "behavior set DisableDeleteNotify 0")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    System.Diagnostics.Process.Start(psi)?.WaitForExit();
                    AppendLog("Comando TRIM habilitado no Windows para todos os volumes NTFS e ReFS.");
                    await RunAuditAsync();
                    break;

                case "GUIDE_REBAR":
                    ShowModal("Ativar Resizable BAR / SAM na BIOS MSI", GuideContent.GetGuide("GUIDE_REBAR"));
                    break;

                case "GUIDE_DISPLAYLINK":
                    ShowModal("Conexão Otimizada dos Monitores na RX 9070 XT", GuideContent.GetGuide("GUIDE_DISPLAYLINK"));
                    break;

                case "GUIDE_CURVE_OPTIMIZER":
                    ShowModal("Tuning do Ryzen 5 7600X: Curve Optimizer & Eco Mode", GuideContent.GetGuide("GUIDE_CURVE_OPTIMIZER"));
                    break;

                case "GUIDE_RAM":
                    ShowModal("Guia de Memória RAM DDR5 no AMD Zen 4", GuideContent.GetGuide("GUIDE_RAM"));
                    break;
            }
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
            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                TxtConsoleLog.AppendText($"[{timestamp}] {message}\r\n");
                TxtConsoleLog.ScrollToEnd();
            });
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

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
