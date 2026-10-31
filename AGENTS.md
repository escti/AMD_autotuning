# AGENTS.md — AMD AutoTuning & Optimizer (EFF5)

Instruções críticas e não óbvias para agentes que trabalham neste repositório.

---

## 🛠️ Ambiente & Comandos de Build

- **Stack:** .NET 9 WPF x64 (`net9.0-windows`), C# 13, Windows 10/11.
- **SDK Path Quirk:** Se `dotnet` não for encontrado no terminal, o SDK está localizado em `C:\Users\thiag\.dotnet`. Adicione ao PATH da sessão:
  ```powershell
  $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
  ```
- **Build & Assinatura Completa (Recomendado):**
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\build.ps1
  ```
  *Gera o executável standalone em `dist\AMDAutoTuning.exe` (~60 MB) e assina digitalmente.*
- **Build Rápido para Validação de Código:**
  ```powershell
  dotnet build AMDAutoTuning\AMDAutoTuning.csproj
  ```

---

## ⚠️ Armadilhas Arquiteturais Críticas (Gotchas)

### 1. Ciclo de Vida do WPF (`MainWindow.xaml.cs`)
- No XAML, abas (`RadioButton`) com `IsChecked="True"` disparam o evento `Checked` **durante o `InitializeComponent()`**, antes que os containers de visualização (`ViewDiagnostics`, `ViewHistory`) sejam instanciados.
- **Regra:** Qualquer método de troca de abas ou renderização de itens **DEVE** conter a guarda:
  ```csharp
  if (!_isInitialized || ViewDiagnostics == null || ViewHistory == null || PanelHardwareOverview == null) return;
  ```
  Omitir isso causa `NullReferenceException` silenciosa e encerra o aplicativo antes da janela abrir.

### 2. Assinatura Digital e UAC (`consent.exe`)
- O app possui `requireAdministrator` no `app.manifest`.
- A tela de elevação do UAC roda sob a conta `NT AUTHORITY\SYSTEM` e **ignora completamente** `Cert:\CurrentUser`.
- Para o UAC exibir o fornecedor verificado da **EFF5 Automações Inteligentes** com banner azul, o certificado DEVE estar presente em `Cert:\LocalMachine\Root` e `Cert:\LocalMachine\TrustedPublisher`.
- O `App.xaml.cs` sincroniza essas lojas no boot elevado, então rodar o `.exe` como admin já corrige o registro.
- **Armadilha de encoding (custo alto de depuração):** `build.ps1` PRECISA ficar em UTF-8 **com BOM**. O Windows PowerShell 5.1 lê scripts sem BOM como ANSI e corrompe acentos, gerando certificados com Subject duplamente codificado (`C3 83 C2 A7` em vez de `C3 A7`) que o UAC renderiza como `Automa????es`. O nome do certificado é montado por `[char]0x00E7` / `[char]0x00F5` e validado por inspeção dos bytes do RDN (`Test-CertSubjectEncoding`).

### 3. Detecção de Monitores (Evitar Falsos Positivos)
- **NUNCA** use `Win32_VideoController` para detectar monitores ativos. Dockstations USB (como Dell D6000/D3100) registram adaptadores DisplayLink virtuais no Windows mesmo sem telas conectadas.
- **Sempre** use `root\wmi` -> `WmiMonitorID` e `WmiMonitorConnectionParams` com filtro `Active = True`. Verifique o identificador de barramento para confirmar se a tela está no barramento PCI da GPU.

### 4. Sistema de Histórico e Segurança (Rollback Ledger)
- Qualquer tweak que altere registro do Windows ou serviços **DEVE** gravar o estado anterior via `_snapshotService.Record(...)` antes da modificação.
- O arquivo de log do usuário fica persistido em `%APPDATA%\AMDAutoTuning\tweaks_history.json`.

### 5. Ordenação dos Achados (`RefreshTabFilter`)
- A ordem de exibição é **por criticidade**, não por ordem de varredura: `OrderByDescending((int)Severity).ThenBy(Title)`.
  O enum `DiagnosticSeverity` já declara `Ok=0 … Critical=3`, então **não reordene o enum** — a ordenação depende disso.
- Qualquer novo `DiagnosticItem` criado no `HardwareScanner` entra automaticamente na ordem certa; não é preciso mexer no XAML.
- Se um dia for necessário agrupar por categoria, use `ThenBy(x => x.Category)` **entre** severidade e título.

### 6. Verificação de Dependências (`DependencyChecker`)
- `CheckAll()` roda em thread de fundo e devolve **uma lista nova**; a UI copia os valores para os itens já existentes em `RefreshDependenciesAsync()`. Isso evita `NotSupportedException` de `ObservableCollection` cross-thread.
- **Nunca** mutar um `DependencyItem` já data-bound a partir de thread de fundo (por isso `Repair()` não toca no item; o caller revalida na thread de UI).
- Uma dependência que não pode ser inspecionada vira `Missing` com a mensagem do erro — `CheckItem` engole exceções de propósito, para nunca derrubar o boot.
- **Instalação via winget valida por re-detecção, não por exit code.** O código de saída do winget é ambíguo (pacote já instalado, fonte desatualizada e falha real produzem códigos diferentes do esperado); o app relê o estado real do sistema depois de instalar.
- Dependências `Required` ausentes **bloqueiam** a auditoria (abre o overlay); `Recommended` e `Optional` apenas geram badge.
- Flag `--deps` força a abertura da tela de pré-requisitos — útil para validar a UI sem desinstalar nada.

### 7. `build.ps1` falha se o app estiver aberto
- O executável em execução trava `dist\AMDAutoTuning.exe`; o `Remove-Item -Recurse` do build morria com "Acesso negado" sem explicar nada.
- O script agora detecta `Get-Process -Name AMDAutoTuning` e aborta com mensagem clara. Se aparecer esse erro, feche a janela do app.
- Shell não elevado **não consegue** encerrar o app (que roda elevado por manifest): `Stop-Process` e `WM_CLOSE` falham com acesso negado. Peça ao usuário para fechar a janela.

---

## 📚 Skills do Projeto (`.opencode/skills/`)

Carregue a skill com a ferramenta `skill` antes de mexer nessas áreas:

| Skill | Quando usar |
| --- | --- |
| `windows-authenticode` | Assinatura, UAC, certificados, mojibake no Subject. Inclui `scripts/sign-exe.ps1`. |
| `wpf-singlefile-app` | Publish single-file, ícone multi-resolução, crashes de ciclo de vida do XAML. Inclui `scripts/make-icon.ps1`. |
| `amd-hardware-audit` | Coleta WMI/EventLog/powercfg de Ryzen/Radeon, ReBAR, CPPC, EPP, VBS. |

---

## 📋 Regra Obrigatória Pré-Commit & Pré-Push (Doc-First Rule)

**NUNCA execute `git commit` ou `git push` sem antes verificar e atualizar TODA a documentação do repositório.**

Antes de qualquer commit/push, revise e atualize:

1. **`CHANGELOG.md`** — registre toda alteração sob a versão atual ou em `[Unreleased]`, no padrão *Keep a Changelog* (Adicionado / Modificado / Corrigido / Removido).
2. **`README.md`** — atualize versão, catálogo de tweaks, tabelas de recursos e instruções de build quando o comportamento do app mudar.
3. **`AGENTS.md`** — registre qualquer armadilha técnica, comando ou quirk novo descoberto durante a tarefa.
4. **`BUGS.md`** — todo bug encontrado e **não corrigido na mesma tarefa** precisa estar catalogado aqui (ID, gravidade, passos de reprodução). Bug corrigido migra para *Resolvidos* com o commit e, em paralelo, para o `### Corrigido` do `CHANGELOG.md`.
5. **`.opencode/skills/README.md`** — atualize o índice se alguma skill ou script auxiliar foi criado, renomeado ou removido.
6. **`build.ps1` / `.csproj`** — a versão deve coincidir com a documentada. Ao subir versão, atualize `Version`, `AssemblyVersion`, `FileVersion` e o cabeçalho do `CHANGELOG.md` juntos.

Checklist obrigatório antes do `git add`:

- [ ] O `CHANGELOG.md` descreve exatamente o que mudou?
- [ ] O `README.md` continua coerente com os recursos reais?
- [ ] O `AGENTS.md` cobre as novas armadilhas, se houver?
- [ ] Todo bug conhecido e pendente está no `BUGS.md` (nenhum achado só na conversa)?
- [ ] O índice de skills reflete o diretório `.opencode/skills/`?
- [ ] As versões em `build.ps1`, `.csproj` e `CHANGELOG.md` batem?

Commit ou push sem cumprir esta checklist é considerado erro de processo.

### Onde cada tipo de informação vive

| Tipo de informação | Arquivo |
| --- | --- |
| O que mudou em cada versão (já entregue) | `CHANGELOG.md` |
| Bug encontrado e **ainda não corrigido** | `BUGS.md` |
| Ideia/melhoria em andamento, sem release | `CHANGELOG.md` → `[Unreleased]` → `### Adicionado` |
| Como usar, instalar e compilar | `README.md` |
| Armadilha técnica e procedimento de release | `AGENTS.md` |
| Instruções reutilizáveis por domínio | `.opencode/skills/` |

---

## 🚀 CI / CD & Releases no GitHub

- Pushes para a branch `main` com tags no formato `v*` (ex: `v1.2.0`) disparam o workflow `.github/workflows/build.yml`.
- O GitHub Actions compila em ambiente `windows-latest` e cria automaticamente a Release no GitHub anexando o `AMDAutoTuning.exe`.
- **`permissions: contents: write` é obrigatório no workflow.** Sem ele o `GITHUB_TOKEN` herdado é somente-leitura e o step de release falha com `403 — Resource not accessible by integration` (foi exatamente o que aconteceu na `v1.2.2`, registrado como BUG-001).
- **O workflow usa o arquivo do commit apontado pela tag.** Corrigir o `.yml` depois de criar a tag não conserta aquela execução: é preciso apagar e recriar a tag (`git tag -d`, `git push --delete`, retag) para o run usar a definição corrigida.
- O artefato do CI é **não assinado** (o workflow não executa `build.ps1`). Isso está catalogado como BUG-002 em `BUGS.md` e é a diferença entre `dist\` local e o download da Release.
- **Push exige a conta `escti`.** O `gh` pode estar na conta `thiagoea88` e o push falha com `403`. O Credential Manager armazena a conta errada e sobrepõe a URL, então use:
  ```powershell
  $t = (gh auth token --user escti).Trim()
  git -c credential.helper= push https://escti:$t@github.com/escti/AMD_autotuning.git main
  ```
  `gh auth switch` não é confiável aqui — ele reverte. Sempre passe `--user escti` ao pedir o token e nunca deixe o token gravado na URL do remote.
