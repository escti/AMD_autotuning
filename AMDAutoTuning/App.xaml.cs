using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows;

namespace AMDAutoTuning
{
    public partial class App : Application
    {
        private const string PublisherMarker = "EFF5";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            EnsurePublisherCertificateTrusted();
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
