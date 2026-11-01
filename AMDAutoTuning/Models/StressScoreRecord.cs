using System;

namespace AMDAutoTuning.Models
{
    /// <summary>
    /// Registro de resultado de uma rodada de teste de estresse / benchmark.
    /// Exibido no placar lateral com tooltip detalhado das configurações aplicadas.
    /// </summary>
    public class StressScoreRecord
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string TimeDisplay => Timestamp.ToString("HH:mm:ss");

        public string TestType { get; set; } = "Multi-Thread";
        public string Duration { get; set; } = "30s";
        public bool Passed { get; set; } = true;

        public string StatusText => Passed ? "✓ PASS" : "⚠️ ERRO";
        public string StatusColor => Passed ? "#22C55E" : "#EF4444";
        public string StatusBackground => Passed ? "#14291B" : "#381717";

        public int Score { get; set; }
        public string ScoreDisplay => $"{Score:N0} pts";

        /// <summary>
        /// Ficha técnica completa exibida no balão (tooltip) ao passar o mouse por cima da linha.
        /// </summary>
        public string AppliedSettingsTooltip { get; set; } = string.Empty;
    }
}
