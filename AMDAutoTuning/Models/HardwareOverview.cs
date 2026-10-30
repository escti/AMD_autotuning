using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AMDAutoTuning.Models
{
    public class HardwareOverview : INotifyPropertyChanged
    {
        private string _cpuName = "Detectando...";
        private string _cpuCores = "";
        private string _gpuName = "Detectando...";
        private string _gpuVram = "";
        private string _gpuDriver = "";
        private string _ramSummary = "Detectando...";
        private string _ramSpeed = "";
        private string _motherboard = "Detectando...";
        private string _biosVersion = "";
        private string _osInfo = "Detectando...";

        public string CpuName { get => _cpuName; set { _cpuName = value; OnPropertyChanged(); } }
        public string CpuCores { get => _cpuCores; set { _cpuCores = value; OnPropertyChanged(); } }
        public string GpuName { get => _gpuName; set { _gpuName = value; OnPropertyChanged(); } }
        public string GpuVram { get => _gpuVram; set { _gpuVram = value; OnPropertyChanged(); } }
        public string GpuDriver { get => _gpuDriver; set { _gpuDriver = value; OnPropertyChanged(); } }
        public string RamSummary { get => _ramSummary; set { _ramSummary = value; OnPropertyChanged(); } }
        public string RamSpeed { get => _ramSpeed; set { _ramSpeed = value; OnPropertyChanged(); } }
        public string Motherboard { get => _motherboard; set { _motherboard = value; OnPropertyChanged(); } }
        public string BiosVersion { get => _biosVersion; set { _biosVersion = value; OnPropertyChanged(); } }
        public string OsInfo { get => _osInfo; set { _osInfo = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
