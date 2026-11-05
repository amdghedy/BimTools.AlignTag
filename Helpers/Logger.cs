using System;
using System.IO;
using System.Text;
using AlignTool.Models;
using AlignTool.Services;

namespace AlignTool.Helpers
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _logFolder;
        private static AppSettings _settings;

        public static void Initialise(AppSettings settings)
        {
            _settings = settings ?? new AppSettings();
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _logFolder = Path.Combine(appData, "AlignTool", "Logs");
                Directory.CreateDirectory(_logFolder);
                CleanOldLogs();
            }
            catch { _logFolder = null; }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message, Exception ex = null)
        {
            string full = ex == null ? message : message + Environment.NewLine + ex;
            Write("ERROR", full);
        }
        public static void Verbose(string message)
        {
            if (_settings != null && _settings.EnableVerboseLogging)
                Write("VERB", message);
        }

        private static void Write(string level, string message)
        {
            if (_settings == null || !_settings.EnableFileLogging || string.IsNullOrEmpty(_logFolder))
                return;
            try
            {
                string file = Path.Combine(_logFolder, $"AlignTool_{DateTime.Now:yyyy-MM-dd}.log");
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                lock (_lock) { File.AppendAllText(file, line, Encoding.UTF8); }
            }
            catch { }
        }

        private static void CleanOldLogs()
        {
            if (_settings == null || _logFolder == null) return;
            try
            {
                var cutoff = DateTime.Now.AddDays(-_settings.LogRetentionDays);
                foreach (var file in Directory.GetFiles(_logFolder, "AlignTool_*.log"))
                    if (File.GetLastWriteTime(file) < cutoff) File.Delete(file);
            }
            catch { }
        }
    }
}
