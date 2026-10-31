<#
.SYNOPSIS
    Assina um executavel com certificado autoassinado, validando encoding do Subject
    e registrando o certificado nas lojas da maquina para o UAC exibir fornecedor verificado.

.DESCRIPTION
    Implementacao de referencia da skill windows-authenticode.

    - Cria (ou reaproveita) um certificado Code Signing no CurrentUser\My
    - Rejeita certificado com Subject duplamente codificado (mojibake)
    - Nome montado por code points (imune a codepage do host)
    - Registra em LocalMachine\Root + LocalMachine\TrustedPublisher quando elevado
    - Assina com SHA256 + carimbo de tempo RFC 3161 (fallback local)

.PARAMETER ExePath
    Caminho do executavel a assinar.

.PARAMETER PublisherName
    Nome do fornecedor. Se omitido, use -PublisherCodePoints.

.PARAMETER PublisherCodePoints
    Array de inteiros (code points Unicode) que compoem o nome do fornecedor.
    Ex.: para "EFF5 Automações Inteligentes":
        69,70,70,53,32,65,117,116,111,109,97,231,245,101,115,32,73,110,116,101,108,105,103,101,110,116,101,115
    Use esta opcao em vez de -PublisherName quando o nome tiver acentos,
    garantindo independencia total de encoding.

.PARAMETER TimestampServer
    URL do servidor de carimbo de tempo RFC 3161.

.EXAMPLE
    .\sign-exe.ps1 -ExePath .\dist\App.exe -PublisherName "Minha Empresa Ltda"

.EXAMPLE
    $cp = 'EFF5 Automa' | ForEach-Object { $_.ToCharArray() } | ForEach-Object { [int]$_ }
    # ... acrescente os code points dos acentos e chame com -PublisherCodePoints
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [string]$PublisherName,

    [int[]]$PublisherCodePoints,

    [string]$TimestampServer = "http://timestamp.digicert.com",

    [switch]$SkipMachineTrust
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# 1. Resolver o nome do fornecedor de forma imune a encoding
# ---------------------------------------------------------------------------
if ($PublisherCodePoints -and $PublisherCodePoints.Count -gt 0) {
    $resolvedName = -join ($PublisherCodePoints | ForEach-Object { [char]$_ })
} elseif ($PublisherName) {
    $resolvedName = $PublisherName
} else {
    throw "Informe -PublisherName ou -PublisherCodePoints."
}

if (-not (Test-Path $ExePath)) {
    throw "Executavel nao encontrado: $ExePath"
}

$subjectCn = "CN=$resolvedName"

# ---------------------------------------------------------------------------
# 2. Validar encoding do Subject (rejeita mojibake C3 83)
# ---------------------------------------------------------------------------
function Test-SubjectEncoding {
    param([System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate)

    $raw = $Certificate.SubjectName.RawData
    for ($i = 0; $i -lt ($raw.Length - 1); $i++) {
        if ($raw[$i] -eq 0xC3 -and $raw[$i + 1] -eq 0x83) { return $false }
    }
    return $true
}

# ---------------------------------------------------------------------------
# 3. Obter ou criar o certificado
# ---------------------------------------------------------------------------
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -like "*$($resolvedName.Substring(0, [Math]::Min(6, $resolvedName.Length)))*" } |
        Select-Object -First 1

if ($cert -and -not (Test-SubjectEncoding -Certificate $cert)) {
    Write-Warning "Certificado existente com Subject corrompido. Removendo (thumbprint $($cert.Thumbprint))..."
    Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force -ErrorAction SilentlyContinue
    $cert = $null
}

if (-not $cert) {
    Write-Host "Criando certificado Code Signing..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $subjectCn `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddYears(10) `
        -CertStoreLocation "Cert:\CurrentUser\My"

    if (-not (Test-SubjectEncoding -Certificate $cert)) {
        throw "Falha critica: certificado gerado com Subject duplamente codificado. Verifique se este script esta salvo em UTF-8 COM BOM."
    }
}

Write-Host "Subject   : $($cert.Subject)" -ForegroundColor DarkGray
Write-Host "Thumbprint: $($cert.Thumbprint)" -ForegroundColor DarkGray

# ---------------------------------------------------------------------------
# 4. Registrar para o UAC
# ---------------------------------------------------------------------------
$tempCer = Join-Path $env:TEMP ("codesign_" + [guid]::NewGuid().ToString('N') + ".cer")
$cert | Export-Certificate -FilePath $tempCer -Force | Out-Null

certutil -addstore -user -f TrustedPublisher $tempCer | Out-Null
certutil -addstore -user -f Root $tempCer | Out-Null

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if ($isAdmin -and -not $SkipMachineTrust) {
    certutil -addstore -f Root $tempCer | Out-Null
    certutil -addstore -f TrustedPublisher $tempCer | Out-Null

    # Purga certificados antigos do mesmo fornecedor
    foreach ($storeName in @('Root', 'TrustedPublisher')) {
        $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($storeName, 'LocalMachine')
        $store.Open('ReadWrite')
        $stale = @($store.Certificates | Where-Object {
            $_.Subject -like "*$($resolvedName.Substring(0, 6))*" -and $_.Thumbprint -ne $cert.Thumbprint
        })
        foreach ($old in $stale) {
            try { $store.Remove($old) } catch { }
        }
        $store.Close()
    }
    Write-Host "Registrado em LocalMachine\Root e LocalMachine\TrustedPublisher." -ForegroundColor Green
} else {
    Write-Warning "Sem elevacao: registro em LocalMachine nao realizado. Rode como Administrador ou deixe o app se auto-registrar no primeiro boot elevado."
}

Remove-Item $tempCer -Force -ErrorAction SilentlyContinue

# ---------------------------------------------------------------------------
# 5. Assinar
# ---------------------------------------------------------------------------
try {
    Set-AuthenticodeSignature -FilePath $ExePath -Certificate $cert `
        -HashAlgorithm SHA256 -TimestampServer $TimestampServer -ErrorAction Stop | Out-Null
    Write-Host "Assinado com carimbo de tempo." -ForegroundColor Green
} catch {
    Set-AuthenticodeSignature -FilePath $ExePath -Certificate $cert -HashAlgorithm SHA256 | Out-Null
    Write-Warning "Assinado sem carimbo de tempo (servidor indisponivel)."
}

# ---------------------------------------------------------------------------
# 6. Verificar
# ---------------------------------------------------------------------------
$sig = Get-AuthenticodeSignature $ExePath
Write-Host ""
Write-Host "Status    : $($sig.Status)"
Write-Host "Fornecedor: $($sig.SignerCertificate.Subject)"

$rawBytes = ($sig.SignerCertificate.SubjectName.RawData | ForEach-Object { $_.ToString('X2') }) -join ' '
Write-Host "DN bytes  : $rawBytes"

if ($sig.Status -ne 'Valid') {
    Write-Warning "Status diferente de Valid. Verifique a cadeia de confianca."
}
