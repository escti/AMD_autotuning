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
- Para o UAC exibir o fornecedor verificado da **EFF5 Automações Inteligentes** com banner azul, o certificado DEVE estar presente em `Cert:\LocalMachine\Root` e `Cert:\LocalMachine\TrustedPublisher` (gerenciado via `certutil -addstore -f "Root"` e `certutil -addstore -f "TrustedPublisher"` no `build.ps1`).

### 3. Detecção de Monitores (Evitar Falsos Positivos)
- **NUNCA** use `Win32_VideoController` para detectar monitores ativos. Dockstations USB (como Dell D6000/D3100) registram adaptadores DisplayLink virtuais no Windows mesmo sem telas conectadas.
- **Sempre** use `root\wmi` -> `WmiMonitorID` e `WmiMonitorConnectionParams` com filtro `Active = True`. Verifique o identificador de barramento para confirmar se a tela está no barramento PCI da GPU.

### 4. Sistema de Histórico e Segurança (Rollback Ledger)
- Qualquer tweak que altere registro do Windows ou serviços **DEVE** gravar o estado anterior via `_snapshotService.Record(...)` antes da modificação.
- O arquivo de log do usuário fica persistido em `%APPDATA%\AMDAutoTuning\tweaks_history.json`.

---

## 🚀 CI / CD & Releases no GitHub

- Pushes para a branch `main` com tags no formato `v*` (ex: `v1.2.0`) disparam o workflow `.github/workflows/build.yml`.
- O GitHub Actions compila em ambiente `windows-latest` e cria automaticamente a Release no GitHub anexando o `AMDAutoTuning.exe`.
