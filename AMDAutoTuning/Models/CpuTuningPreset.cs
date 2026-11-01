using System;
using System.Collections.Generic;

namespace AMDAutoTuning.Models
{
    public class PresetAttributeBadge
    {
        public string Text { get; set; } = string.Empty;
        public bool IsPositive { get; set; } // true = verde (+), false = vermelho (-)

        public string BackgroundColor => IsPositive ? "#153022" : "#381B1B";
        public string BorderColor => IsPositive ? "#22C55E" : "#EF4444";
        public string TextColor => IsPositive ? "#4ADE80" : "#F87171";
    }

    public class CpuTuningPreset
    {
        public string Id { get; set; } = string.Empty;
        public string Emoji { get; set; } = "⚡";
        public string Name { get; set; } = string.Empty;
        public string Tagline { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public int PptWatts { get; set; }
        public int TdcAmps { get; set; }
        public int EdcAmps { get; set; }
        public int TjMaxCelsius { get; set; }
        public int BoostOverrideMhz { get; set; }
        public int AllCoreOffset { get; set; }

        public bool IsSelected { get; set; }

        public Dictionary<int, int> PerCoreOffsets { get; } = new();
        public List<PresetAttributeBadge> AttributeBadges { get; } = new();

        public static List<CpuTuningPreset> CreatePresetsFor(CpuTopology topo)
        {
            var list = new List<CpuTuningPreset>();

            // 1. ECO MODE 65W
            int ecoPpt = topo.Architecture == CpuArchitecture.Zen3_Ryzen5000 ? 76 : 88;
            int ecoTdc = topo.Architecture == CpuArchitecture.Zen3_Ryzen5000 ? 60 : 75;
            int ecoEdc = topo.Architecture == CpuArchitecture.Zen3_Ryzen5000 ? 90 : 150;
            int deltaPptEco = ecoPpt - topo.StockPptWatts;
            int deltaTjMaxEco = 75 - topo.StockTjMaxCelsius;

            var eco = new CpuTuningPreset
            {
                Id = "PRESET_ECO",
                Emoji = "🌿",
                Name = "Eco Mode 65W",
                Tagline = "Silêncio & Frio",
                Description = "Reduz o consumo em ~40% e mantém a CPU gelada (<65°C), com perda imperceptível (<2%) em jogos.",
                PptWatts = ecoPpt,
                TdcAmps = ecoTdc,
                EdcAmps = ecoEdc,
                TjMaxCelsius = 75,
                BoostOverrideMhz = 0,
                AllCoreOffset = -25
            };
            foreach (var core in topo.Cores) { eco.PerCoreOffsets[core.CoreIndex] = -25; }
            eco.AttributeBadges.Add(new PresetAttributeBadge { Text = $"{deltaPptEco}W PPT", IsPositive = false });
            eco.AttributeBadges.Add(new PresetAttributeBadge { Text = $"{deltaTjMaxEco}°C TjMax", IsPositive = false });
            eco.AttributeBadges.Add(new PresetAttributeBadge { Text = "-25 mV V-Core", IsPositive = false });
            eco.AttributeBadges.Add(new PresetAttributeBadge { Text = "+40% Eficiência", IsPositive = true });
            eco.AttributeBadges.Add(new PresetAttributeBadge { Text = "+0 MHz Boost", IsPositive = true });
            list.Add(eco);

            // 2. BALANCED SWEET SPOT (RECOMENDADO)
            int balPpt = topo.Architecture == CpuArchitecture.Zen3_Ryzen5000 ? 95 : 105;
            int balTdc = topo.Architecture == CpuArchitecture.Zen3_Ryzen5000 ? 80 : 85;
            int balEdc = topo.Architecture == CpuArchitecture.Zen3_Ryzen5000 ? 120 : 130;
            int deltaPptBal = balPpt - topo.StockPptWatts;
            int deltaTjMaxBal = 85 - topo.StockTjMaxCelsius;

            var balanced = new CpuTuningPreset
            {
                Id = "PRESET_BALANCED",
                Emoji = "⚖️",
                Name = "Balanced Sweet",
                Tagline = "Recomendado (100% Boost)",
                Description = "O ponto ideal para o dia a dia. Corta o alvo de 95°C de fábrica para 75°C mantendo 100% dos clocks.",
                PptWatts = balPpt,
                TdcAmps = balTdc,
                EdcAmps = balEdc,
                TjMaxCelsius = 85,
                BoostOverrideMhz = 0,
                AllCoreOffset = -20,
                IsSelected = true
            };
            foreach (var core in topo.Cores) { balanced.PerCoreOffsets[core.CoreIndex] = -20; }
            balanced.AttributeBadges.Add(new PresetAttributeBadge { Text = $"{deltaPptBal}W PPT", IsPositive = false });
            balanced.AttributeBadges.Add(new PresetAttributeBadge { Text = $"{deltaTjMaxBal}°C TjMax", IsPositive = false });
            balanced.AttributeBadges.Add(new PresetAttributeBadge { Text = "-20 mV V-Core", IsPositive = false });
            balanced.AttributeBadges.Add(new PresetAttributeBadge { Text = "+100% Boost", IsPositive = true });
            balanced.AttributeBadges.Add(new PresetAttributeBadge { Text = "+0 MHz Boost", IsPositive = true });
            list.Add(balanced);

            // 3. COMPETITIVE GAMING (FPS SINGLE-CORE MÁXIMO)
            int gamePpt = Math.Min(130, topo.StockPptWatts);
            int gameTdc = Math.Min(95, topo.StockTdcAmps);
            int gameEdc = Math.Min(150, topo.StockEdcAmps);
            int maxBoost = topo.IsX3D ? 0 : 200;
            int deltaPptGame = gamePpt - topo.StockPptWatts;
            int deltaTjMaxGame = 85 - topo.StockTjMaxCelsius;

            var gaming = new CpuTuningPreset
            {
                Id = "PRESET_GAMING",
                Emoji = "🎯",
                Name = "Gaming Single",
                Tagline = "FPS & 1% Low",
                Description = "Otimizado para jogos competitivos (CS2, Valorant). Usa offsets assimétricos e boost extra.",
                PptWatts = gamePpt,
                TdcAmps = gameTdc,
                EdcAmps = gameEdc,
                TjMaxCelsius = Math.Min(85, topo.StockTjMaxCelsius),
                BoostOverrideMhz = maxBoost,
                AllCoreOffset = -20
            };
            foreach (var core in topo.Cores)
            {
                gaming.PerCoreOffsets[core.CoreIndex] = (core.IsGold || core.IsSilver) ? -15 : -25;
            }
            if (maxBoost > 0)
            {
                gaming.AttributeBadges.Add(new PresetAttributeBadge { Text = $"+{maxBoost} MHz Boost", IsPositive = true });
            }
            gaming.AttributeBadges.Add(new PresetAttributeBadge { Text = "+8% 1% Low FPS", IsPositive = true });
            if (deltaPptGame != 0) gaming.AttributeBadges.Add(new PresetAttributeBadge { Text = $"{deltaPptGame}W PPT", IsPositive = false });
            gaming.AttributeBadges.Add(new PresetAttributeBadge { Text = $"{deltaTjMaxGame}°C TjMax", IsPositive = false });
            gaming.AttributeBadges.Add(new PresetAttributeBadge { Text = "-15/-25 mV CO", IsPositive = false });
            list.Add(gaming);

            // 4. MULTI-THREAD HEAVY (RENDER & PRODUTIVIDADE)
            var render = new CpuTuningPreset
            {
                Id = "PRESET_RENDER",
                Emoji = "🚀",
                Name = "Multi-Thread",
                Tagline = "Render & Heavy",
                Description = "Garante sustentação máxima em todos os núcleos para Blender e Cinebench, com teto térmico seguro de 88°C.",
                PptWatts = topo.StockPptWatts,
                TdcAmps = topo.StockTdcAmps,
                EdcAmps = topo.StockEdcAmps,
                TjMaxCelsius = Math.Min(88, topo.StockTjMaxCelsius),
                BoostOverrideMhz = 0,
                AllCoreOffset = -15
            };
            foreach (var core in topo.Cores) { render.PerCoreOffsets[core.CoreIndex] = -15; }
            render.AttributeBadges.Add(new PresetAttributeBadge { Text = $"+{topo.StockPptWatts}W Potência", IsPositive = true });
            render.AttributeBadges.Add(new PresetAttributeBadge { Text = "+5% Render MT", IsPositive = true });
            render.AttributeBadges.Add(new PresetAttributeBadge { Text = $"{88 - topo.StockTjMaxCelsius}°C TjMax", IsPositive = false });
            render.AttributeBadges.Add(new PresetAttributeBadge { Text = "-15 mV V-Core", IsPositive = false });
            list.Add(render);

            // 5. STOCK BASELINE (PADRÃO DE FÁBRICA)
            var stock = new CpuTuningPreset
            {
                Id = "PRESET_STOCK",
                Emoji = "🛡️",
                Name = "Stock AMD",
                Tagline = "Padrão Fábrica",
                Description = "Restaura todos os limites de fábrica da AMD para comparações de estabilidade e testes A/B.",
                PptWatts = topo.StockPptWatts,
                TdcAmps = topo.StockTdcAmps,
                EdcAmps = topo.StockEdcAmps,
                TjMaxCelsius = topo.StockTjMaxCelsius,
                BoostOverrideMhz = 0,
                AllCoreOffset = 0
            };
            foreach (var core in topo.Cores) { stock.PerCoreOffsets[core.CoreIndex] = 0; }
            stock.AttributeBadges.Add(new PresetAttributeBadge { Text = "0W Delta PPT", IsPositive = true });
            stock.AttributeBadges.Add(new PresetAttributeBadge { Text = "0°C Delta Temp", IsPositive = true });
            stock.AttributeBadges.Add(new PresetAttributeBadge { Text = "0 mV V-Core", IsPositive = true });
            stock.AttributeBadges.Add(new PresetAttributeBadge { Text = "0 MHz Boost", IsPositive = true });
            list.Add(stock);

            return list;
        }
    }
}
