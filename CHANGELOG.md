# Changelog

Todas as alterações notáveis neste projeto serão documentadas neste arquivo.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/),
e este projeto adere ao [Versionamento Semântico](https://semver.org/lang/pt-BR/).

---

## [1.1.0] - 2026-10-30

### Adicionado
- **Identidade da Empresa & Assinatura Digital:**
  - Fornecedor oficial configurado como `EFF5 Automações Inteligentes`.
  - Site oficial `https://eff5.com.br` integrado aos metadados do executável e com link direto na interface.
  - Assinatura digital Authenticode com carimbo de data/hora (SHA256) automatizada no `build.ps1`.
- **Navegação por Abas Temáticas:**
  - `📊 Painel Geral`: Visão consolidada com cartões de hardware em tempo real e todos os diagnósticos.
  - `🖥️ Sistema Operacional & Latência`: Tweaks de plano de energia CPPC, pagefile 8GB, desativação de Fast Startup e remoção de bloatware.
  - `⚡ Processador & RAM`: Presets de Curve Optimizer (-15 a -30), guia de Eco Mode (PPT 88W) e auditor de latência DDR5 UCLK:MCLK 1:1.
  - `🎮 Placa de Vídeo & Telas`: Auditor físico de Resizable BAR / SAM, detector de barramento PCIe e alerta de telas em docks USB DisplayLink.
  - `🖴 Drivers & Armazenamento`: Checagem do comando TRIM em SSDs NVMe/SATA e verificação de drivers de chipset AMD B650.
  - `🛡️ Histórico & Reversão`: Ledger local (`tweaks_history.json`) com timestamp e valores originais para desfazer qualquer tweak com 1 clique.
- **Medidor de Health Score Dinâmico:** Pontuação geral de 0 a 100 com alertas visuais baseados na severidade dos gargalos encontrados.
- **Ponto de Restauração Integrado:** Botão para criar Pontos de Restauração do Windows diretamente pelo app antes de aplicar otimizações.
- **Reversão Individual e em Massa:** Cada alteração agora conta com botão `↩ Reverter` dedicado e a opção de desfazer todas as alterações registradas.
- **Guia de Memória AM5:** Documentação com as regras essenciais de latência DDR5 para a arquitetura Zen 4 (UCLK=MCLK 1:1 e FCLK 2000 MHz).

### Modificado
- Refatoração da interface gráfica WPF com novo seletor de abas estilizado em tema escuro AMD.
- Ajuste do packaging do executável único para v1.1.0 standalone.

---

## [1.0.0] - 2026-10-30

### Adicionado
- Lançamento inicial do **AMD AutoTuning & Optimizer**.
- Executável único nativo (.NET 9 WPF x64 Standalone Single-File).
- Auditoria de Resizable BAR lendo mapeamento de endereços PCI no Windows.
- Otimização em 1-clique para Plano Equilibrado AMD CPPC e Pagefile fixo de 8GB.
- Desativação de serviços IObit / Advanced SystemCare para estabilização de latência DPC.
- Guias integrados para Click BIOS 5 da MSI e conexão direta de monitores na RX 9070 XT.
