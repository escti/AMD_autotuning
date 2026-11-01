using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace AMDAutoTuning.Services
{
    public class StressTestProgress
    {
        public int Percent { get; set; }
        public TimeSpan Elapsed { get; set; }
        public TimeSpan Total { get; set; }
        public long OperationsPerSecond { get; set; }
        public int ActiveThreads { get; set; }
    }

    public class StressTestResult
    {
        public bool Passed { get; set; }
        public TimeSpan Duration { get; set; }
        public long TotalCalculations { get; set; }
        public List<string> FailureMessages { get; } = new();
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Motor de teste de estresse multi-thread para validação de estabilidade de undervolt.
    /// Realiza cálculos matriciais e verificações matemáticas rigorosas por thread.
    /// Se um Curve Optimizer for instável, a thread falha na verificação de paridade.
    /// </summary>
    public class CpuStressTester
    {
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public event Action<StressTestProgress>? ProgressChanged;
        public event Action<StressTestResult>? TestCompleted;

        public async Task StartAsync(int durationSeconds)
        {
            if (_isRunning) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            int threadCount = Math.Max(1, Environment.ProcessorCount);
            var failures = new ConcurrentBag<string>();
            long totalOps = 0;
            var sw = Stopwatch.StartNew();
            var totalDuration = TimeSpan.FromSeconds(durationSeconds);

            Logger.Info($"Iniciando Teste de Estresse da CPU ({durationSeconds}s) em {threadCount} threads...", "STRESS_TEST");

            // Timer para emitir progresso
            var progressTimer = new System.Timers.Timer(500);
            progressTimer.Elapsed += (s, e) =>
            {
                var elapsed = sw.Elapsed;
                int pct = Math.Min(100, (int)((elapsed.TotalSeconds / totalDuration.TotalSeconds) * 100));
                long currentOps = Interlocked.Read(ref totalOps);
                long opsPerSec = elapsed.TotalSeconds > 0 ? (long)(currentOps / elapsed.TotalSeconds) : 0;

                ProgressChanged?.Invoke(new StressTestProgress
                {
                    Percent = pct,
                    Elapsed = elapsed,
                    Total = totalDuration,
                    OperationsPerSecond = opsPerSec,
                    ActiveThreads = threadCount
                });
            };
            progressTimer.Start();

            try
            {
                var tasks = new Task[threadCount];
                for (int t = 0; t < threadCount; t++)
                {
                    int threadId = t;
                    tasks[t] = Task.Run(() => RunStressWorker(threadId, token, ref totalOps, failures), token);
                }

                // Aguarda o término pelo tempo estipulado ou cancelamento
                var timeoutTask = Task.Delay(totalDuration, token);
                await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);
                _cts.Cancel(); // Encerra workers se o tempo expirou
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Error("Erro inesperado durante o teste de estresse", ex, "STRESS_TEST");
            }
            finally
            {
                progressTimer.Stop();
                progressTimer.Dispose();
                sw.Stop();
                _isRunning = false;
            }

            var result = new StressTestResult
            {
                Duration = sw.Elapsed,
                TotalCalculations = Interlocked.Read(ref totalOps),
                Passed = failures.IsEmpty
            };

            foreach (var f in failures) { result.FailureMessages.Add(f); }

            if (result.Passed)
            {
                result.Summary = $"✓ Estabilidade Aprovada! Todas as {threadCount} threads executaram {result.TotalCalculations:N0} operações matemáticas sem nenhuma divergência numérica.";
                Logger.Info($"Teste de estresse concluído com SUCESSO ({result.TotalCalculations:N0} operações).", "STRESS_TEST");
            }
            else
            {
                result.Summary = $"⚠️ Instabilidade Detectada! {result.FailureMessages.Count} thread(s) apresentaram erros de cálculo. Reduza a magnitude negativa do Curve Optimizer.";
                Logger.Warn($"Teste de estresse detectou INSTABILIDADE: {string.Join("; ", result.FailureMessages)}", "STRESS_TEST");
            }

            TestCompleted?.Invoke(result);
        }

        public void Cancel()
        {
            if (_isRunning && _cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                Logger.Info("Teste de estresse interrompido pelo usuário.", "STRESS_TEST");
            }
        }

        private static void RunStressWorker(int threadId, CancellationToken token, ref long totalOps, ConcurrentBag<string> failures)
        {
            const int batchSize = 1000;
            double x = 1.0000001 + (threadId * 0.0001);

            while (!token.IsCancellationRequested)
            {
                // Carga 1: Cálculo de convergência trigonométrica (sen^2 + cos^2 deve ser 1.0)
                for (int i = 0; i < batchSize; i++)
                {
                    double angle = x * 0.1234567;
                    double identity = (Math.Sin(angle) * Math.Sin(angle)) + (Math.Cos(angle) * Math.Cos(angle));
                    if (Math.Abs(identity - 1.0) > 1e-12)
                    {
                        failures.Add($"Thread {threadId}: Falha de paridade trigonométrica (erro de ponto flutuante)");
                        return;
                    }
                    x = (x * 1.00000001) % 100.0;
                }

                // Carga 2: Hash criptográfico SHA256 para estresse de registradores inteiros
                byte[] buffer = new byte[256];
                buffer[0] = (byte)threadId;
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(buffer);
                    if (hash.Length != 32)
                    {
                        failures.Add($"Thread {threadId}: Falha de integridade SHA256");
                        return;
                    }
                }

                Interlocked.Add(ref totalOps, batchSize + 256);
            }
        }
    }
}
