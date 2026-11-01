using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AMDAutoTuning.Services;

namespace AMDAutoTuning
{
    public partial class App : Application
    {
        private const string PublisherMarker = "EFF5";

        /// <summary>
        /// Quando ativado via CLI (--dry-run ou --simulate), os tweaks simulam a execução
        /// e gravam o que fariam no log sem alterar o registro ou parar serviços do Windows.
        /// </summary>
        public static bool IsDryRun { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Detecta modo simulação
            IsDryRun = e.Args.Any(a => a.Equals("--dry-run", StringComparison.OrdinalIgnoreCase) ||
                                       a.Equals("--simulate", StringComparison.OrdinalIgnoreCase));

            // Configura telemetria e Crash Guard global
            SetupExceptionHandling();

            string ver = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
            Logger.Info($"=== AMD AutoTuning v{ver} Iniciado === (PID: {Environment.ProcessId}, DryRun: {IsDryRun})", "BOOT");
            Logger.Info($"Ambiente: {Environment.OSVersion} | 64-bit OS: {Environment.Is64BitOperatingSystem}", "BOOT");

            EnsurePublisherCertificateTrusted();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Info($"AMD AutoTuning finalizado com código de saída {e.ApplicationExitCode}.", "SHUTDOWN");
            base.OnExit(e);
        }

        private void SetupExceptionHandling()
        {
            // Captura erros na thread principal da interface WPF
            DispatcherUnhandledException += (s, args) =>
            {
                Logger.Fatal("Erro não tratado na thread da UI (WPF)", args.Exception);
                MessageBox.Show(
                    $"Ocorreu um erro inesperado na interface:\n\n{args.Exception.Message}\n\n" +
                    $"Um relatório de diagnóstico foi gravado em:\n{Logger.LogDirectory}",
                    "AMD AutoTuning - Falha de Execução",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true; // Impede encerramento abrupto se recuperável
            };

            // Captura erros em threads de fundo / TaskPool
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    Logger.Fatal("Erro fatal não tratado no AppDomain", ex);
                }
            };

            // Captura falhas em Tasks não aguardadas
            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                Logger.Error("Exceção não observada em Task assíncrona", args.Exception);
                args.SetObserved();
            };
        }

        /// <summary>
        /// O UAC (consent.exe) roda como NT AUTHORITY\SYSTEM e valida assinaturas
        /// EXCLUSIVAMENTE contra Cert:\LocalMachine\*. Como o app ja inicia elevado
        /// (requireAdministrator), registramos o proprio certificado de assinatura
        /// nas lojas da maquina e removemos entradas antigas/obsoletas da EFF5.
        /// </summary>
        private static void EnsurePublisherCertificateTrusted()
        {
            try
            {
                string? currentExe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe)) return;

#pragma warning disable SYSLIB0057
                var rawCert = X509Certificate.CreateFromSignedFile(currentExe);
#pragma warning restore SYSLIB0057

                if (rawCert == null) return;

                using var signerCert = new X509Certificate2(rawCert);
                if (!signerCert.Subject.Contains(PublisherMarker, StringComparison.OrdinalIgnoreCase)) return;

                string thumbprint = signerCert.Thumbprint;

                SyncStore(StoreName.Root, signerCert, thumbprint);
                SyncStore(StoreName.TrustedPublisher, signerCert, thumbprint);
            }
            catch
            {
                // Nunca impedir a inicializacao do app por causa de trust de certificado.
            }
        }

        private static void SyncStore(StoreName storeName, X509Certificate2 cert, string thumbprint)
        {
            try
            {
                using var store = new X509Store(storeName, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);

                // Remove entradas EFF5 obsoletas (ex.: certificado com encoding corrompido)
                var stale = new System.Collections.Generic.List<X509Certificate2>();
                foreach (var existing in store.Certificates)
                {
                    if (existing.Subject.Contains(PublisherMarker, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(existing.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase))
                    {
                        stale.Add(existing);
                    }
                }

                foreach (var old in stale)
                {
                    try { store.Remove(old); } catch { }
                    old.Dispose();
                }

                // Adiciona o certificado atual se ainda nao estiver presente
                var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                if (found.Count == 0)
                {
                    store.Add(cert);
                }
            }
            catch
            {
                // Sem elevacao ou loja bloqueada: ignora silenciosamente.
            }
        }
    }
}
