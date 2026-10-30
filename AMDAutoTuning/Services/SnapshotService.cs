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

            // Remove existing snapshot for same id if any, then insert newest at start
            _history.RemoveAll(x => x.Id == id);
            _history.Insert(0, item);
            SaveHistory();
        }

        public void Remove(string id)
        {
            _history.RemoveAll(x => x.Id == id);
            SaveHistory();
        }

        public void Clear()
        {
            _history.Clear();
            SaveHistory();
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
