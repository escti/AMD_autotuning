---
name: WPF Single-File Desktop App
description: Empacotar utilitarios Windows em .NET WPF como um unico .exe standalone (self-contained, sem instalador, com icone multi-resolucao), e evitar os crashes de ciclo de vida do XAML. Use ao criar, publicar ou distribuir apps desktop .NET/WPF no Windows, configurar PublishSingleFile, gerar .ico, ou depurar app que fecha sozinho ao abrir.
---

# WPF Single-File Desktop App

Receita validada para gerar **um unico `.exe`** de ~60 MB, sem runtime instalado,
sem instalador e sem falso positivo comum de PyInstaller.

## 1. Propriedades de publicacao (`.csproj`)

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net9.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
  <ApplicationManifest>app.manifest</ApplicationManifest>
  <ApplicationIcon>Assets\app.ico</ApplicationIcon>

  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
</PropertyGroup>
```

Comando equivalente:

```powershell
dotnet publish App.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o ./dist
```

Nao use Native AOT com WPF: o XAML/BAML nao e compativel. `PublishSingleFile` +
`SelfContained` e o caminho correto para WPF.

## 2. Metadados que aparecem para o usuario

Sem estes campos, o UAC e as propriedades do arquivo mostram o nome interno do assembly:

```xml
<AssemblyTitle>Meu App</AssemblyTitle>
<Company>Minha Empresa</Company>
<Product>Meu App</Product>
<Description>O que o app faz</Description>
<Copyright>Copyright (c) 2026 Minha Empresa</Copyright>
<Version>1.0.0</Version>
<FileVersion>1.0.0.0</FileVersion>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
```

`ApplicationIcon` cobre Explorer/barra de tarefas; `AssemblyTitle` cobre o UAC.
Defina os dois.

## 3. Icone multi-resolucao (armadilha silenciosa)

Um `.ico` com um unico frame de 256 px fica desfocado em 16/32 px. Gere frames
**16, 24, 32, 48, 64, 128 e 256** com payload PNG (suportado desde o Vista).

Use `scripts/make-icon.ps1` (renderiza vetorial em cada tamanho e monta o container
ICO manualmente). Evite depender de conversores online ou de `Icon.FromHandle`,
que perde os frames grandes.

Sempre registre o script no repo (`tools/make-icon.ps1`) para o icone ser reproduzivel.

## 4. Crash silencioso na inicializacao (a armadilha mais cara)

No XAML, `IsChecked="True"` dispara o evento `Checked` **durante**
`InitializeComponent()`, antes que os controles irmaos declarados mais abaixo existam.
Acessar esses controles no handler lanca `NullReferenceException` e o app fecha
sem mensagem visivel -- o UAC aparece e "nada acontece".

Padrao obrigatorio:

```csharp
private bool _isInitialized;

public MainWindow()
{
    _snapshotService = new SnapshotService(); // servicos primeiro
    InitializeComponent();
    _isInitialized = true;                    // so depois do XAML existir
    // ... bindings
}

private void Tab_Checked(object sender, RoutedEventArgs e)
{
    if (!_isInitialized) return;
    RefreshTabFilter();
}

private void RefreshTabFilter()
{
    if (!_isInitialized || ViewA == null || ViewB == null) return;
    // ...
}
```

## 5. Diagnostico: app fecha logo apos o UAC

O erro real esta no Event Viewer, nao no console:

```powershell
Get-WinEvent -LogName Application -MaxEvents 20 |
  Where-Object { $_.ProviderName -match 'Application Error|\.NET Runtime' } |
  Format-List TimeCreated, ProviderName, Message
```

Procure `System.NullReferenceException` e o stack trace — normalmente aponta direto
para o handler de inicializacao.

## 6. Elevacao (UAC) via manifest

```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```

`requireAdministrator` e necessario para escrever em `HKLM`, ler barramentos PCI,
gerenciar servicos e usar `certutil` em `LocalMachine`. Combine com a skill
`windows-authenticode` para o banner azul.

## 7. Estrutura de projeto que funciona bem

```text
App/
  Assets/app.ico
  Models/            # DTOs com INotifyPropertyChanged
  Services/          # logica testavel, sem dependencia de UI
  MainWindow.xaml
  App.xaml.cs        # boot, auto-registro, handlers globais
  app.manifest
build.ps1            # publish + assinatura + verificacao
tools/make-icon.ps1  # icone reproduzivel
```

Mantenha coleta/otimizacao em `Services/` puro. A `MainWindow` so orquestra e exibe,
o que permite testar e reusar a logica.

## 8. Checklist antes de publicar

1. `dotnet build` sem warnings.
2. `.exe` abre e permanece rodando (teste automatizado: `Start-Process -PassThru`,
   aguardar 5s, checar `HasExited`).
3. Icone visivel no Explorer e na barra de tarefas.
4. `VersionInfo.CompanyName` legivel (sem mojibake).
5. Assinatura `Valid` e UAC com fornecedor correto.
6. Tamanho final documentado no README.
