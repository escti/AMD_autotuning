---
name: WPF UI Automation & Testing
description: Automatizar testes de interface de usuário em aplicações .NET/WPF no Windows usando UIAutomationClient nativo, navegando em abas, disparando cliques e capturando screenshots para inspeção visual sem dependências pesadas de Selenium/WinAppDriver.
---

# WPF UI Automation & Testing

Como controlar, navegar e tirar capturas de tela de aplicativos desktop WPF (.NET) no Windows
usando exclusivamente as bibliotecas nativas do sistema operacional (`UIAutomationClient` e `UIAutomationTypes`).

## 1. Por que Windows UI Automation (UIA)?

WinAppDriver, FlaUI, Selenium e Appium exigem servidores auxiliares, drivers de terceiros
ou pacotes NuGet pesados. O Windows possui embutido desde o Vista as DLLs:
- `UIAutomationClient.dll`
- `UIAutomationTypes.dll`

Elas inspecionam o visual tree do WPF diretamente por `AutomationId`, `Name` ou `ControlType`,
funcionando de forma limpa via script PowerShell.

## 2. Preparando o XAML para Testabilidade

Para que o UIA encontre elementos com 100% de estabilidade e sem depender de texto localizado:
Sempre defina `AutomationProperties.AutomationId` nos controles interativos:

```xml
<RadioButton x:Name="TabDashboard" 
             AutomationProperties.AutomationId="TabDashboard"
             Content="📊 Painel Geral" />

<Button x:Name="BtnOptimizeAll"
        AutomationProperties.AutomationId="BtnOptimizeAll"
        Content="Otimizar Tudo" />
```

## 3. Padrão de Inicialização e Captura do Elemento Raiz

```powershell
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

# 1. Inicia o processo
$proc = Start-Process -FilePath $exePath -ArgumentList "--dry-run" -PassThru
Start-Sleep -Seconds 4

# 2. Obtém a janela raiz
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
if (-not $root) { throw "Janela principal não encontrada." }
```

## 4. Localizando e Clicando em Elementos

### Por AutomationId:
```powershell
function Get-UiaElement {
    param([System.Windows.Automation.AutomationElement]$Parent, [string]$AutomationId)

    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    return $Parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}
```

### Acionando o Clique:
Controles WPF implementam padrões UIA (`InvokePattern` para Buttons, `SelectionItemPattern` para RadioButtons):

```powershell
function Invoke-UiaControl {
    param([System.Windows.Automation.AutomationElement]$Element)

    # Tenta padrão de seleção (RadioButtons / Tabs)
    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
        $pattern.Select()
        return
    }

    # Tenta padrão de clique simples (Buttons)
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
        return
    }

    throw "Controle não suporta InvokePattern nem SelectionItemPattern."
}
```

## 5. Capturando Screenshots de Alta Qualidade

Para salvar a tela para inspeção visual do agente ou usuário:

```powershell
function Save-UiaScreenshot {
    param([System.Windows.Automation.AutomationElement]$Element, [string]$OutputPath)

    Add-Type -AssemblyName System.Drawing
    $rect = $Element.Current.BoundingRectangle

    $bmp = New-Object System.Drawing.Bitmap([int]$rect.Width, [int]$rect.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
    $bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
}
```

## 6. Script de Referência

O script `scripts/navigate-ui.ps1` contém a implementação completa pronta para uso:
- Inicializa o app com `--dry-run`
- Clica ordenadamente em cada aba
- Dispara os modais
- Grava os prints em uma pasta designada
- Encerra o app de forma limpa
