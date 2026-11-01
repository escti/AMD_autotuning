# Changelog

Todas as alterações notáveis neste projeto serão documentadas neste arquivo.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/),
e este projeto adere ao [Versionamento Semântico](https://semver.org/lang/pt-BR/).

---

## [1.5.2] - 2026-11-01

### Adicionado
- **Integração com `LibreHardwareMonitorLib` (Telemetria Física Real):**
  - Leitura física direta dos sensores do silício do AMD Ryzen via MSRs de hardware: temperatura `Core (Tctl/Tdie)` em °C, consumo real elétrico `CPU Package Power` em Watts e frequências reais instantâneas dos núcleos.
  - Elimina 100% dos cálculos por estimativa matemática, exibindo exatamente as mesmas temperaturas e consumos lidos pelo FanControl e HWiNFO.
  - Fallback gracioso automático para o kernel do Windows caso o driver de hardware não responda.
- **Controle Físico Real de Frequência e Teto de Potência nos Perfis:**
  - O `CpuTunerService` agora configura os registradores de gerenciamento de frequência de processador do Windows (`PROCFREQMAX`, `PERFBOOSTMODE` e `PROCTHROTTLEMAX`).
  - No perfil **Eco Mode 65W**, a CPU é travada fisicamente no clock base (4.70 GHz) e o boost agressivo é desativado, reduzindo o consumo de 105W para ~65W reais e a temperatura para menos de 68°C.
  - No perfil **Gaming Single-Core**, o teto de 5.45 GHz e o boost agressivo são destravados para máxima performance.

---

## [1.5.1] - 2026-11-01

### Modificado
- **Plano de Energia & EPP unificados na aba *Sistema Operacional & Latência*:**
  - O ajuste de política de energia (Plano Equilibrado + EPP em 33% para permitir transição de C-States e boost sustentado) foi totalmente consolidado na aba do SO como um item padrão do catálogo com aplicação e reversão automática.
- **Aplicação Imediata de Presets em Tempo Real:**
  - Clicar em qualquer preset (Eco, Balanced, Gaming, Render, Stock) agora aplica instantaneamente as políticas de energia, teto térmico e frequências de boost correspondentes no sistema, com feedback visual em tempo real (`🟢 Perfil Ativo`).
  - Adicionado botão de atalho `[ 🚀 Abrir AMD Ryzen Master ]` para abrir o utilitário oficial da AMD caso o usuário queira monitorar ou ajustar curvas adicionais.
- **Removida a função de gerar roteiro para BIOS:**
  - Removidos os botões de roteiro de BIOS da interface de tuning; o app foca em controle direto no sistema e exportação de fichas técnicas de perfil (`.txt`).

---

## [1.5.0] - 2026-11-01

### Adicionado
- **Mostrador de Frequência da CPU Atual vs Teto Máximo Configurado:**
  - O Gauge de frequência agora exibe o clock atual em tempo real e compara visualmente contra o teto configurado de boost (Clock base + Boost Override PBO), com barra de progresso proporcional.
- **Cartões de Receitas Prontas Estilo MMORPG (Stat Badges):**
  - Cada preset exibe pílulas visuais de atributos: valores reduzidos em vermelho com número negativo (`-54W PPT`, `-20°C TjMax`, `-25 mV V-Core`) e ganhos em verde com número positivo (`+200 MHz Boost`, `+40% Eficiência`, `+8% 1% Low FPS`).
- **Teste de Estresse com Suporte a Single-Core (Gold ⭐) vs Multi-Thread + Benchmark Score:**
  - Seletor de modo entre Multi-Thread (todas as threads) e Single-Core (focado exclusivamente no melhor núcleo Gold identificado dinamicamente).
  - Cálculo de pontuação padronizada de benchmark baseada em operações matemáticas por segundo (ex: ~14.000-16.000 pts em MT; ~2.400-2.800 pts em Single-Core).
- **Placar Lateral de Benchmarks (Scoreboard) com Tooltip de Ficha Técnica:**
  - Histórico de rodadas com horário, tipo de teste, duração, status de aprovação e pontuação.
  - Ao passar o mouse sobre qualquer linha, um balão detalhado exibe a ficha técnica completa do tuning que estava ativo naquela execução (Preset, PPT, TDC, EDC, TjMax, Boost Override e offsets de Curve Optimizer por núcleo).

---

## [1.4.0] - 2026-11-01

### Adicionado
- **Central de CPU Tuning & Curve Optimizer (sub-aba em *Processador & RAM*):**
  - **Detecção dinâmica de silício a cada boot:** identifica arquiteturas AMD Ryzen 5000 (Zen 3 / AM4) e 7000 (Zen 4 / AM5), classe de TDP (65W, 105W, 120W X3D, 170W) e inspeciona o Event ID 55 para classificar dinamicamente os núcleos ⭐ Gold e 🥈 Silver.
  - **Mostradores em tempo real (4 Gauges):** Uso Total da CPU (%), Temperatura Estimada (°C com cor dinâmica), Clock de Pico (GHz) e Consumo Estimado / Limite PPT (W).
  - **5 Receitas Prontas da Comunidade (1-clique):**
    1. 🌿 *Eco Mode 65W* (88W PPT / -25 CO / 75°C) — silêncio e eficiência com queda inferior a 2% em jogos.
    2. ⚖️ *Balanced Sweet Spot* (105W PPT / -20 CO / 85°C) — o ponto ideal recomendado para 100% de boost com ~15°C a menos.
    3. 🎯 *Competitive Gaming* (130W PPT / -15 Gold/Silver e -25 Demais / +200MHz) — foco em máximo 1% Low FPS.
    4. 🚀 *Multi-Thread Heavy* (PPT Stock / -15 CO / 88°C) — máxima sustentação all-core para render e Blender.
    5. 🛡️ *Padrão de Fábrica (Stock AMD)* — reset de segurança oficial para testes A/B.
  - **Motor de Teste de Estresse Nativo:** avaliação de estabilidade multi-thread com duração configurável (30s, 1m, 2m, 5m), cálculos matriciais de paridade e detecção precisa de instabilidade de undervolt.
  - **Modo Avançado com Sliders e Explicações Didáticas:** ajuste manual de Curve Optimizer (All-Core e Per-Core com destaque para Gold/Silver), limites PPT, TDC, EDC, TjMax térmico e Boost Override, com comentários educativos abaixo de cada controle.
  - **Geração de Roteiro de BIOS e Exportação:** instruções adaptadas para a placa-mãe detectada (MSI, ASUS, Gigabyte, ASRock) e exportação de perfil em `.txt`.
- **Suíte de Testes Automatizada (`tools\test-release.ps1`):** 6 baterias de testes em 1 comando (compilação limpa 0 warnings, encoding BOM nos scripts, sincronia Doc-First, integridade de BUGS.md, assinatura Authenticode sem mojibake e simulação `--dry-run`).
- **Modo Simulação Segura (`--dry-run` / `--simulate`):** permite rodar o executável completo e simular a aplicação de tweaks sem tocar no registro do Windows ou parar serviços do host.
- **Sistema de Logs Estruturado em Disco (`Services/Logger.cs`):** gravação diária em `%APPDATA%\AMDAutoTuning\Logs\app-YYYY-MM-DD.log` com telemetria de tweaks, WMI e captura de unhandled exceptions no `App.xaml.cs`.
- **Botão "📁 Logs em Disco" na interface:** atalho direto no rodapé da janela para inspecionar os arquivos de log.
- **Ambiente Descartável em 1-Clique (`tools\AMDAutoTuning-Sandbox.wsb`):** perfil de execução no Windows Sandbox para testes de ponta a ponta isolados da máquina de desenvolvimento.
- **Skill de Automação de UI (`wpf-ui-automation`):** automação nativa do Windows via `UIAutomationClient` para navegação em abas, disparo de cliques e captura de screenshots (`tools\test-ui-navigation.ps1`).
- **Guia Operacional de Testes (`TESTING.md`):** documentação da pirâmide de testes e critérios de aceite para novas releases.

### Corrigido
- **Reversão com valores hardcoded no `OptimizerService.cs` (Zero-Hardcode Policy):**
  - `RevertPowerPlan()` agora detecta e restaura o GUID real do usuário gravado no snapshot, eliminando o GUID fixo de máquina específica.
  - `RevertPagefile()` agora lê a configuração prévia do registro (`PagingFiles`) e a restaura fielmente, ou retorna a `?:\pagefile.sys` (padrão automático do Windows), sem forçar `16 4096`.
  - `RevertMemoryManagement()` restaura o valor prévio do usuário (padrão desktop `0`), sem forçar `LargeSystemCache = 1`.
  - `RevertIObitBloat()` só tenta reativar o serviço se ele já existia na máquina do usuário.
- **Encoding de scripts auxiliares:** `tools/make-icon.ps1` salvo com UTF-8 BOM, corrigindo falha apontada pelo verificador de release.

---

## [1.3.0] - 2026-10-31

### Adicionado
- **Tela de Pré-requisitos (`DependencyOverlay`) com verificação de 6 dependências** antes da auditoria:
  | Dependência | Classe | Detecção |
  | --- | --- | --- |
  | WMI | Obrigatória | consulta real ao `Win32_OperatingSystem` (prova funcional, não só serviço) |
  | Visual C++ 2015–2022 Redistributable (x64) | Obrigatória | `HKLM\...\VC\Runtimes\X64` → `Installed` + versão |
  | Driver AMD Radeon (Adrenalin) | Obrigatória | `Win32_VideoController` com vendor AMD e versão do driver |
  | winget | Recomendada | PATH → alias do usuário → pacote `Microsoft.DesktopAppInstaller_*` |
  | Driver de Chipset AMD (AM5) | Recomendada | serviço `amdppm` ou `System32\drivers\amdppm.sys` |
  | AMD Ryzen Master | Opcional | chave de desinstalação + `Program Files\AMD\RyzenMaster` |
- **Instalação automatizada via winget** (botão individual e "Instalar Todas as Ausentes"), com log em tempo real no console do app.
- **Download oficial em 1 clique** por dependência, com URLs da Microsoft e da AMD.
- **Reparo automático do WMI** (`net start winmgmt`) e instruções de `winmgmt /verifyrepository` quando falha.
- **Badge de pendências no cabeçalho** (contador vermelho no botão *Pré-requisitos*).
- **Flag `--deps`**: abre a tela de pré-requisitos direto, mesmo com tudo instalado (útil para conferência rápida).
- **`Models/DependencyItem.cs`** e **`Services/DependencyChecker.cs`**, isolados da UI e reaproveitáveis.

### Modificado
- **Achados ordenados por criticidade** em todas as abas e no Painel Geral: 🔴 Crítico → 🟠 Atenção → 🔵 Info → 🟢 Otimizado, com desempate alfabético estável. O que exige ação agora aparece sempre no topo.
- **Badge de versão no título da janela** passou a derivar do assembly (`AppVersionLabel`), eliminando a divergência que mantinha `v1.1` fixo no XAML.

### Corrigido
- **GitHub Actions nunca publicava a Release (403):** o workflow não declarava `permissions`, então o `GITHUB_TOKEN` herdava o padrão somente-leitura e o `softprops/action-gh-release` falhava com `Resource not accessible by integration`. A tag `v1.2.2` ficou sem artefato publicado. Adicionado `permissions: contents: write`.
- **`build.ps1` falhava com "Acesso negado" sem explicação** quando o app estava aberto (o `.exe` de `dist\` fica travado em execução). Agora detecta o processo e informa o motivo real antes de tentar apagar.
- **`sign-exe.ps1` (skill `windows-authenticode`) rejeitava `-PublisherCodePoints` via `-File`:** o PowerShell não converte a string `69,70,70` em `int[]`. O parâmetro virou `[string[]]` com split interno, aceitando tanto array quanto lista única — e a armadilha ficou documentada na skill.

### Conhecido (registrado em `BUGS.md`)
- **BUG-002 🔴 P2 — artefato do CI sai sem assinatura Authenticode.** O `.exe` da Release é gerado pelo `dotnet publish` puro; o assinado só existe no build local. Requer `.pfx` em `secrets` (ou certificado EV) para fechar a lacuna.

### Documentação (pendências da versão anterior, agora liberadas)
- **`BUGS.md` — catálogo de bugs e problemas conhecidos:**
  - Escala de gravidade P0–P3 (P0 automático para qualquer alteração de registro/serviço sem snapshot no ledger).
  - Seções *Abertos*, *Em análise / Bloqueados* e *Resolvidos*, com tabela de resumo.
  - Modelo de registro com versão testada, ambiente, passos de reprodução, evidência e causa raiz.
  - Fluxo explícito: bug corrigido migra para *Resolvidos* **e** para o `### Corrigido` do `CHANGELOG.md`.
- **Seção "Onde cada tipo de informação vive" no `AGENTS.md`**, eliminando a ambiguidade entre backlog, changelog e armadilhas técnicas.
- **Item `BUGS.md` e novo checklist na regra Doc-First**, para que nenhum achado de teste fique apenas na conversa.
- **Badge de versão do `README.md`** corrigido (estava em `v1.1.0` com o projeto em `1.2.2`).

---

## [1.2.2] - 2026-10-31

### Adicionado
- **Regra Doc-First no `AGENTS.md`:** commit e push passam a exigir atualização prévia de `CHANGELOG.md`, `README.md`, `AGENTS.md`, índice de skills e sincronia de versões entre `build.ps1`, `.csproj` e `CHANGELOG.md`.
- **Skills do projeto (`.opencode/skills/`)** — instruções reutilizáveis carregadas sob demanda:
  - `windows-authenticode`: assinatura Authenticode, registro em `LocalMachine` para o UAC, diagnóstico de mojibake no Subject e `scripts/sign-exe.ps1`.
  - `wpf-singlefile-app`: publish single-file, ícone multi-resolução, guardas de ciclo de vida do XAML e `scripts/make-icon.ps1`.
  - `amd-hardware-audit`: coleta real via WMI/EventLog/powercfg (ReBAR, CPPC Gold/Silver, EPP, VBS/HVCI, monitores, limites de PBO).
- **Seção "Documentação do Projeto" no `README.md`**, apontando para `AGENTS.md`, `CHANGELOG.md` e o índice de skills.

### Corrigido
- **Versão do executável não correspondia à release:** o `.csproj` permanecia em `1.2.0` enquanto o `CHANGELOG.md` já registrava `1.2.1`. Versões agora sincronizadas em `1.2.2`.

---

## [1.2.1] - 2026-10-30

### Corrigido
- **Acentuação do fornecedor no UAC (`Automações`):**
  - O `build.ps1` era salvo em UTF-8 sem BOM e o PowerShell 5.1 o lia como ANSI/Windows-1252, gerando certificados com Subject duplamente codificado (bytes `C3 83 C2 A7` em vez de `C3 A7`), que o Windows exibia como `Automa????es`.
  - O nome do certificado agora é montado por code points (`[char]0x00E7`, `[char]0x00F5`), imune a codepage do host, e validado por inspeção dos bytes do RDN antes de assinar.
  - O `build.ps1` passou a ser gravado em UTF-8 **com BOM**.
  - `App.xaml.cs` agora sincroniza as lojas `LocalMachine\Root` e `LocalMachine\TrustedPublisher`, registrando o certificado atual e removendo entradas obsoletas.

### Adicionado
- **Ícone do aplicativo:** `Assets/app.ico` multi-resolução (16, 24, 32, 48, 64, 128 e 256 px, frames PNG), aplicado via `<ApplicationIcon>` e vinculado à janela.
- **Gerador de ícone reproduzível:** `tools/make-icon.ps1`.
- **`AssemblyTitle`** definido, para o UAC e as propriedades do arquivo exibirem `AMD AutoTuning & Optimizer` em vez do nome interno do assembly.

---

## [1.2.0] - 2026-10-30

### Adicionado
- **Aba SO expandida para 13 subsistemas auditáveis:**
  - Telemetria (`DiagTrack` / `dmwappushservice`), SysMain (Superfetch em NVMe), P2P de Atualizações (DoSvc), Xbox GameDVR, Indexador WSearch, `LargeSystemCache`, `NetworkThrottlingIndex` / `SystemResponsiveness`, Game Mode & HAGS.
  - Cada item conta com ação de otimização e chave de reversão individual.
- **Aba CPU expandida com telemetria Zen 4 real:**
  - Ranking de núcleos **CPPC Gold / Silver** lido do event log do kernel (`Microsoft-Windows-Kernel-Processor-Power` ID 55).
  - Auditoria de **VBS / HVCI** (Integridade de Memória) com impacto estimado em 1% Low FPS.
  - Leitura do **EPP** (Energy Performance Preference) via `powercfg`.
- **3 Perfis de Tuning completos do Ryzen 7600X:** Gaming Single-Core (+200 MHz Boost Override), Balanced Sweet Spot (105W / 85°C) e Eco Mode Workstation (88W / 75°C), com passos para BIOS MSI e Ryzen Master.
- **Auto-registro do certificado no primeiro boot elevado** (`App.xaml.cs`), garantindo banner azul de fornecedor verificado no UAC.
- `AGENTS.md` com armadilhas arquiteturais, comandos de build e padrões de release.

### Corrigido
- Registro do certificado de assinatura em `LocalMachine\Root` e `LocalMachine\TrustedPublisher` (necessário porque o `consent.exe` do UAC ignora o escopo do usuário).

---

## [1.1.2] - 2026-10-30

### Corrigido
- **Detecção Precisa de Monitores Físicos:**
  - Substituição da leitura genérica de controladoras de vídeo pelo barramento EDID PnP (`WmiMonitorID` e `WmiMonitorConnectionParams`).
  - Eliminação de falso positivo para dockstations USB (Dell/DisplayLink) sem monitores conectados: o app agora valida a tela ativa diretamente na GPU dedicada e reconhece a dock exclusivamente como hub de portas/rede.
  - Correção no ciclo de vida de inicialização de abas WPF (`NullReferenceException` ao abrir o executável).

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
