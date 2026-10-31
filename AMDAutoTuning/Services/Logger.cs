using System;
using System.IO;
using System.Text;

namespace AMDAutoTuning.Services
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }

    /// <summary>
    /// Sistema de logs persistente em disco para auditoria, telemetria de tweaks
    /// e diagnóstico de crashes. Grava em %APPDATA%\AMDAutoTuning\Logs\app-YYYY-MM-DD.log.
    /// Thread-safe e sem dependências externas.
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new();
        private static readonly string _logDirectory;
        private static string _currentLogFile = string.Empty;

        public static string LogDirectory => _logDirectory;

        /// <summary>
        /// Evento disparado a cada linha gravada, permitindo espelhar no console visual da UI.
        /// </summary>
        public static event Action<string, LogLevel>? LogWritten;

        static Logger()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _logDirectory = Path.Combine(appData, "AMDAutoTuning", "Logs");
                Directory.CreateDirectory(_logDirectory);
                RotateLogFile();
            }
            catch
            {
                _logDirectory = Path.GetTempPath();
            }
        }

        private static void RotateLogFile()
        {
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            _currentLogFile = Path.Combine(_logDirectory, $"app-{dateStr}.log");
        }

        public static void Debug(string message, string category = "GENERAL") => Log(LogLevel.Debug, category, message);
        public static void Info(string message, string category = "GENERAL") => Log(LogLevel.Info, category, message);
        public static void Warn(string message, string category = "GENERAL") => Log(LogLevel.Warn, category, message);
        public static void Error(string message, Exception? ex = null, string category = "GENERAL")
        {
            string fullMsg = ex == null ? message : $"{message} | Exceção: {ex.GetType().Name}: {ex.Message}\nStack: {ex.StackTrace}";
            Log(LogLevel.Error, category, fullMsg);
        }
        public static void Fatal(string message, Exception? ex = null, string category = "CRASH")
        {
            string fullMsg = ex == null ? message : $"{message} | FATAL: {ex.GetType().Name}: {ex.Message}\nStack: {ex.StackTrace}";
            Log(LogLevel.Fatal, category, fullMsg);
        }

        private static void Log(LogLevel level, string category, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string levelTag = level.ToString().ToUpperInvariant().PadRight(5);
            string formattedLine = $"[{timestamp}] [{levelTag}] [{category}] {message}";

            try
            {
                lock (_lock)
                {
                    RotateLogFile();
                    File.AppendAllText(_currentLogFile, formattedLine + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Fallback silencioso: nunca derrubar a aplicação por falha de I/O em log
            }

            try
            {
                LogWritten?.Invoke(formattedLine, level);
            }
            catch { }
        }
    }
}
