<#
.SYNOPSIS
    Script de build e assinatura digital automatizada do AMD AutoTuning & Optimizer
    Desenvolvido para EFF5 Automações Inteligentes (eff5.com.br)
#>

param(
    [switch]$SelfContained = $true,
    [string]$Configuration = "Release",
    [switch]$SkipSign = $false
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       AMD AutoTuning & Optimizer - Build Script          " -ForegroundColor Cyan
Write-Host "             EFF5 Automações Inteligentes                 " -ForegroundColor DarkGray
Write-Host "                   eff5.com.br                            " -ForegroundColor DarkGray
Write-Host "==========================================================" -ForegroundColor Cyan

# Ensure dotnet in PATH
$dotnetPath = Join-Path $env:USERPROFILE ".dotnet"
if (Test-Path $dotnetPath) {
    $env:PATH = "$dotnetPath;$env:PATH"
}

Write-Host "`n[1/4] Verificando versão do .NET SDK..." -ForegroundColor Yellow
dotnet --version

$outputDir = Join-Path $PSScriptRoot "dist"
if (Test-Path $outputDir) {
    Remove-Item -Path $outputDir -Recurse -Force
}

Write-Host "`n[2/4] Compilando executável único (Single-File)..." -ForegroundColor Yellow
$projectPath = Join-Path $PSScriptRoot "AMDAutoTuning\AMDAutoTuning.csproj"

if ($SelfContained) {
    Write-Host "Modo: Self-Contained (Independente, roda em qualquer Windows x64 sem instalar .NET)" -ForegroundColor Gray
    dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o $outputDir
} else {
    Write-Host "Modo: Portable (Requer .NET 9 Desktop Runtime, arquivo ultraleve)" -ForegroundColor Gray
    dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=true -o $outputDir
}

$exePath = Join-Path $outputDir "AMDAutoTuning.exe"

# Code Signing Section
if (-not $SkipSign -and (Test-Path $exePath)) {
    Write-Host "`n[3/4] Assinando digitalmente com a marca EFF5 Automações Inteligentes..." -ForegroundColor Yellow
    
    $certSubject = "EFF5 Automações Inteligentes"
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue | Where-Object { $_.Subject -match $certSubject } | Select-Object -First 1

    if (-not $cert) {
        Write-Host "Gerando certificado de assinatura de código para: $certSubject..." -ForegroundColor Gray
        $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=$certSubject" -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(5)
    }

    # Ensure certificate is in TrustedPublisher and Root for UAC verification
    try {
        $tempCer = Join-Path $env:TEMP "eff5_codesign.cer"
        $cert | Export-Certificate -FilePath $tempCer -Force | Out-Null

        # User stores (presentation & local validation)
        certutil -addstore -user TrustedPublisher $tempCer | Out-Null
        certutil -addstore -f -user Root $tempCer | Out-Null

        # LocalMachine stores (REQUIRED by UAC / consent.exe validation)
        $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        if ($isAdmin) {
            certutil -addstore -f Root $tempCer | Out-Null
            certutil -addstore -f TrustedPublisher $tempCer | Out-Null
            Write-Host "Certificado registrado em LocalMachine\Root e LocalMachine\TrustedPublisher (UAC verificado)." -ForegroundColor Green
        } else {
            Write-Host "AVISO: Execute o build.ps1 como Administrador para registrar o certificado em LocalMachine (UAC azul)." -ForegroundColor Yellow
            Write-Host "O proprio aplicativo faz esse registro automaticamente no primeiro boot elevado." -ForegroundColor Gray
        }

        Remove-Item $tempCer -Force -ErrorAction SilentlyContinue
    } catch { }

    try {
        $sigResult = Set-AuthenticodeSignature -FilePath $exePath -Certificate $cert -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com" -ErrorAction Stop
        Write-Host "Assinatura digital aplicada com êxito! Carimbo de tempo validado." -ForegroundColor Green
    } catch {
        # Fallback without timestamp if offline or server blocked
        $sigResult = Set-AuthenticodeSignature -FilePath $exePath -Certificate $cert -HashAlgorithm SHA256
        Write-Host "Assinatura digital aplicada localmente!" -ForegroundColor Green
    }
} else {
    Write-Host "`n[3/4] Assinatura digital ignorada." -ForegroundColor DarkGray
}

Write-Host "`n[4/4] Build finalizado com sucesso!" -ForegroundColor Green
$exe = Get-Item $exePath
$sizeMb = [math]::Round($exe.Length / 1MB, 2)
Write-Host "Arquivo gerado: $($exe.FullName) ($sizeMb MB)" -ForegroundColor White
Write-Host "Fornecedor: EFF5 Automações Inteligentes (eff5.com.br)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
