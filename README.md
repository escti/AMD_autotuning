# ⚡ AMD AutoTuning & Optimizer (ESCTI Edition)

[![Build & Release](https://github.com/escti/AMD_autotuning/actions/workflows/build.yml/badge.svg)](https://github.com/escti/AMD_autotuning/actions/workflows/build.yml)
[![Version](https://img.shields.io/badge/Version-v1.1.0-red.svg)](https://github.com/escti/AMD_autotuning/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20x64-blue.svg)](https://microsoft.com/windows)
[![Architecture](https://img.shields.io/badge/.NET-9.0%20WPF%20Single--File-red.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

Aplicativo desktop independente (**arquivo único `.exe`, sem instalador e sem dependências externas**) desenvolvido para auditar o hardware, diagnosticar gargalos críticos de firmware/BIOS e aplicar otimizações seguras em 1-clique no ecossistema **AMD Ryzen (AM5/AM4)** e **AMD Radeon (RDNA 4/3/2)** no Windows 11 e 10.

---

## 📑 Navegação em Abas Temáticas

O aplicativo é organizado em abas especializadas para facilitar a auditoria e o controle de cada subsistema:

```text
┌────────────────────────────────────────────────────────────────────────────────────────┐
│  ⚡ AMD AutoTuning & Optimizer (ESCTI)                                      ─  □  ✕  │
├─────────────┬───────────────────┬──────────────┬──────────────┬─────────────┬──────────┤
│  📊 Painel  │ 🖥️ OS & Latência │ ⚡ CPU & RAM │ 🎮 GPU/Telas │ 🖴 Drivers  │ 🛡️ Hist. │
├─────────────┴───────────────────┴──────────────┴──────────────┴─────────────┴──────────┤
│ [Health Score: 95/100]  Processador: Ryzen 7600X  |  GPU: Radeon RX 9070 XT             │
│                                                                                        │
│  ✓ AMD Smart Access Memory (ReBAR): ATIVADO (16 GB mapeados)                           │
│  ✓ Plano de Energia: OTIMIZADO (Equilibrado para CPPC)               [↩ Reverter]       │
│  ✓ Memória Virtual: SEGURO (8192 MB fixo no NVMe)                    [↩ Reverter]       │
│  ✓ Fast Startup: DESATIVADO (Ciclo limpo de drivers)                 [↩ Reverter]       │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

1. **📊 Painel Geral:** Visão consolidada com medidor de *Health Score*, resumo dos 4 pilares de hardware e botão de otimização em 1-clique.
2. **🖥️ Sistema Operacional & Latência:** Tweaks de gerenciamento de energia, agendamento de núcleos CPPC, memória virtual contínua, remoção de bloatware e pilha TCP.
3. **⚡ Processador & RAM:** Presets do Curve Optimizer (-15 a -30 all-core), guia do Eco Mode (PPT 88W) e verificação da proporção DDR5 UCLK:MCLK 1:1 e FCLK 2000 MHz.
4. **🎮 Placa de Vídeo & Telas:** Verificação da janela física de VRAM do Resizable BAR, topologia dos monitores (alerta de DisplayLink USB) e velocidade do barramento PCIe.
5. **🖴 Drivers & Armazenamento:** Estado do comando TRIM para SSDs NVMe e SATA, integridade e verificação dos componentes do chipset AMD AM5.
6. **🛡️ Histórico & Reversão:** Livro-razão (*ledger*) de alterações com timestamp e botão `↩ Reverter` para restaurar o estado original de qualquer tweak a qualquer momento.

---

## 📋 Catálogo Completo de Alterações e Segurança

Total transparência: veja exatamente o que cada otimização altera no sistema e como é feita a reversão:

| Tweak | O que faz no Windows | Benefício | Como Reverter |
| :--- | :--- | :--- | :--- |
| **Plano Equilibrado (CPPC)** | Define o plano `381b4222...` e ajusta o throttle min em 0% e max em 100%. | Permite aos núcleos ociosos do Zen 4 entrarem em C-States, liberando margem térmica para o boost single-core máximo (>5.3 GHz). | Botão `↩ Reverter` restaura o plano anterior no registro. |
| **Memória Virtual (Pagefile)** | Grava `c:\pagefile.sys 8192 8192` no registro `Memory Management`. | Aloca um bloco contínuo de 8GB no SSD NVMe, eliminando fragmentação e prevenindo erros de "Out of Memory" em jogos pesados. | Botão `↩ Reverter` restaura a paginação dinâmica anterior. |
| **Desativar Fast Startup** | Define `HiberbootEnabled = 0` em `Session Manager\Power`. | Garante desligamento e reinicialização 100% limpos do kernel, evitando acúmulo de fragmentação de RAM e bugs de driver de vídeo. | Botão `↩ Reverter` redefine `HiberbootEnabled = 1`. |
| **Desativar Bloatware IObit** | Para e desativa os serviços `IObitUnSvr`, `AdvancedSystemCare` e remove da inicialização. | Elimina varreduras contínuas em segundo plano e estabiliza a latência DPC (reduz microengasgos em jogos). | Botão `↩ Reverter` reconfigura o serviço para inicialização automática. |
| **Otimização TCP** | Configura `autotuninglevel=normal`, `rss=enabled` e desativa `timestamps`. | Menor bufferbloat e melhor distribuição de processamento de pacotes pelos núcleos da CPU na placa Realtek 2.5GbE. | Botão `↩ Reverter` restaura os valores de rede padrão do Windows. |
| **Limpeza de Inicialização** | Remove entradas duplicadas do Google Drive em perfis de sistema (.DEFAULT e S-1-5-18). | Menor consumo parasitário de memória RAM na inicialização. | Reconfigurável nas opções do Google Drive. |

---

## 🛡️ Sistema de Segurança e Ponto de Restauração

Antes de aplicar qualquer alteração:
* O usuário pode clicar em **"Criar Ponto de Restauração"** na aba *Histórico & Reversão* para registrar um snapshot oficial do Windows (`Checkpoint-Computer`).
* Todas as alterações de registro e serviços são salvas em `%APPDATA%\AMDAutoTuning\tweaks_history.json`.
* O botão **"Desfazer Todas as Alterações"** restaura em lote todos os valores originais.

---

## 🖥️ Como Usar

1. Baixe o executável mais recente na aba [Releases](https://github.com/escti/AMD_autotuning/releases): **`AMDAutoTuning.exe`**.
2. Clique com o botão direito e selecione **"Executar como Administrador"** (necessário para ler o barramento PCI e aplicar os planos do sistema).
3. Verifique o **Health Score** e os cartões sinalizados em vermelho ou amarelo.
4. Clique em **"Otimizar Tudo Seguro (1-Clique)"** ou navegue pelas abas para aplicar ou reverter ajustes pontuais.

---

## 🛠️ Compilando a partir do Código Fonte

### Pré-requisitos
* Windows 10/11 x64
* [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Compilação em 1 Comando
Na raiz do repositório, execute:

```powershell
.\build.ps1
```

O arquivo único compilado e independente de 59 MB estará pronto em:
`dist\AMDAutoTuning.exe`

---

## 📝 Licença
Distribuído sob a licença MIT. Veja `LICENSE` para mais detalhes.

Desenvolvido com excelência por **ESCTI** 🚀
