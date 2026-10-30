using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AMDAutoTuning.Models
{
    public enum DiagnosticSeverity
    {
        Ok,
        Info,
        Warning,
        Critical
    }

    public class DiagnosticItem : INotifyPropertyChanged
    {
        private DiagnosticSeverity _severity;
        private string _currentValue = string.Empty;
        private string _description = string.Empty;
        private bool _hasAction;
        private string _actionLabel = string.Empty;
        private bool _canRevert;

        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryKey { get; set; } = "OS"; // Dashboard, OS, CPU_RAM, GPU_DISPLAYS, DRIVERS_BIOS
        public string RecommendedValue { get; set; } = string.Empty;
        public string ActionKey { get; set; } = string.Empty;
        public string RevertKey { get; set; } = string.Empty;

        public DiagnosticSeverity Severity
        {
            get => _severity;
            set
            {
                if (_severity != value)
                {
                    _severity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusBadgeColor));
                    OnPropertyChanged(nameof(StatusIcon));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public string CurrentValue
        {
            get => _currentValue;
            set { if (_currentValue != value) { _currentValue = value; OnPropertyChanged(); } }
        }

        public string Description
        {
            get => _description;
            set { if (_description != value) { _description = value; OnPropertyChanged(); } }
        }

        public bool HasAction
        {
            get => _hasAction;
            set { if (_hasAction != value) { _hasAction = value; OnPropertyChanged(); } }
        }

        public string ActionLabel
        {
            get => _actionLabel;
            set { if (_actionLabel != value) { _actionLabel = value; OnPropertyChanged(); } }
        }

        public bool CanRevert
        {
            get => _canRevert;
            set { if (_canRevert != value) { _canRevert = value; OnPropertyChanged(); } }
        }

        public string StatusBadgeColor => Severity switch
        {
            DiagnosticSeverity.Ok => "#22C55E",        // Emerald Green
            DiagnosticSeverity.Info => "#06B6D4",      // Cyan
            DiagnosticSeverity.Warning => "#F59E0B",   // Amber
            DiagnosticSeverity.Critical => "#EF4444",  // Red
            _ => "#6B7280"
        };

        public string StatusIcon => Severity switch
        {
            DiagnosticSeverity.Ok => "✓",
            DiagnosticSeverity.Info => "ℹ",
            DiagnosticSeverity.Warning => "⚠",
            DiagnosticSeverity.Critical => "✕",
            _ => "•"
        };

        public string StatusText => Severity switch
        {
            DiagnosticSeverity.Ok => "OTIMIZADO",
            DiagnosticSeverity.Info => "INFO",
            DiagnosticSeverity.Warning => "ATENÇÃO",
            DiagnosticSeverity.Critical => "CRÍTICO",
            _ => "STATUS"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
