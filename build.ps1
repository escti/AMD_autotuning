<#
.SYNOPSIS
    Script de build e assinatura digital automatizada do AMD AutoTuning & Optimizer.
    Desenvolvido para EFF5 Automacoes Inteligentes (eff5.com.br)

.NOTES
    IMPORTANTE: este arquivo DEVE permanecer salvo em UTF-8 COM BOM.
    O Windows PowerShell 5.1 le scripts sem BOM como ANSI/Windows-1252, o que
    corrompe acentos e gera certificados com Subject duplamente codificado
    (ex.: "AutomaÃ§Ãµes"), que aparece quebrado no dialogo do UAC.
    Alem disso, o nome do certificado e montado por codigos de caractere
    para ser imune a qualquer configuracao de codepage do host.
#>

param(
    [switch]$SelfContained = $true,
    [string]$Configuration = "Release",
    [switch]$SkipSign = $false
)

$ErrorActionPreference = "Stop"

# Nome do fornecedor montado por code points (imune a encoding do host)
# 0x00E7 = c-cedilha | 0x00F5 = o-til
$certSubject = 'EFF5 Automa' + [char]0x00E7 + [char]0x00F5 + 'es Inteligentes'
$certCn = "CN=$certSubject"

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host $Text -ForegroundColor Cyan
}

function Test-CertSubjectEncoding {
    <#
        Rejeita certificados cujo RDN contenha a assinatura de dupla codificacao
        (C3 83 = "A til" + "C2" combinados), sintoma classico de mojibake.
    #>
    param([System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate)

    $raw = $Certificate.SubjectName.RawData
    for ($i = 0; $i -lt ($raw.Length - 1); $i++) {
        if ($raw[$i] -eq 0xC3 -and $raw[$i + 1] -eq 0x83) { return $false }
    }
    return $true
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       AMD AutoTuning & Optimizer - Build Script          " -ForegroundColor Cyan
Write-Host "             EFF5 Automacoes Inteligentes                 " -ForegroundColor DarkGray
Write-Host "                   eff5.com.br                            " -ForegroundColor DarkGray
Write-Host "==========================================================" -ForegroundColor Cyan

# Garante o SDK no PATH
$dotnetPath = Join-Path $env:USERPROFILE ".dotnet"
if (Test-Path $dotnetPath) {
    $env:PATH = "$dotnetPath;$env:PATH"
}

Write-Header "[1/5] Verificando versao do .NET SDK..."
dotnet --version

$outputDir = Join-Path $PSScriptRoot "dist"
if (Test-Path $outputDir) {
    Remove-Item -Path $outputDir -Recurse -Force
}

Write-Header "[2/5] Compilando executavel unico (Single-File)..."
$projectPath = Join-Path $PSScriptRoot "AMDAutoTuning\AMDAutoTuning.csproj"

if ($SelfContained) {
    Write-Host "Modo: Self-Contained (roda em qualquer Windows x64 sem instalar .NET)" -ForegroundColor Gray
    dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o $outputDir
} else {
    Write-Host "Modo: Portable (requer .NET 9 Desktop Runtime, arquivo ultraleve)" -ForegroundColor Gray
    dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=true -o $outputDir
}

$exePath = Join-Path $outputDir "AMDAutoTuning.exe"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

# ---------------------------------------------------------------------------
# Assinatura digital
# ---------------------------------------------------------------------------
if (-not $SkipSign -and (Test-Path $exePath)) {
    Write-Header "[3/5] Assinando digitalmente como $certSubject ..."

    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
            Where-Object { $_.Subject -like "*EFF5*" } |
            Select-Object -First 1

    # Descarta certificado legado com encoding corrompido
    if ($cert -and -not (Test-CertSubjectEncoding -Certificate $cert)) {
        Write-Host "Certificado existente possui Subject corrompido. Removendo..." -ForegroundColor Yellow
        Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force -ErrorAction SilentlyContinue
        $cert = $null
    }

    if (-not $cert) {
        Write-Host "Gerando certificado de assinatura de codigo..." -ForegroundColor Gray
        $cert = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject $certCn `
            -KeyAlgorithm RSA `
            -KeyLength 3072 `
            -HashAlgorithm SHA256 `
            -NotAfter (Get-Date).AddYears(10) `
            -CertStoreLocation "Cert:\CurrentUser\My"

        if (-not (Test-CertSubjectEncoding -Certificate $cert)) {
            throw "Falha critica: o certificado gerado continua com encoding invalido no Subject."
        }
    }

    Write-Host "Subject verificado: $($cert.Subject)" -ForegroundColor DarkGray
    Write-Host "Thumbprint: $($cert.Thumbprint)" -ForegroundColor DarkGray

    # -----------------------------------------------------------------------
    # Registro do certificado para o UAC
    # -----------------------------------------------------------------------
    Write-Header "[4/5] Registrando certificado nas lojas de confianca..."

    $tempCer = Join-Path $env:TEMP "eff5_codesign.cer"
    $cert | Export-Certificate -FilePath $tempCer -Force | Out-Null

    # Escopo do usuario (validacao local / Get-AuthenticodeSignature)
    certutil -addstore -user -f TrustedPublisher $tempCer | Out-Null
    certutil -addstore -user -f Root $tempCer | Out-Null

    if ($isAdmin) {
        # Escopo da maquina (OBRIGATORIO para o UAC / consent.exe)
        certutil -addstore -f Root $tempCer | Out-Null
        certutil -addstore -f TrustedPublisher $tempCer | Out-Null

        # Remove entradas EFF5 antigas (certificado corrompido) em LocalMachine
        foreach ($storeName in @("Root", "TrustedPublisher")) {
            $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($storeName, "LocalMachine")
            $store.Open("ReadWrite")
            $stale = @($store.Certificates | Where-Object {
                $_.Subject -like "*EFF5*" -and $_.Thumbprint -ne $cert.Thumbprint
            })
            foreach ($old in $stale) {
                $store.Remove($old)
                Write-Host "Removido certificado obsoleto de LocalMachine\$storeName (thumbprint $($old.Thumbprint.Substring(0,8))...)" -ForegroundColor DarkYellow
            }
            $store.Close()
        }

        Write-Host "Certificado registrado em LocalMachine\Root e LocalMachine\TrustedPublisher." -ForegroundColor Green
        Write-Host "O UAC exibira o banner azul com o fornecedor verificado." -ForegroundColor Green
    } else {
        Write-Host "Execucao sem elevacao: registro em LocalMachine nao realizado." -ForegroundColor Yellow
        Write-Host "O proprio aplicativo faz esse registro automaticamente no primeiro boot elevado." -ForegroundColor Gray
    }

    Remove-Item $tempCer -Force -ErrorAction SilentlyContinue

    # Aplica a assinatura com carimbo de tempo RFC 3161
    try {
        Set-AuthenticodeSignature -FilePath $exePath -Certificate $cert `
            -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com" -ErrorAction Stop | Out-Null
        Write-Host "Assinatura aplicada com carimbo de tempo validado." -ForegroundColor Green
    } catch {
        Set-AuthenticodeSignature -FilePath $exePath -Certificate $cert -HashAlgorithm SHA256 | Out-Null
        Write-Host "Assinatura aplicada localmente (carimbo de tempo indisponivel)." -ForegroundColor Yellow
    }
} else {
    Write-Header "[3/5] Assinatura digital ignorada."
}

# ---------------------------------------------------------------------------
# Verificacao final
# ---------------------------------------------------------------------------
Write-Header "[5/5] Build finalizado com sucesso!"

$exe = Get-Item $exePath
$sizeMb = [math]::Round($exe.Length / 1MB, 2)
Write-Host "Arquivo gerado: $($exe.FullName) ($sizeMb MB)" -ForegroundColor White

$sig = Get-AuthenticodeSignature $exePath
Write-Host "Assinatura: $($sig.Status)" -ForegroundColor White
if ($sig.SignerCertificate) {
    Write-Host "Fornecedor: $($sig.SignerCertificate.Subject)" -ForegroundColor Cyan
}

$vi = $exe.VersionInfo
Write-Host "Empresa:    $($vi.CompanyName)" -ForegroundColor Cyan
Write-Host "Versao:     $($vi.FileVersion)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
