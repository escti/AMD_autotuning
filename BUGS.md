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
| 🔴 Abertos | 0 |
| 🟠 Em análise / Bloqueados | 0 |
| 🟢 Resolvidos | 0 |

_Última atualização: 2026-10-31 — é esperado, o catálogo acabou de ser criado._

---

## 🔴 Abertos

_Nenhum bug registrado no momento._

---

## 🟠 Em análise / Bloqueados

_Nenhum item aguardando investigação ou dependência externa._

---

## 🟢 Resolvidos

_Nenhum item registrado desde a criação deste catálogo._

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
| — | — | — | _nenhum achado ainda_ | — |

---

<p align="center">
  <a href="https://eff5.com.br"><b>EFF5 Automações Inteligentes</b></a>
</p>
