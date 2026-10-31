<#
.SYNOPSIS
    Navega automaticamente pelas abas e modais de uma aplicacao WPF via UI Automation
    e grava capturas de tela para auditoria visual e deteccao de regressao.

.DESCRIPTION
    Implementacao de referencia da skill wpf-ui-automation.
    Utiliza as bibliotecas nativas UIAutomationClient e UIAutomationTypes do Windows.

.PARAMETER ExePath
    Caminho do executavel a testar. Padrao: .\dist\AMDAutoTuning.exe

.PARAMETER OutputDirectory
    Pasta de destino dos screenshots. Padrao: .\artifacts\screenshots

.PARAMETER DelaySeconds
    Tempo de espera entre acoes na interface (segundos). Padrao: 1.2

.EXAMPLE
    .\navigate-ui.ps1 -ExePath .\dist\AMDAutoTuning.exe -OutputDirectory .\artifacts\screenshots
#>

param(
    [string]$ExePath = ".\dist\AMDAutoTuning.exe",
    [string]$OutputDirectory = ".\artifacts\screenshots",
    [double]$DelaySeconds = 1.2
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ExePath)) {
    throw "Executavel nao encontrado em: $ExePath"
}

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

# Verificacao de integridade de seguranca (UIPI)
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Warning "=========================================================================="
    Write-Warning "AVISO DE SEGURANCA (UIPI - User Interface Privilege Isolation):"
    Write-Warning "O aplicativo AMD AutoTuning roda em Alta Integridade (requireAdministrator)."
    Write-Warning "Terminais nao elevados sao bloqueados pelo Windows de ler a arvore UIA."
    Write-Warning "Para navegar e capturar telas completas, execute o PowerShell como Administrador"
    Write-Warning "ou utilize o ambiente descartavel: tools\AMDAutoTuning-Sandbox.wsb."
    Write-Warning "=========================================================================="
}

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinUser32 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@

function Get-ElementByAutomationId {
    param(
        [System.Windows.Automation.AutomationElement]$Parent,
        [string]$AutomationId,
        [int]$TimeoutSeconds = 8
    )

    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)

    $start = [DateTime]::Now
    while (([DateTime]::Now - $start).TotalSeconds -lt $TimeoutSeconds) {
        $el = $Parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($el) { return $el }
        Start-Sleep -Milliseconds 250
    }
    return $null
}

function Invoke-Control {
    param([System.Windows.Automation.AutomationElement]$Element)

    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
        $pattern.Select()
        return
    }

    if ($Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
        return
    }

    throw "Elemento nao suporta Invoke nem Selection."
}

function Save-Screenshot {
    param(
        [System.Windows.Automation.AutomationElement]$Element,
        [string]$FilePath
    )

    $rect = $Element.Current.BoundingRectangle
    if ($rect.Width -le 0 -or $rect.Height -le 0) { return }

    $bmp = New-Object System.Drawing.Bitmap([int]$rect.Width, [int]$rect.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
    $bmp.Save($FilePath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
    Write-Host "Screenshot: $(Split-Path $FilePath -Leaf) (${([int]$rect.Width)}x${([int]$rect.Height)})" -ForegroundColor DarkCyan
}

Write-Host "Iniciando $ExePath em modo simulacao (--dry-run)..." -ForegroundColor Cyan
$proc = Start-Process -FilePath $ExePath -ArgumentList "--dry-run" -PassThru

try {
    Start-Sleep -Seconds 4

    [WinUser32]::ShowWindow($proc.MainWindowHandle, 9) | Out-Null # SW_RESTORE
    [WinUser32]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
    if (-not $root) {
        throw "Nao foi possivel obter o elemento raiz da janela do aplicativo."
    }

    Write-Host "Janela localizada: $($root.Current.Name)" -ForegroundColor Green

    # Sequencia de abas para navegar
    $tabs = @(
        @{ Id = "TabDashboard"; Name = "01_painel_geral.png" },
        @{ Id = "TabOs"; Name = "02_os_latencia.png" },
        @{ Id = "TabCpuRam"; Name = "03_cpu_ram.png" },
        @{ Id = "TabGpuDisplays"; Name = "04_gpu_telas.png" },
        @{ Id = "TabDriversBios"; Name = "05_drivers_armazenamento.png" },
        @{ Id = "TabHistory"; Name = "06_historico_reversao.png" }
    )

    foreach ($tab in $tabs) {
        $el = Get-ElementByAutomationId -Parent $root -AutomationId $tab.Id
        if ($el) {
            Invoke-Control -Element $el
            Start-Sleep -Milliseconds ($DelaySeconds * 1000)
            $outPath = Join-Path $OutputDirectory $tab.Name
            Save-Screenshot -Element $root -FilePath $outPath
        } else {
            Write-Warning "Aba $($tab.Id) nao encontrada."
        }
    }

    # Teste do modal de pre-requisitos
    $btnPreflight = Get-ElementByAutomationId -Parent $root -AutomationId "BtnPreflight"
    if ($btnPreflight) {
        Write-Host "Abrindo tela de Pre-requisitos..." -ForegroundColor Cyan
        Invoke-Control -Element $btnPreflight
        Start-Sleep -Milliseconds ($DelaySeconds * 1000)
        Save-Screenshot -Element $root -FilePath (Join-Path $OutputDirectory "07_pre_requisitos.png")

        $btnClose = Get-ElementByAutomationId -Parent $root -AutomationId "BtnCloseDependencyPanel"
        if ($btnClose) {
            Invoke-Control -Element $btnClose
            Start-Sleep -Milliseconds 600
        }
    }

    Write-Host "Navegacao concluida com sucesso!" -ForegroundColor Green
}
finally {
    # Encerra o processo de forma limpa
    try {
        $winPattern = $null
        if ($root -and $root.TryGetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern, [ref]$winPattern)) {
            $winPattern.Close()
        } else {
            $proc | Stop-Process -Force -ErrorAction SilentlyContinue
        }
    }
    catch {
        $proc | Stop-Process -Force -ErrorAction SilentlyContinue
    }
}
