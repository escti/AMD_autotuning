# ⚡ AMD AutoTuning & Optimizer (ESCTI Edition)

[![Build & Release](https://github.com/escti/AMD_autotuning/actions/workflows/build.yml/badge.svg)](https://github.com/escti/AMD_autotuning/actions/workflows/build.yml)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20x64-blue.svg)](https://microsoft.com/windows)
[![Architecture](https://img.shields.io/badge/.NET-9.0%20WPF%20Single--File-red.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

Aplicativo desktop independente (**arquivo único `.exe`, sem instalador e sem dependências**) desenvolvido para auditar o hardware, diagnosticar gargalos críticos de firmware/BIOS e aplicar otimizações seguras em 1-clique no ecossistema **AMD Ryzen (AM5/AM4)** e **AMD Radeon (RDNA 4/3/2)** no Windows 11 e 10.

---

## 🎯 Por que este projeto nasceu?

Muitos computadores modernos de altíssimo desempenho (como plataformas **AM5 Zen 4** com placas **Radeon RX**) sofrem com gargalos silenciosos de configuração de fábrica que o usuário comum não percebe:

1. **Resizable BAR / AMD Smart Access Memory (SAM) desativado na BIOS:** A GPU fica limitada a uma janela de barramento PCIe legada de apenas 256 MB, perdendo até 15% de FPS e gerando engasgos (*stutters*) em DirectX 12/Vulkan.
2. **Monitores 120Hz/144Hz ligados em Docks DisplayLink USB:** A CPU passa a consumir ciclos para comprimir imagem por software em vez de usar as saídas diretas DisplayPort/HDMI da Radeon.
3. **Plano de Energia "Desempenho Máximo":** Ao contrário do que parece, no Ryzen 7000/9000 o plano "Desempenho Máximo" prejudica o agendamento **AMD CPPC**, impedindo os núcleos de esfriarem e reduzindo a margem térmica para o boost single-core máximo.
4. **Memória Virtual (Pagefile) fragmentada ou limitada:** Causa crashes aleatórios para a área de trabalho (*Crash to Desktop*) e esgotamento de commit em sessões de jogos pesados e render.
5. **Bloatware e Otimizadores Falsos:** Serviços em segundo plano de softwares como IObit/Advanced SystemCare que geram picos de latência DPC.

---

## 🚀 Recursos Principais

*   🔍 **Auditoria de Hardware Instantânea:** Detecta automaticamente modelo de CPU, núcleos, GPU, VRAM, velocidade da RAM DDR5/DDR4 e BIOS da placa-mãe.
*   📊 **Detecção Real do Resizable BAR:** Analisa diretamente a tabela de endereçamento físico PCIe da GPU para confirmar se a janela de VRAM completa está liberada.
*   ⚡ **Otimização Segura em 1-Clique:**
    *   Restaura o plano **Equilibrado** nativo com CPPC para máxima frequência do Ryzen.
    *   Fixa o arquivo de paginação em **8192 MB (8 GB)** contínuo no SSD NVMe.
    *   Desativa serviços e telemetrias intrusivas de bloatware.
    *   Ajusta a pilha de rede **TCP (Auto-Tuning Normal, RSS e Timings)** para menor latência em jogos online.
*   📖 **Guias Interativos Integrados:**
    *   Passo a passo com telas para placas-mãe **MSI (Click BIOS 5)** ativar *Above 4G Decoding* e *ReBAR*.
    *   Guia de undervolt com **Curve Optimizer (-20 all-core)** e **Eco Mode (PPT 88W)** para o Ryzen 5 7600X operar até 20°C mais frio mantendo 5.3 GHz.
    *   Instruções de conexão física direta para múltiplos monitores na GPU.
*   📦 **Executável Único (Standalone Single-File):** Apenas baixe o `AMDAutoTuning.exe` e execute. Não exige instalação de runtime do .NET, Python ou bibliotecas externas.

---

## 🖥️ Como Usar

1. Acesse a aba [Releases](https://github.com/escti/AMD_autotuning/releases) e baixe a versão mais recente do **`AMDAutoTuning.exe`**.
2. Clique com o botão direito e execute como Administrador (necessário para ler dados de barramento PCI e aplicar os planos de energia do Windows).
3. Analise o status dos cartões:
   * 🟢 **OTIMIZADO:** Componente configurado perfeitamente.
   * 🟡 **ATENÇÃO:** Oportunidade de melhoria recomendada.
   * 🔴 **CRÍTICO:** Gargalo ativo afetando desempenho.
4. Clique em **"Otimizar Tudo Seguro (1-Clique)"** ou use os botões individuais de cada cartão.

---

## 🛠️ Compilando a partir do Código Fonte

### Pré-requisitos
* Windows 10/11 x64
* [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) ou superior

### Compilação Rápida via PowerShell
Execute o script automatizado na raiz do repositório:

```powershell
.\build.ps1
```

O executável único e independente será gerado em:
`dist\AMDAutoTuning.exe`

---

## 📝 Licença
Distribuído sob a licença MIT. Veja `LICENSE` para mais informações.

Desenvolvido por **ESCTI** 🚀
