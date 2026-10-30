<#
.SYNOPSIS
    Script de build automatizado do AMD AutoTuning & Optimizer
#>

param(
    [switch]$SelfContained = $true,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       AMD AutoTuning & Optimizer - Build Script          " -ForegroundColor Cyan
Write-Host "                       ESCTI                              " -ForegroundColor DarkGray
Write-Host "==========================================================" -ForegroundColor Cyan

# Ensure dotnet in PATH
$dotnetPath = Join-Path $env:USERPROFILE ".dotnet"
if (Test-Path $dotnetPath) {
    $env:PATH = "$dotnetPath;$env:PATH"
}

Write-Host "`n[1/3] Verificando versão do .NET SDK..." -ForegroundColor Yellow
dotnet --version

$outputDir = Join-Path $PSScriptRoot "dist"
if (Test-Path $outputDir) {
    Remove-Item -Path $outputDir -Recurse -Force
}

Write-Host "`n[2/3] Compilando executável único (Single-File)..." -ForegroundColor Yellow
$projectPath = Join-Path $PSScriptRoot "AMDAutoTuning\AMDAutoTuning.csproj"

if ($SelfContained) {
    Write-Host "Modo: Self-Contained (Independente, roda em qualquer Windows x64 sem instalar .NET)" -ForegroundColor Gray
    dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o $outputDir
} else {
    Write-Host "Modo: Portable (Requer .NET 9 Desktop Runtime, arquivo ultraleve)" -ForegroundColor Gray
    dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=true -o $outputDir
}

Write-Host "`n[3/3] Build finalizado com sucesso!" -ForegroundColor Green
$exe = Get-Item (Join-Path $outputDir "AMDAutoTuning.exe")
$sizeMb = [math]::Round($exe.Length / 1MB, 2)
Write-Host "Arquivo gerado: $($exe.FullName) ($sizeMb MB)" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan
