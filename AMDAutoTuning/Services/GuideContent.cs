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
