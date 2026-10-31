<#
.SYNOPSIS
    Executa a navegacao automatizada na interface do AMD AutoTuning via UI Automation,
    capturando telas de todas as abas e modais para validacao visual.

.DESCRIPTION
    Script de conveniencia do projeto que executa a skill wpf-ui-automation.
#>

param(
    [string]$ExePath = ".\dist\AMDAutoTuning.exe",
    [string]$OutputDirectory = ".\artifacts\screenshots"
)

$scriptPath = Join-Path $PSScriptRoot "..\.opencode\skills\wpf-ui-automation\scripts\navigate-ui.ps1"

if (-not (Test-Path $scriptPath)) {
    throw "Script da skill nao encontrado em $scriptPath"
}

& $scriptPath -ExePath $ExePath -OutputDirectory $OutputDirectory
