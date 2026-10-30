using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AMDAutoTuning.Models;
using AMDAutoTuning.Services;

namespace AMDAutoTuning
{
    public partial class MainWindow : Window
    {
        private readonly HardwareScanner _scanner = new();
        private readonly OptimizerService _optimizer = new();
        private HardwareOverview _overview = new();

        public ObservableCollection<DiagnosticItem> Diagnostics { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _overview;
            ItemsDiagnosticList.ItemsSource = Diagnostics;

            Loaded += async (s, e) => await RunAuditAsync();
        }

        private async Task RunAuditAsync()
        {
            AppendLog("Iniciando auditoria de hardware e subsistemas...");
            TxtLogStatus.Text = "Analisando processador, GPU, memória e configurações do Windows...";

            try
            {
                var overviewTask = Task.Run(() => _scanner.ScanOverview());
                var diagnosticsTask = Task.Run(() => _scanner.ScanDiagnostics());

                await Task.WhenAll(overviewTask, diagnosticsTask);

                _overview = overviewTask.Result;
                DataContext = _overview;

                Diagnostics.Clear();
                foreach (var item in diagnosticsTask.Result)
                {
                    Diagnostics.Add(item);
                }

                AppendLog($"Auditoria concluída com sucesso. {Diagnostics.Count} verificações realizadas.");
                TxtLogStatus.Text = "Auditoria concluída. Analise os pontos críticos e aplique as melhorias.";
            }
            catch (Exception ex)
            {
                AppendLog($"Erro durante a auditoria: {ex.Message}");
                TxtLogStatus.Text = "Erro ao executar auditoria.";
            }
        }

        private async void RescanButton_Click(object sender, RoutedEventArgs e)
        {
            await RunAuditAsync();
        }

        private async void OptimizeAllButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Deseja aplicar todas as otimizações seguras recomendadas pelo aplicativo?\n\n" +
                "• Plano de Energia Equilibrado (Otimizado para AMD CPPC e boost Zen 4)\n" +
                "• Fixação do Arquivo de Paginação em 8 GB contínuo no SSD NVMe\n" +
                "• Desativação de serviços e processos em segundo plano do IObit\n" +
                "• Otimização da pilha de rede TCP (menor latência de pacotes)\n\n" +
                "Nenhuma alteração de hardware ou overclock arriscado é feito nesta etapa.",
                "Confirmar Otimização Segura em 1-Clique",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            AppendLog("Iniciando pacote de otimizações seguras...");
            TxtLogStatus.Text = "Aplicando otimizações no sistema operacional...";

            string resultLog = await Task.Run(() => _optimizer.ApplyAllSafeOptimizations());
            AppendLog(resultLog);

            MessageBox.Show(
                "Otimizações aplicadas com sucesso!\n\n" +
                "O plano de energia e as configurações de rede já estão ativas.\n" +
                "Para a memória virtual de 8GB ter efeito total sem fragmentação, reinicie o computador quando for conveniente.",
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
                    var rPower = await Task.Run(() => _optimizer.ApplyBalancedPowerPlan());
                    AppendLog(rPower.message);
                    MessageBox.Show(rPower.message, rPower.success ? "Sucesso" : "Erro", MessageBoxButton.OK, rPower.success ? MessageBoxImage.Information : MessageBoxImage.Error);
                    await RunAuditAsync();
                    break;

                case "APPLY_PAGEFILE":
                    var rPage = await Task.Run(() => _optimizer.ApplySafePagefile(8192));
                    AppendLog(rPage.message);
                    MessageBox.Show(rPage.message, rPage.success ? "Sucesso" : "Erro", MessageBoxButton.OK, rPage.success ? MessageBoxImage.Information : MessageBoxImage.Error);
                    await RunAuditAsync();
                    break;

                case "APPLY_DISABLE_IOBIT":
                    var rBloat = await Task.Run(() => _optimizer.DisableIObitBloat());
                    AppendLog(rBloat.message);
                    MessageBox.Show(rBloat.message, rBloat.success ? "Sucesso" : "Erro", MessageBoxButton.OK, rBloat.success ? MessageBoxImage.Information : MessageBoxImage.Error);
                    await RunAuditAsync();
                    break;

                case "APPLY_TCP_TWEAKS":
                    var rTcp = await Task.Run(() => _optimizer.ApplyTcpOptimizations());
                    AppendLog(rTcp.message);
                    MessageBox.Show(rTcp.message, rTcp.success ? "Sucesso" : "Erro", MessageBoxButton.OK, rTcp.success ? MessageBoxImage.Information : MessageBoxImage.Error);
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
            }
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

        // Titlebar Controls
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
