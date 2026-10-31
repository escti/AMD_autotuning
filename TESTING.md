# 🧪 Guia Operacional de Testes & QA — AMD AutoTuning

Este guia padroniza os procedimentos de teste para desenvolvedores e agentes de IA,
garantindo que toda nova versão seja validada com rigor sem colocar em risco o sistema do usuário.

---

## 🏗️ A Pirâmide de Testes do Projeto

```text
       ▲
      / \     Nível 3: Windows Sandbox (VM Descartável em 10s — 0 risco)
     /───\
    /     \   Nível 2: UI Automation & Screenshots (Navegação em Abas)
   /───────\
  /─────────\ Nível 1: Test Suite Automatizada (tools\test-release.ps1)
```

---

## 1. Teste Mestre em 1 Comando (`tools\test-release.ps1`)

Executa em ~15 segundos as 6 validações obrigatórias antes de qualquer commit ou release:

```powershell
.\tools\test-release.ps1
```

### O que o teste mestre valida:
1. **Compilação Release:** `dotnet build` com 0 erros e 0 warnings.
2. **Integridade de Encoding:** Garante que todo `.ps1` possui UTF-8 com BOM (`EF BB BF`), prevenindo corrupção de acentos no UAC.
3. **Sincronia Doc-First:** Checa se a versão bate entre `AMDAutoTuning.csproj`, `CHANGELOG.md` e o badge do `README.md`.
4. **Catálogo de Bugs:** Valida se `BUGS.md` está presente e íntegro.
5. **Assinatura Authenticode:** Inspeciona os bytes do Subject no `.exe` para garantir ausência de mojibake (`C3 83`) e status `Valid`.
6. **Simulação Segura (`--dry-run`):** Inicia o executável em modo simulação, valida que não houve crash na inicialização e confirma a gravação de telemetria em disco.

---

## 2. Modo Simulação Segura (`--dry-run`)

Para testar o aplicativo diretamente na máquina de desenvolvimento sem alterar nenhuma chave de registro nem parar serviços:

```powershell
.\dist\AMDAutoTuning.exe --dry-run
```

- **Comportamento:** A interface exibe a tarja âmbar `🛡️ MODO SIMULAÇÃO (DRY-RUN)` no topo.
- **Segurança:** Qualquer clique em "Otimizar Tudo", "Ativar" ou "Reverter" apenas grava no log a ação que seria executada, retornando sucesso simulado sem tocar no sistema operacional.

---

## 3. Teste em Ambiente Descartável (Windows Sandbox)

Para testar a aplicação real com privilégios de Administrador sem deixar resíduos no seu computador:

1. Dê duplo clique no arquivo:
   ```text
   tools\AMDAutoTuning-Sandbox.wsb
   ```
   *(Ou execute no terminal: `WindowsSandbox.exe tools\AMDAutoTuning-Sandbox.wsb`)*

2. O Windows abrirá uma máquina virtual descartável em ~10 segundos.
3. A pasta `dist\` é montada como somente-leitura dentro do Sandbox e o app inicia automaticamente com elevação.
4. Você pode aplicar todos os tweaks, reverter e testar à vontade.
5. Ao fechar o Sandbox, **o ambiente é 100% destruído**.

*Requisito: O recurso "Área Restrita do Windows" (Windows Sandbox) deve estar ativado nos Recursos do Windows.*

---

## 4. Automação de Interface (UI Automation)

Para navegar automaticamente pelas abas, abrir modais e gerar capturas de tela:

```powershell
# Execute em um terminal como Administrador:
.\tools\test-ui-navigation.ps1
```

Os prints de cada tela são salvos em `artifacts\screenshots\`:
- `01_painel_geral.png`
- `02_os_latencia.png`
- `03_cpu_ram.png`
- `04_gpu_telas.png`
- `05_drivers_armazenamento.png`
- `06_historico_reversao.png`
- `07_pre_requisitos.png`

> ⚠️ **Aviso de UIPI (User Interface Privilege Isolation):** Como o app requer elevação (`requireAdministrator`), o Windows bloqueia terminais não elevados de inspecionarem a árvore UIA de janelas elevadas. Para o script conseguir clicar nos elementos e capturar telas, o PowerShell deve ser aberto como Administrador.

---

## 5. Logs & Telemetria em Disco

Todos os eventos, auditorias WMI, comandos do sistema e eventuais falhas são gravados em:

```text
%APPDATA%\AMDAutoTuning\Logs\app-YYYY-MM-DD.log
```

- **Pela Interface:** Clique no botão `📁 Logs em Disco` no rodapé da janela para abrir a pasta diretamente no Windows Explorer.
- **Crash Guard:** Se ocorrer qualquer exceção não tratada na interface ou em threads de fundo, o stack trace completo é salvo no arquivo de log antes de encerrar.

---

## 📋 Checklist de Aceite para Novas Releases

Antes de marcar uma tag de release:
- [ ] `.\tools\test-release.ps1` finalizou com `6 PASS / 0 FAIL`.
- [ ] O arquivo `%APPDATA%\AMDAutoTuning\Logs\` registrou a sessão sem erros fatais.
- [ ] Toda inconsistência encontrada foi registrada em `BUGS.md` com status ou resolução.
- [ ] A checklist Doc-First do `AGENTS.md` foi cumprida.
