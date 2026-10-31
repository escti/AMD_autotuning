---
name: Windows Authenticode & UAC Publisher
description: Assinar executaveis no Windows (Authenticode/SHA256), fazer o UAC exibir o fornecedor verificado com banner azul, e diagnosticar mojibake no nome do certificado. Use ao gerar certificados de code signing, assinar .exe/.msi, configurar pipelines de release no Windows, ou quando o UAC mostrar "Fornecedor: Desconhecido" ou acentos quebrados.
---

# Windows Authenticode & UAC Publisher

Regras praticas validadas em producao. Siga na ordem.

## 1. Onde o UAC realmente valida a assinatura

O `consent.exe` roda no **Secure Desktop** como `NT AUTHORITY\SYSTEM` e valida a cadeia
**exclusivamente** contra `Cert:\LocalMachine\*`. Certificados em `Cert:\CurrentUser\*`
sao ignorados por design de seguranca (um usuario comum pode escrever nesse escopo).

Consequencia: `Get-AuthenticodeSignature` pode retornar `Valid` e o UAC continuar
mostrando "Fornecedor: Desconhecido".

| Loja | Necessaria para UAC | Funcao |
| --- | --- | --- |
| `LocalMachine\Root` | Sim | Ancora a cadeia de confianca |
| `LocalMachine\TrustedPublisher` | Sim | Autoriza o editor a elevar |
| `CurrentUser\Root` | Nao | So afeta `Get-AuthenticodeSignature` |
| `CurrentUser\My` | Nao | Guarda a chave privada para assinar |

Uma unica certificado **autoassinado** funciona: ele e sua propria raiz. Nao e
preciso montar hierarquia Root CA -> Intermediate -> Leaf, embora seja recomendado
quando a chave privada circula em CI/CD.

## 2. Armadilha numero 1: encoding do script

O Windows PowerShell 5.1 le arquivos `.ps1` **sem BOM** como ANSI/Windows-1252.
Uma string `"Automações"` em UTF-8 sem BOM chega ao `New-SelfSignedCertificate` como
`"AutomaÃ§Ãµes"`, e o certificado nasce com RDN duplamente codificado.

Sintoma nos bytes do DN:

- Correto: `... 6D 61 C3 A7 C3 B5 65 73 ...` (`ç` = `C3 A7`, `õ` = `C3 B5`)
- Corrompido: `... 6D 61 C3 83 C2 A7 C3 83 C2 B5 65 73 ...` (`C3 83` = marca de mojibake)

Regras obrigatorias:

1. Grave todo `.ps1` com acentos em **UTF-8 com BOM** (`EF BB BF`).
2. Monte strings criticas por **code points**, imunes a codepage do host:
   ```powershell
   $name = 'EFF5 Automa' + [char]0x00E7 + [char]0x00F5 + 'es Inteligentes'
   ```
3. **Valide os bytes** depois de criar o certificado e falhe o build se houver `C3 83`.

Ver `scripts/sign-exe.ps1` para a implementacao de referencia (cria, valida e assina).

> **Armadilha do `-File`:** o PowerShell nao converte a string `69,70,70` para
> `int[]` quando o script e chamado com `powershell -File script.ps1 -PublisherCodePoints 69,70,70`
> (o interpretador entrega um unico argumento de texto). O script de referencia
> declara o parametro como `[string[]]` e faz o split interno, aceitando tanto o
> array quanto a lista unica. Se for editar, preserve esse comportamento.

## 3. Armadilha numero 2: console mentiroso

O console do PowerShell 5.1 exibe `Automa????es` mesmo quando os bytes estao corretos
(o codepage de saida nao representa UTF-8). Nunca diagnostique encoding olhando o
terminal. Use uma destas provas:

```powershell
# Bytes reais do RDN
$cert.SubjectName.RawData | ForEach-Object { $_.ToString('X2') }

# Code points de um titulo de janela
$p = Get-Process -Name MeuApp | Select-Object -First 1
$p.MainWindowTitle.ToCharArray() | ForEach-Object { [int]$_ }
```

## 4. Armadilha numero 3: elevar para registrar

Escrever em `LocalMachine` exige elevacao. Duas estrategias que funcionam juntas:

- **Build elevado:** o script detecta `IsInRole(Administrator)` e registra direto.
- **Auto-registro em runtime:** o proprio app inicia com `requireAdministrator`
  (via `app.manifest`), extrai seu certificado com
  `X509Certificate.CreateFromSignedFile(Environment.ProcessPath)` e sincroniza
  `LocalMachine\Root` e `LocalMachine\TrustedPublisher` no primeiro boot.

O auto-registro torna o app autossuficiente em qualquer maquina nova.

## 5. Sempre assine com carimbo de tempo

Sem timestamp, a assinatura expira junto com o certificado. Use RFC 3161 e tenha
fallback local para build offline:

```powershell
Set-AuthenticodeSignature -FilePath $exe -Certificate $cert `
  -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com"
```

## 6. Checklist de verificacao

Antes de considerar a assinatura concluida:

1. `Get-AuthenticodeSignature` retorna `Status = Valid`.
2. Bytes do `SubjectName.RawData` nao contem `C3 83`.
3. O certificado esta em `LocalMachine\Root` e `LocalMachine\TrustedPublisher`.
4. Executar o `.exe`: o UAC mostra nome do programa e fornecedor corretos.
5. Propriedades do arquivo (`VersionInfo`) mostram `CompanyName` legivel.

## 7. Limitacao conhecida

Certificado autoassinado so produz banner azul nas maquinas onde a chave publica foi
registrada. Em maquinas de terceiros nao provisionadas, o SmartScreen continuara
avisando. Para distribuicao publica, adquira um certificado de autoridade
reconhecida (Sectigo, DigiCert, Certisign) ou use EV Code Signing.
