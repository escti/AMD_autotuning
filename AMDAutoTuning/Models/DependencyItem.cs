using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AMDAutoTuning.Models
{
    public enum DependencyKind
    {
        Required,
        Recommended,
        Optional
    }

    public enum DependencyState
    {
        Pending,
        Installed,
        Missing,
        NotApplicable
    }

    /// <summary>
    /// Uma dependencia externa que o app precisa (ou recomenda) que exista na maquina
    /// antes de rodar a auditoria completa. Alimenta a tela de Pre-requisitos.
    /// </summary>
    public class DependencyItem : INotifyPropertyChanged
    {
        private DependencyState _state = DependencyState.Pending;
        private string _detectedValue = "Verificando...";
        private string _statusMessage = string.Empty;
        private bool _isBusy;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>Obrigatorio bloqueia o app; recomendado gera aviso; opcional e apenas informativo.</summary>
        public DependencyKind Kind { get; set; } = DependencyKind.Required;

        /// <summary>ID do pacote no winget. Vazio = sem instalacao automatizada.</summary>
        public string WingetId { get; set; } = string.Empty;

        /// <summary>URL de download oficial. Vazio = sem botao de download.</summary>
        public string DownloadUrl { get; set; } = string.Empty;

        public string DownloadLabel { get; set; } = "🌐 Baixar";

        /// <summary>Se true, oferece o botao de reparo automatico (ex.: iniciar servico do Windows).</summary>
        public bool CanRepair { get; set; }

        public string RepairLabel { get; set; } = "♻ Reparar";

        public DependencyState State
        {
            get => _state;
            set
            {
                if (_state == value) return;
                _state = value;
                OnPropertyChanged();
                NotifyDerived();
            }
        }

        public string DetectedValue
        {
            get => _detectedValue;
            set { if (_detectedValue != value) { _detectedValue = value; OnPropertyChanged(); } }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { if (_statusMessage != value) { _statusMessage = value; OnPropertyChanged(); } }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                NotifyDerived();
            }
        }

        public bool IsInstalled => _state == DependencyState.Installed || _state == DependencyState.NotApplicable;

        public bool IsMissing => _state == DependencyState.Missing;

        /// <summary>Bloqueia a entrada no app (obrigatorio ausente).</summary>
        public bool IsBlocking => _state == DependencyState.Missing && Kind == DependencyKind.Required;

        public bool ShowWingetButton => IsMissing && !_isBusy && !string.IsNullOrWhiteSpace(WingetId);

        public bool ShowDownloadButton => IsMissing && !_isBusy && !string.IsNullOrWhiteSpace(DownloadUrl);

        public bool ShowRepairButton => IsMissing && !_isBusy && CanRepair;

        public string KindLabel => Kind switch
        {
            DependencyKind.Required => "OBRIGATÓRIO",
            DependencyKind.Recommended => "RECOMENDADO",
            _ => "OPCIONAL"
        };

        public string StatusBadgeColor => _state switch
        {
            DependencyState.Installed => "#22C55E",
            DependencyState.Missing => Kind == DependencyKind.Required ? "#EF4444" : "#F59E0B",
            DependencyState.NotApplicable => "#64748B",
            _ => "#06B6D4"
        };

        public string StatusIcon => _state switch
        {
            DependencyState.Installed => "✓",
            DependencyState.Missing => "✕",
            DependencyState.NotApplicable => "–",
            _ => "…"
        };

        public string StatusText => _state switch
        {
            DependencyState.Installed => "INSTALADO",
            DependencyState.Missing => "AUSENTE",
            DependencyState.NotApplicable => "NÃO APLICÁVEL",
            _ => "VERIFICANDO"
        };

        private void NotifyDerived()
        {
            OnPropertyChanged(nameof(IsInstalled));
            OnPropertyChanged(nameof(IsMissing));
            OnPropertyChanged(nameof(IsBlocking));
            OnPropertyChanged(nameof(ShowWingetButton));
            OnPropertyChanged(nameof(ShowDownloadButton));
            OnPropertyChanged(nameof(ShowRepairButton));
            OnPropertyChanged(nameof(StatusBadgeColor));
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
