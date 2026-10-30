using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows;

namespace AMDAutoTuning
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            EnsureSelfSignedCertTrustedInLocalMachine();
        }

        private static void EnsureSelfSignedCertTrustedInLocalMachine()
        {
            try
            {
                string? currentExe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe)) return;

#pragma warning disable SYSLIB0057
                var cert = X509Certificate.CreateFromSignedFile(currentExe);
#pragma warning restore SYSLIB0057
                if (cert == null || !cert.Subject.Contains("EFF5")) return;

                using var storeRoot = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                storeRoot.Open(OpenFlags.ReadOnly);
                bool hasInRoot = storeRoot.Certificates.Find(X509FindType.FindByThumbprint, cert.GetCertHashString(), false).Count > 0;
                storeRoot.Close();

                if (!hasInRoot)
                {
                    string tempCer = Path.Combine(Path.GetTempPath(), "eff5_trusted.cer");
                    File.WriteAllBytes(tempCer, cert.Export(X509ContentType.Cert));

                    // When running as admin (requireAdministrator), certutil can write to LocalMachine
                    var psi1 = new ProcessStartInfo("certutil.exe", $"-addstore -f Root \"{tempCer}\"") { CreateNoWindow = true, UseShellExecute = false };
                    Process.Start(psi1)?.WaitForExit(5000);

                    var psi2 = new ProcessStartInfo("certutil.exe", $"-addstore -f TrustedPublisher \"{tempCer}\"") { CreateNoWindow = true, UseShellExecute = false };
                    Process.Start(psi2)?.WaitForExit(5000);

                    File.Delete(tempCer);
                }
            }
            catch { }
        }
    }
}
