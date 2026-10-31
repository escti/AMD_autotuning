<#
.SYNOPSIS
    Suite de testes completa e automatizada de pre-release para o AMD AutoTuning & Optimizer.

.DESCRIPTION
    Executa 6 baterias de validacao sem alterar o estado do Windows:
    1. Compilacao limpa (0 erros, 0 warnings).
    2. Integridade de encoding (UTF-8 com BOM em todos os scripts .ps1).
    3. Sincronia de versoes (Doc-First: .csproj == CHANGELOG.md == README.md).
    4. Integridade do catalogo de bugs (BUGS.md).
    5. Assinatura Authenticode e ausencia de mojibake (C3 83).
    6. Execucao segura e telemetria em disco (--dry-run).

.PARAMETER SkipBuild
    Pula a etapa de compilacao e usa o binario existente em dist\.

.PARAMETER RunUiAutomation
    Executa adicionalmente a navegacao automatica com capturas de tela via UI Automation.

.EXAMPLE
    .\tools\test-release.ps1
    .\tools\test-release.ps1 -RunUiAutomation
#>

param(
    [switch]$SkipBuild,
    [switch]$RunUiAutomation
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

# Adiciona .dotnet ao PATH se necessario
$dotnetPath = Join-Path $env:USERPROFILE ".dotnet"
if (Test-Path $dotnetPath) { $env:PATH = "$dotnetPath;$env:PATH" }

$passCount = 0
$failCount = 0

function Report-Result {
    param([string]$Step, [bool]$Success, [string]$Details = "")
    if ($Success) {
        Write-Host "  [PASS] $Step" -ForegroundColor Green
        if ($Details) { Write-Host "         $Details" -ForegroundColor DarkGray }
        $script:passCount++
    } else {
        Write-Host "  [FAIL] $Step" -ForegroundColor Red
        if ($Details) { Write-Host "         $Details" -ForegroundColor Yellow }
        $script:failCount++
    }
}

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "       AMD AutoTuning & Optimizer - Test Release Suite      " -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# --------------------------------------------------------------------------
# 1. COMPILACAO LIMPA (.NET)
# --------------------------------------------------------------------------
Write-Host "`n[1/6] Verificando compilacao do projeto..." -ForegroundColor Cyan
if (-not $SkipBuild) {
    $buildOutput = dotnet build "AMDAutoTuning\AMDAutoTuning.csproj" -c Release 2>&1
    $hasErrors = ($LASTEXITCODE -ne 0)
    $warningCount = ($buildOutput | Select-String "warning CS").Count

    if (-not $hasErrors -and $warningCount -eq 0) {
        Report-Result "Compilacao Release com 0 erros e 0 warnings" $true
    } elseif ($hasErrors) {
        Report-Result "Compilacao falhou com erros" $false ($buildOutput -join "`n")
    } else {
        Report-Result "Compilacao gerou $warningCount warning(s) de codigo" $false "Remova todos os warnings antes da release."
    }
} else {
    Write-Host "  [SKIP] Etapa de compilacao ignorada (-SkipBuild)." -ForegroundColor DarkGray
}

# --------------------------------------------------------------------------
# 2. ENCODING DOS SCRIPTS POWERSHELL (UTF-8 COM BOM)
# --------------------------------------------------------------------------
Write-Host "`n[2/6] Verificando encoding UTF-8 com BOM dos scripts..." -ForegroundColor Cyan
$ps1Files = Get-ChildItem -Path $repoRoot -Filter "*.ps1" -Recurse | Where-Object { $_.FullName -notmatch "node_modules|obj|bin" }
$allBomOk = $true
$bomDetails = @()

foreach ($f in $ps1Files) {
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    
    $errs = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$null, [ref]$errs)
    $parseOk = ($errs.Count -eq 0)

    if (-not $hasBom -or -not $parseOk) {
        $allBomOk = $false
        $bomDetails += "$($f.Name): BOM=$hasBom, Sintaxe=$parseOk"
    }
}

Report-Result "Encoding e sintaxe de $($ps1Files.Count) scripts .ps1" $allBomOk ($bomDetails -join "; ")

# --------------------------------------------------------------------------
# 3. SINCRONIA DE VERSOES (DOC-FIRST)
# --------------------------------------------------------------------------
Write-Host "`n[3/6] Verificando sincronia de versoes (Doc-First)..." -ForegroundColor Cyan
$csprojContent = Get-Content "AMDAutoTuning\AMDAutoTuning.csproj" -Raw
$csprojVersion = ""
if ($csprojContent -match "<Version>(.+?)</Version>") { $csprojVersion = $matches[1].Trim() }

$changelogContent = Get-Content "CHANGELOG.md" -Raw
$changelogHasVersion = ($changelogContent -match "## \[$csprojVersion\]")

$readmeContent = Get-Content "README.md" -Raw
$readmeHasBadge = ($readmeContent -match "img\.shields\.io/badge/Version-v$csprojVersion")

$versionsSynced = ($csprojVersion -and $changelogHasVersion -and $readmeHasBadge)
$syncMsg = "Versao: $csprojVersion | .csproj: OK | CHANGELOG: $(if ($changelogHasVersion) { 'OK' } else { 'FALHA' }) | README Badge: $(if ($readmeHasBadge) { 'OK' } else { 'FALHA' })"
Report-Result "Sincronia de versao entre codigo e documentacao" $versionsSynced $syncMsg

# --------------------------------------------------------------------------
# 4. CATALOGO DE BUGS (BUGS.MD)
# --------------------------------------------------------------------------
Write-Host "`n[4/6] Verificando integridade do BUGS.md..." -ForegroundColor Cyan
$bugsFile = "BUGS.md"
$bugsOk = (Test-Path $bugsFile)
$openBugs = 0
if ($bugsOk) {
    $bugsContent = Get-Content $bugsFile -Encoding UTF8 -Raw
    if ($bugsContent -match "Abertos\s*\|\s*(\d+)") { $openBugs = [int]$matches[1] }
}
Report-Result "Catalogo BUGS.md presente ($openBugs bug(s) aberto(s) catalogado(s))" $bugsOk

# --------------------------------------------------------------------------
# 5. ASSINATURA AUTHENTICODE & METADADOS DO .EXE
# --------------------------------------------------------------------------
Write-Host "`n[5/6] Verificando assinatura digital e metadados..." -ForegroundColor Cyan
$exePath = "dist\AMDAutoTuning.exe"
if (Test-Path $exePath) {
    $sig = Get-AuthenticodeSignature $exePath
    $sigValid = ($sig.Status -eq "Valid")

    $rawDn = ($sig.SignerCertificate.SubjectName.RawData | ForEach-Object { $_.ToString("X2") }) -join " "
    $hasMojibake = $rawDn.Contains("C3 83")

    $vi = (Get-Item $exePath).VersionInfo
    $verMatch = ($vi.FileVersion -match "^$csprojVersion")

    $authOk = ($sigValid -and -not $hasMojibake -and $verMatch)
    $authDetails = "Status: $($sig.Status) | Mojibake: $(if ($hasMojibake) { 'DETECTADO' } else { 'NAO' }) | FileVersion: $($vi.FileVersion)"
    Report-Result "Assinatura Authenticode e integridade de caracteres" $authOk $authDetails
} else {
    Report-Result "Executavel dist\AMDAutoTuning.exe nao encontrado" $false "Execute build.ps1 antes do teste de release."
}

# --------------------------------------------------------------------------
# 6. EXECUCAO SIMULADA (--DRY-RUN) E LOGS EM DISCO
# --------------------------------------------------------------------------
Write-Host "`n[6/6] Verificando execucao em modo simulacao (--dry-run)..." -ForegroundColor Cyan
if (Test-Path $exePath) {
    $appData = [Environment]::GetFolderPath("ApplicationData")
    $logDir = Join-Path $appData "AMDAutoTuning\Logs"
    $today = [DateTime]::Now.ToString("yyyy-MM-dd")
    $logFile = Join-Path $logDir "app-$today.log"

    $p = Start-Process -FilePath $exePath -ArgumentList "--dry-run" -PassThru
    Start-Sleep -Seconds 6

    $isAlive = $false
    if (Get-Process -Id $p.Id -ErrorAction SilentlyContinue) {
        $isAlive = $true
        $p | Stop-Process -Force -ErrorAction SilentlyContinue
    }

    $logExists = (Test-Path $logFile)
    $dryRunPassed = ($isAlive -and $logExists)
    Report-Result "App inicia sem crashar e grava telemetria em disco" $dryRunPassed "PID: $($p.Id) | Log: $logFile"
} else {
    Report-Result "Executavel dist\AMDAutoTuning.exe nao encontrado" $false
}

# --------------------------------------------------------------------------
# BATERIA OPCIONAL DE UI AUTOMATION
# --------------------------------------------------------------------------
if ($RunUiAutomation) {
    Write-Host "`n[EXTRA] Executando automacao de interface (UI Automation)..." -ForegroundColor Cyan
    try {
        & ".\tools\test-ui-navigation.ps1" -ExePath $exePath -OutputDirectory ".\artifacts\screenshots"
        Report-Result "Navegacao em abas e captura de screenshots" $true "Salvos em artifacts\screenshots\"
    } catch {
        Report-Result "Navegacao em abas falhou" $false $_.Exception.Message
    }
}

# --------------------------------------------------------------------------
# RESUMO FINAL
# --------------------------------------------------------------------------
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "Resultado dos Testes: $passCount PASS / $failCount FAIL" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })
Write-Host "============================================================" -ForegroundColor Cyan

if ($failCount -gt 0) {
    exit 1
} else {
    exit 0
}
