using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AMDAutoTuning.Services
{
    public class TweakSnapshot
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string PreviousValue { get; set; } = string.Empty;
        public string AppliedValue { get; set; } = string.Empty;
        public string RevertKey { get; set; } = string.Empty;
        public bool CanRevert { get; set; } = true;
    }

    public class SnapshotService
    {
        private readonly string _historyFilePath;
        private readonly List<TweakSnapshot> _history = new();

        public SnapshotService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "AMDAutoTuning");
            Directory.CreateDirectory(appFolder);
            _historyFilePath = Path.Combine(appFolder, "tweaks_history.json");
            LoadHistory();
        }

        public IReadOnlyList<TweakSnapshot> History => _history;

        public TweakSnapshot? GetSnapshot(string id)
        {
            lock (_history)
            {
                return _history.Find(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            }
        }

        public string? GetPreviousValue(string id) => GetSnapshot(id)?.PreviousValue;

        public void Record(string id, string title, string previousValue, string appliedValue, string revertKey)
        {
            var item = new TweakSnapshot
            {
                Id = id,
                Title = title,
                Timestamp = DateTime.Now,
                PreviousValue = previousValue,
                AppliedValue = appliedValue,
                RevertKey = revertKey,
                CanRevert = true
            };

            lock (_history)
            {
                // Remove existing snapshot for same id if any, then insert newest at start
                _history.RemoveAll(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                _history.Insert(0, item);
                SaveHistory();
            }

            Logger.Info($"Snapshot gravado: [{id}] '{title}' -> Anterior: '{previousValue}' | Aplicado: '{appliedValue}'", "LEDGER");
        }

        public void Remove(string id)
        {
            lock (_history)
            {
                _history.RemoveAll(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                SaveHistory();
            }

            Logger.Info($"Snapshot removido após reversão: [{id}]", "LEDGER");
        }

        public void Clear()
        {
            lock (_history)
            {
                _history.Clear();
                SaveHistory();
            }

            Logger.Info("Histórico de snapshots completamente limpo.", "LEDGER");
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    string json = File.ReadAllText(_historyFilePath);
                    var list = JsonSerializer.Deserialize<List<TweakSnapshot>>(json);
                    if (list != null)
                    {
                        _history.Clear();
                        _history.AddRange(list);
                    }
                }
            }
            catch { }
        }

        private void SaveHistory()
        {
            try
            {
                string json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyFilePath, json);
            }
            catch { }
        }
    }
}
