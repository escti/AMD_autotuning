using System;
using System.Collections.Generic;
using System.Linq;

namespace AMDAutoTuning.Models
{
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

        public static List<CpuTuningPreset> CreatePresetsFor(CpuTopology topo)
        {
            var list = new List<CpuTuningPreset>();

            // 1. ECO MODE
            int ecoPpt = topo.Architecture == CpuArchitecture.Zen3_Ryzen5000 ? 76 : 88;
            int ecoTdc = topo.Architecture == CpuArchitecture.Zen3_Ryzen5000 ? 60 : 75;
            int ecoEdc = topo.Architecture == CpuArchitecture.Zen3_Ryzen5000 ? 90 : 150;
            var eco = new CpuTuningPreset
            {
                Id = "PRESET_ECO",
                Emoji = "🌿",
                Name = "Eco Mode 65W",
                Tagline = "Silêncio & Eficiência",
                Description = "Reduz o consumo em ~40% e mantém a CPU fria (<65°C), perdendo menos de 2% de desempenho.",
                PptWatts = ecoPpt,
                TdcAmps = ecoTdc,
                EdcAmps = ecoEdc,
                TjMaxCelsius = 75,
                BoostOverrideMhz = 0,
                AllCoreOffset = -25
            };
            foreach (var core in topo.Cores) { eco.PerCoreOffsets[core.CoreIndex] = -25; }
            list.Add(eco);

            // 2. BALANCED SWEET SPOT (RECOMENDADO)
            int balPpt = topo.Architecture == CpuArchitecture.Zen3_Ryzen5000 ? 95 : 105;
            int balTdc = topo.Architecture == CpuArchitecture.Zen3_Ryzen5000 ? 80 : 85;
            int balEdc = topo.Architecture == CpuArchitecture.Zen3_Ryzen5000 ? 120 : 130;
            var balanced = new CpuTuningPreset
            {
                Id = "PRESET_BALANCED",
                Emoji = "⚖️",
                Name = "Balanced Sweet Spot",
                Tagline = "O Mais Recomendado (100% Boost)",
                Description = "O ponto ideal para o dia a dia. Corta o alvo de 95°C de fábrica para 75°C mantendo 100% dos clocks.",
                PptWatts = balPpt,
                TdcAmps = balTdc,
                EdcAmps = balEdc,
                TjMaxCelsius = 85,
                BoostOverrideMhz = 0,
                AllCoreOffset = -20,
                IsSelected = true // Pré-selecionado por padrão
            };
            foreach (var core in topo.Cores) { balanced.PerCoreOffsets[core.CoreIndex] = -20; }
            list.Add(balanced);

            // 3. COMPETITIVE GAMING (FPS SINGLE-CORE MÁXIMO)
            int gamePpt = Math.Min(130, topo.StockPptWatts);
            int gameTdc = Math.Min(95, topo.StockTdcAmps);
            int gameEdc = Math.Min(150, topo.StockEdcAmps);
            int maxBoost = topo.IsX3D ? 0 : 200; // X3D não aceita Boost Override por segurança física de V-Cache
            var gaming = new CpuTuningPreset
            {
                Id = "PRESET_GAMING",
                Emoji = "🎯",
                Name = "Competitive Gaming",
                Tagline = "Foco em FPS & 1% Low",
                Description = "Otimizado para jogos que demandam arrancadas agressivas de 1-2 núcleos (CS2, Valorant). Usa offsets assimétricos.",
                PptWatts = gamePpt,
                TdcAmps = gameTdc,
                EdcAmps = gameEdc,
                TjMaxCelsius = Math.Min(85, topo.StockTjMaxCelsius),
                BoostOverrideMhz = maxBoost,
                AllCoreOffset = -20
            };
            // Aplica curva assimétrica: núcleos Gold/Silver recebem -15 para máxima estabilidade em clocks de pico; os demais -25
            foreach (var core in topo.Cores)
            {
                gaming.PerCoreOffsets[core.CoreIndex] = (core.IsGold || core.IsSilver) ? -15 : -25;
            }
            list.Add(gaming);

            // 4. MULTI-THREAD HEAVY (RENDER & PRODUTIVIDADE)
            var render = new CpuTuningPreset
            {
                Id = "PRESET_RENDER",
                Emoji = "🚀",
                Name = "Multi-Thread Heavy",
                Tagline = "Render & Produtividade",
                Description = "Garante sustentação máxima em todos os núcleos para Blender e Cinebench, com teto térmico seguro de 88°C.",
                PptWatts = topo.StockPptWatts,
                TdcAmps = topo.StockTdcAmps,
                EdcAmps = topo.StockEdcAmps,
                TjMaxCelsius = Math.Min(88, topo.StockTjMaxCelsius),
                BoostOverrideMhz = 0,
                AllCoreOffset = -15
            };
            foreach (var core in topo.Cores) { render.PerCoreOffsets[core.CoreIndex] = -15; }
            list.Add(render);

            // 5. STOCK BASELINE (PADRÃO DE FÁBRICA)
            var stock = new CpuTuningPreset
            {
                Id = "PRESET_STOCK",
                Emoji = "🛡️",
                Name = "Padrão de Fábrica (Stock)",
                Tagline = "Reset de Segurança Oficial",
                Description = "Restaura todos os limites de fábrica da AMD para comparações de estabilidade e testes A/B.",
                PptWatts = topo.StockPptWatts,
                TdcAmps = topo.StockTdcAmps,
                EdcAmps = topo.StockEdcAmps,
                TjMaxCelsius = topo.StockTjMaxCelsius,
                BoostOverrideMhz = 0,
                AllCoreOffset = 0
            };
            foreach (var core in topo.Cores) { stock.PerCoreOffsets[core.CoreIndex] = 0; }
            list.Add(stock);

            return list;
        }
    }
}
