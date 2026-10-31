# 🐛 Bugs & Problemas Conhecidos

Catálogo formal de falhas, inconsistências e comportamentos inesperados encontrados
durante as baterias de teste do **AMD AutoTuning & Optimizer**.

> **Por que este arquivo existe:** o `CHANGELOG.md` registra apenas o que **já foi
> resolvido**. Item encontrado e não corrigido imediatamente precisa de um lugar
> durável, senão se perde entre sessões de trabalho.

---

## 📌 Como usar

1. Toda falha encontrada em teste recebe um **ID sequencial** (`BUG-001`, `BUG-002`, …).
2. O item entra em **Abertos** usando o modelo da seção [Modelo de registro](#-modelo-de-registro)
   e recebe uma **gravidade**.
3. Enquanto a causa não estiver clara, o item fica em **Em análise / Bloqueados**.
4. Ao corrigir:
   - preencha **Resolução** (commit, versão e causa raiz);
   - mova o item para **Resolvidos**;
   - registre a correção em `CHANGELOG.md` (seção `### Corrigido`, versão atual ou `[Unreleased]`).
5. Se a falha revelar uma armadilha técnica reutilizável (encoding, WPF, WMI, assinatura),
   registre também em `AGENTS.md` — e considere virar uma skill em `.opencode/skills/`.

**Testes manuais não rastreados não existem.** Se não está aqui ou no `CHANGELOG.md`,
o problema será redescoberto do zero.

---

## 🚦 Escala de gravidade

| Nível | Sigla | Critério | Prazo |
| --- | --- | --- | --- |
| 🔴 **Crítica** | P0 | Crash do app, corrupção de dados, tweak aplicado sem reversão possível, falso positivo que leva o usuário a alterar a BIOS indevidamente | Bloqueia qualquer release |
| 🟠 **Alta** | P1 | Funcionalidade principal quebrada, detecção de hardware incorreta, reversão que não devolve o estado original | Próxima release |
| 🟡 **Média** | P2 | Comportamento incorreto em cenário específico, diagnóstico impreciso, tweak parcialmente aplicado | Backlog priorizado |
| 🔵 **Baixa** | P3 | Texto, acentuação, alinhamento, mensagem confusa, link quebrado | Oportunidade |

> Regra herdada de `AGENTS.md`: qualquer bug que envolva escrita em registro/serviço
> sem `_snapshotService.Record(...)` é automaticamente **P0**.

---

## 📊 Resumo

| Status | Quantidade |
| --- | --- |
| 🔴 Abertos | 1 |
| 🟠 Em análise / Bloqueados | 0 |
| 🟢 Resolvidos | 2 |

_Última atualização: 2026-10-31._

---

## 🔴 Abertos

### BUG-002 — Artefato gerado pelo CI sai sem assinatura Authenticode

| Campo | Valor |
| --- | --- |
| **Data** | 2026-10-31 |
| **Gravidade** | 🟡 Média (P2) |
| **Área** | `.github/workflows/build.yml` — step *Publish Single-File Executable* |
| **Versão testada** | 1.3.0 |
| **Ambiente** | GitHub Actions · `windows-latest` |
| **Status** | Aberto |

**Comportamento observado:**
O `AMDAutoTuning.exe` publicado na Release pelo GitHub Actions é compilado sem assinatura
(o workflow não executa `build.ps1` nem o step de assinatura). O `.exe` assinado só existe
quando o build é feito localmente.

**Comportamento esperado:**
O artefato da Release deveria ser idêntico ao gerado por `build.ps1`: assinado com SHA256,
carimbo de tempo e `CompanyName = EFF5 Automações Inteligentes`.

**Impacto:**
O SmartScreen/UAC trata o download da Release como "Editor desconhecido", minando a
percepção de confiança do produto — exatamente o problema que a skill
`windows-authenticode` resolve no build local.

**Hipótese / causa raiz:**
A chave privada do certificado (`.pfx`) não está no CI e o workflow nunca chama o passo
de assinatura. Opções: (a) guardar `.pfx` + senha em `secrets`, importar, assinar com
`Set-AuthenticodeSignature` e publicar o artefato assinado; (b) adquirir certificado de
CA reconhecida e usar EV Code Signing em hardware/HSM.

---

## 🟠 Em análise / Bloqueados

_Nenhum item aguardando investigação ou dependência externa._

---

## 🟢 Resolvidos

### BUG-003 — Reversão de tweaks no OptimizerService continha valores hardcoded de máquina específica

| Campo | Valor |
| --- | --- |
| **Data** | 2026-10-31 |
| **Gravidade** | 🔴 Crítica (P0) |
| **Área** | `Services/OptimizerService.cs` — `RevertPowerPlan`, `RevertPagefile`, `RevertMemoryManagement` |
| **Versão testada** | 1.3.0 |
| **Status** | ✅ Resolvido |

**Comportamento observado:**
Ao reverter os tweaks, o `OptimizerService` não lia o histórico do `SnapshotService`:
1. `RevertPowerPlan()` forçava a restauração de um GUID hardcoded (`7dd30729-79cb...`) inexistente no PC de terceiros.
2. `RevertPagefile()` forçava `c:\pagefile.sys 16 4096`, destruindo configurações de paginação gerenciada pelo Windows ou em outros drives (`D:\`).
3. `RevertMemoryManagement()` forçava `LargeSystemCache = 1` (o padrão de Windows Desktop para jogos é `0`; `1` consome toda a RAM em cache de rede).
4. `RevertIObitBloat()` tentava iniciar o serviço `IObitUnSvr` mesmo que o usuário nunca tivesse o IObit instalado.

**Comportamento esperado:**
A reversão deve restaurar o valor exato prévio capturado do sistema do usuário via `SnapshotService.GetPreviousValue(id)`.

**Resolução:**
Refatorado o `OptimizerService.cs` para a política Zero-Hardcode: detecção dinâmica do GUID original antes da troca de plano, leitura do array bruto de `PagingFiles`, preservação do `LargeSystemCache` anterior e checagem de existência de serviços antes de tentar iniciá-los.

---

### BUG-001 — Release do CI falhava com 403 ao criar a GitHub Release

| Campo | Valor |
| --- | --- |
| **Data** | 2026-10-31 |
| **Gravidade** | 🟠 Alta (P1) |
| **Área** | `.github/workflows/build.yml` — step *Create Release on Tag* |
| **Versão testada** | 1.2.2 |
| **Status** | ✅ Resolvido em 1.3.0 |

**Comportamento observado:**
Em toda tag `v*`, o step `softprops/action-gh-release@v2` terminava com:

```text
⚠️ GitHub release failed with status: 403
{"message":"Resource not accessible by integration","status":"403"}
```

O build e o upload de artifact funcionavam, mas a Release nunca era criada — o `v1.2.2`
ficou sem artefato publicado.

**Causa raiz:**
O workflow não declarava `permissions`. O `GITHUB_TOKEN` herdava o padrão somente-leitura
da organização, insuficiente para `POST /repos/{owner}/{repo}/releases`.

**Resolução:**
Adicionado `permissions: contents: write` no nível do workflow. Commit desta versão (1.3.0).

> Correções anteriores a este arquivo continuam documentadas no
> [`CHANGELOG.md`](CHANGELOG.md) (seções `### Corrigido` de cada versão).

---

## 📋 Modelo de registro

Copie o bloco abaixo ao abrir um novo bug.

````markdown
### BUG-001 — Título curto e objetivo

| Campo | Valor |
| --- | --- |
| **Data** | AAAA-MM-DD |
| **Gravidade** | 🔴 Crítica / 🟠 Alta / 🟡 Média / 🔵 Baixa |
| **Área** | `Services/HardwareScanner.cs` — detecção de ReBAR |
| **Versão testada** | 1.2.2 (FileVersion do `.exe`) |
| **Ambiente** | Windows 11 24H2 · Ryzen 5 7600X · RX 9070 XT · MSI PRO B650M-P |
| **Status** | Aberto / Em análise / Bloqueado |

**Comportamento observado:**
Descreva o que aconteceu, com números concretos (valores lidos, mensagens exibidas).

**Comportamento esperado:**
Descreva o resultado correto e por quê.

**Passos para reproduzir:**
1. Executar `dist\AMDAutoTuning.exe` como Administrador.
2. Abrir a aba _GPU & Telas_.
3. …

**Evidência:**
```text
stack trace, trecho do Event Viewer, saída de WMI/powercfg, print
```

**Hipótese / causa raiz:**
Preenchido durante a investigação.

**Resolução:**
Commit `xxxxxxx` · corrigido na versão `1.2.3` · causa raiz em uma linha.
````

---

## 📝 Template rápido para bateria de testes

Ao rodar uma bateria, registre cada achado nesta tabela antes de detalhar:

| ID | Gravidade | Área | Descrição em 1 linha | Status |
| --- | --- | --- | --- | --- |
| BUG-001 | 🟠 Alta | `.github/workflows/build.yml` | Release do CI falhava com 403 (token sem `contents: write`) | ✅ Resolvido |
| BUG-002 | 🟡 Média | `.github/workflows/build.yml` | Artefato da Release é publicado sem assinatura Authenticode | 🔴 Aberto |
| BUG-003 | 🔴 Crítica | `Services/OptimizerService.cs` | Reversão continha valores hardcoded de máquina específica | ✅ Resolvido |

---

<p align="center">
  <a href="https://eff5.com.br"><b>EFF5 Automações Inteligentes</b></a>
</p>
