# Skills do Projeto

Instruções reutilizáveis carregadas sob demanda pelo agente via ferramenta `skill`.

## Índice

| ID | Descrição | Artefatos |
| --- | --- | --- |
| [`windows-authenticode`](windows-authenticode/SKILL.md) | Assinatura Authenticode, UAC com banner azul, diagnóstico de mojibake no Subject | `scripts/sign-exe.ps1` |
| [`wpf-singlefile-app`](wpf-singlefile-app/SKILL.md) | Publish single-file .NET WPF, ícone multi-resolução, guardas de ciclo de vida do XAML | `scripts/make-icon.ps1` |
| [`amd-hardware-audit`](amd-hardware-audit/SKILL.md) | Coleta real de dados AMD (ReBAR, CPPC, EPP, VBS, monitores, PBO) | — |

## Estrutura

```text
.opencode/skills/
├── windows-authenticode/
│   ├── SKILL.md
│   └── scripts/
│       └── sign-exe.ps1
├── wpf-singlefile-app/
│   ├── SKILL.md
│   └── scripts/
│       └── make-icon.ps1
└── amd-hardware-audit/
    └── SKILL.md
```

## Como carregar

Mencione `@skill-id` no prompt, ou deixe o agente carregar quando a tarefa casar
com a `description` do frontmatter.

## Convenções ao editar

- O **ID da skill é o nome do diretório** — renomear o diretório muda o ID.
- O arquivo de entrada deve se chamar exatamente `SKILL.md`.
- Toda skill precisa de `description` no frontmatter, senão não é anunciada ao modelo.
- Os scripts `.ps1` devem ser salvos em **UTF-8 com BOM**: o Windows PowerShell 5.1
  lê arquivos sem BOM como ANSI e corrompe acentos (ver skill `windows-authenticode`).
- Conteúdo em português para consistência com o restante do repositório.
- Ao criar, renomear ou remover uma skill, atualize o índice acima **antes** de
  commitar — exigência da regra *Doc-First* documentada em `AGENTS.md`.
