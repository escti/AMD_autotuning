namespace AMDAutoTuning.Services
{
    public static class GuideContent
    {
        public static string GetGuide(string key)
        {
            return key switch
            {
                "GUIDE_REBAR" => @"=======================================================
GUIA PASSO A PASSO: ATIVAR RESIZABLE BAR / SAM (BIOS MSI)
=======================================================

Sua Placa-Mãe: MSI PRO B650M-P (MS-7E27)
Sua GPU: AMD Radeon RX 9070 XT (16 GB VRAM)

1. Reinicie o computador.
2. Assim que o logo da MSI aparecer, pressione a tecla [DEL] repetidamente para entrar no Setup da BIOS (Click BIOS 5).
3. Pressione a tecla [F7] no teclado para entrar no modo 'Advanced' (Avançado).
4. No menu lateral esquerdo, clique em [Settings] (Configurações).
5. Clique em [Advanced] e depois em [PCIe/PCI Sub-system Settings].
6. Encontre e altere os dois itens abaixo:
   • 'Above 4G Memory/Crypto Currency Mining' -> Mude para [Enabled]
   • 'Re-Size BAR Support'                   -> Mude para [Enabled]
7. Pressione a tecla [F10] para salvar as alterações e clique em [Yes] para reiniciar.

Após iniciar o Windows, abra este aplicativo novamente para confirmar que a janela de VRAM completa foi liberada!",

                "GUIDE_DISPLAYLINK" => @"=======================================================
GUIA DE CONEXÃO DIRETA DOS MONITORES NA RX 9070 XT
=======================================================

Situação Atual Detectada:
• Monitor Principal (1440p @ 120Hz): Conectado na RX 9070 XT (Perfeito!)
• Monitor Secundário (1440p @ 120Hz): Conectado via Dell Video Dock (DisplayLink USB)

Por que mudar?
O DisplayLink USB comprime todos os quadros de 2560x1440 @ 120Hz usando os núcleos do seu Ryzen 7600X e transmite por USB. Isso gera atraso de exibição, micro-travamentos e impede o uso de FreeSync no segundo monitor.

Como resolver em 1 minuto:
1. Olhe na parte traseira do seu gabinete, diretamente na sua placa de vídeo AMD Radeon RX 9070 XT.
2. A placa possui 4 saídas de vídeo de altíssima velocidade:
   • 3x DisplayPort 2.1
   • 1x HDMI 2.1
3. Desconecte o cabo do segundo monitor da Dock USB Dell e conecte diretamente em uma das portas livres da RX 9070 XT (usando cabo DisplayPort ou HDMI).
4. Pronto! O Windows detectará instantaneamente a tela com aceleração total da GPU e zero consumo de CPU.",

                "GUIDE_CURVE_OPTIMIZER" => @"=======================================================
GUIA DE TUNING: CURVE OPTIMIZER & ECO MODE (RYZEN 7600X)
=======================================================

Configuração Recomendada para o seu Ryzen 5 7600X:
O 7600X de fábrica tenta empurrar 142W de PPT para buscar 95°C.
Com o ajuste abaixo, ele opera entre 65°C e 72°C mantendo 5.3 GHz!

OPÇÃO A: Via AMD Ryzen Master (Já instalado no seu Windows):
1. Abra o 'AMD Ryzen Master'.
2. Selecione a aba 'Profile 1' ou 'Basic View'.
3. Em 'Control Mode', selecione [Precision Boost Overdrive].
4. Ative o [Curve Optimizer]:
   • Mode: [All Cores]
   • Sign: [Negative] (-)
   • Magnitude: Comece com [20] (se estiver 100% estável, teste [25]).
5. Clique em [Apply & Test]. O sistema fará a validação de estabilidade.

OPÇÃO B: Direto na BIOS MSI (Definitivo e Permanente):
1. Entre na BIOS da MSI pressionando [DEL] ao ligar o PC.
2. Pressione [F7] para o modo Advanced -> Clique em [OC] (Overclocking).
3. Role até [Advanced CPU Configuration] -> [AMD Overclocking].
4. Entre em [Precision Boost Overdrive]:
   • Precision Boost Overdrive: [Advanced]
   • PBO Limits: [Manual]
   • PPT Limit [mW]: [88000]  (Limita a 88W em vez dos 142W ferventes)
   • Curve Optimizer: [All Cores]
   • Curve Optimizer Sign: [Negative]
   • Curve Optimizer Magnitude: [20]
5. Pressione [F10] para salvar e reiniciar.",

                "GUIDE_CPU_PROFILES" => @"=======================================================
GUIA DE TUNING: OS 3 PERFIS DEFINITIVOS DO RYZEN 5 7600X
=======================================================

O Ryzen 7600X de fábrica busca 95°C e 142W de PPT a todo custo.
Escolha o perfil ideal para o seu tipo de uso:

-------------------------------------------------------
PERFIL 1 — GAMING SINGLE-CORE (MÁXIMO FPS COMPETITIVO)
-------------------------------------------------------
Foco: CS2, Valorant, simuladores, jogos com pico de 1 núcleo.
Preparação: Requer sistema de arrefecimento robusto (air cooler 240mm ou water).

Na BIOS MSI (DEL -> F7 -> OC -> AMD Overclocking -> PBO):
  • Precision Boost Overdrive: [Advanced]
  • PBO Limits: [Manual]
  • PPT Limit [mW]: [130000]  (130W)
  • TDC Limit [mA]: [95000]   (95A)
  • EDC Limit [mA]: [150000]  (150A)
  • CPU Boost Clock Override: [Enabled]
  • Max CPU Boost Clock Override: [+200] MHz (+200 MHz no pico)
  • Curve Optimizer Sign: [Negative]
  • Curve Optimizer Magnitude: [15]  (Núcleos mais rápidos/estáveis)
  • PBO Thermal Point: [85°C]

No Windows: Plano de Energia Equilibrado, HAGS Ativado.

-------------------------------------------------------
PERFIL 2 — BALANCED SWEET SPOT (RECOMENDADO PARA 90%)
-------------------------------------------------------
Foco: Melhor equilíbrio entre temperatura baixa, ruído baixo e 100% do FPS.

Na BIOS MSI (DEL -> F7 -> OC -> AMD Overclocking -> PBO):
  • Precision Boost Overdrive: [Advanced]
  • PBO Limits: [Manual]
  • PPT Limit [mW]: [105000]  (105W)
  • TDC Limit [mA]: [85000]   (85A)
  • EDC Limit [mA]: [130000]  (130A)
  • PBO Thermal Point: [85°C]
  • Curve Optimizer: [All Cores]
  • Curve Optimizer Sign: [Negative]
  • Curve Optimizer Magnitude: [20]

Resultado: Temperatura cai de 95°C para 68°C a 74°C, ruído das fans cai pela metade e o desempenho em jogos permanece praticamente idêntico ao de fábrica.

-------------------------------------------------------
PERFIL 3 — ECO MODE & WORKSTATION (SILÊNCIO ABSOLUTO)
-------------------------------------------------------
Foco: Trabalho contínuo, renderização, gabinetes compactos e eficiência.

Na BIOS MSI (DEL -> F7 -> OC -> AMD Overclocking -> PBO):
  • Precision Boost Overdrive: [Advanced]
  • PBO Limits: [Manual]
  • PPT Limit [mW]: [88000]   (88W - equivalente AMD TDP 65W)
  • TDC Limit [mA]: [75000]   (75A)
  • EDC Limit [mA]: [150000]  (150A)
  • PBO Thermal Point: [75°C]
  • Curve Optimizer: [All Cores]
  • Curve Optimizer Sign: [Negative]
  • Curve Optimizer Magnitude: [30]

Resultado: Consumo da tomada cai cerca de 40%, a CPU opera entre 60°C e 68°C em estresse total, com queda inferior a 3% em multi-thread e menos de 1% em jogos.

-------------------------------------------------------
APLICAÇÃO VIA AMD RYZEN MASTER (Sem Reiniciar o PC)
-------------------------------------------------------
1. Abra o 'AMD Ryzen Master'.
2. Selecione [Profile 1] ou [Basic View].
3. Em [Control Mode], escolha [Precision Boost Overdrive].
4. Ajuste PPT, TDC, EDC e o Curve Optimizer conforme o perfil desejado.
5. Clique em [Apply & Test] para validação instantânea.",

                "GUIDE_VBS" => @"=======================================================
IMPACTO DO VBS / HVCI (INTEGRIDADE DE MEMÓRIA) EM JOGOS
=======================================================

O que é?
A 'Integridade de Memória' (HVCI) é um recurso de segurança do Windows 11 que usa a virtualização (Hyper-V) para isolar drivers do kernel de malware.

Qual é o impacto no AMD Ryzen 7000 (Zen 4)?
Cada chamada de alocação no kernel gera uma transição (VM Exit) que esvazia a previsão de saltos (Branch Target Buffer) do processador Zen 4.
Em jogos competitivos pesados (CS2, Valorant, Warzone, Cyberpunk), isso custa de 5% a 10% no 1% Low FPS e adiciona de 15 a 30 microssegundos de latência DPC.

Devo desativar?
• Para jogos competitivos e benchmarks de máxima performance: SIM, ganha-se FPS e reduz-se stutter.
• Para segurança e proteção contra malwares sofisticados: mantenha ATIVADO.

Como alternar (Requer Reinicialização do Windows):
1. Abra o Menu Iniciar e digite: 'Segurança do Windows'.
2. Clique em [Segurança do dispositivo] -> [Detalhes do isolamento do núcleo].
3. Desmarque [Integridade de memória].
4. Reinicie o computador.

Alternativa Programática (Administrador):
  reg add 'HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity' /v Enabled /t REG_DWORD /d 0 /f",

                "GUIDE_RAM" => @"=======================================================
GUIA DE MEMÓRIA RAM DDR5 NO AMD ZEN 4 (AM5)
=======================================================

Configuração Atual Detectada:
• 32 GB (2x 16GB) DDR5-5200 a 1.10V (JEDEC Padrão)
• Slots: DIMMA2 e DIMMB2 (Configuração correta em Dual Channel!)

Regras Fundamentais para a Menor Latência no AM5:
1. UCLK DIV1 MODE (Controlador de Memória):
   • O controlador de memória interno (UCLK) DEVE operar em proporção 1:1 com o clock real da memória (MCLK).
   • Na BIOS MSI: Overclocking (OC) -> [UCLK DIV1 MODE] -> Mude para [UCLK=MCLK].
   • NUNCA use UCLK=MCLK/2 (isso dobra a latência de acesso aos dados).

2. FCLK FREQUENCY (Infinity Fabric):
   • O valor de referência ideal e universalmente estável para os processadores Ryzen 7000 é [2000 MHz].
   • Na BIOS MSI: Overclocking (OC) -> [FCLK Frequency] -> Selecione [2000 MHz].

3. Perfil EXPO / XMP:
   • Se os seus pentes tiverem perfil de fábrica para 5600 ou 6000 MT/s, ative o botão [A-XMP / EXPO] no topo da BIOS MSI.
   • Caso sejam pentes JEDEC 5200 puros (1.10V), eles são 100% seguros e estáveis. O maior ganho sem risco de instabilidade no seu setup já vem do Curve Optimizer e do Resizable BAR!",

                _ => "Instruções não disponíveis."
            };
        }
    }
}
