---
name: AMD Hardware Audit (AM5 / Zen 4 / RDNA)
description: Auditar e diagnosticar hardware AMD no Windows por WMI, EventLog e powercfg - Resizable BAR real, topologia de monitores (evitando falso positivo de dock USB DisplayLink), nucleos CPPC Gold/Silver, EPP, VBS/HVCI, limites PPT/TDC/EDC e perfis de PBO. Use ao escrever ferramentas de diagnostico/tuning para Ryzen 7000/9000, Radeon RX 7000/9000, ou ao investigar gargalos silenciosos de performance.
---

# AMD Hardware Audit (AM5 / Zen 4 / RDNA)

Como obter dados **reais** sem depender de ferramentas de terceiros nem de texto
localizado do Windows (nomes de contador mudam com o idioma do sistema).

## 1. Resizable BAR / Smart Access Memory

Nao existe API direta. O teste confiavel e inspecionar a janela de memoria PCI
alocada para a GPU: se for >= 1 GB, o ReBAR esta ativo; se for 256 MB, esta preso na
janela legada.

```csharp
using var searcher = new ManagementObjectSearcher(
    "SELECT StartingAddress, EndingAddress FROM Win32_DeviceMemoryAddress");

foreach (ManagementObject obj in searcher.Get())
{
    long start = long.Parse(obj["StartingAddress"].ToString()!);
    long end   = long.Parse(obj["EndingAddress"].ToString()!);
    long range = end - start + 1;
    if (range >= 1073741824L) { /* ReBAR ativo */ }
}
```

Para confirmar que a faixa pertence a GPU, cruze com `Win32_PNPAllocatedResource`
filtrando pela `DeviceID` do adaptador (`PCI\VEN_1002&DEV_...`).

Se estiver em 256 MB, a correcao e na BIOS: `Above 4G Decoding` + `Re-Size BAR Support`.

## 2. Topologia de monitores (evite o falso positivo classico)

**Nunca** use `Win32_VideoController` para contar monitores. Docks USB (Dell D6000,
D3100, DisplayLink em geral) registram adaptadores de video virtuais mesmo com as
portas de video vazias, fazendo o diagnostico reportar um segundo monitor inexistente.

Use o barramento EDID dos monitores **ativos**:

```csharp
// Nomes e fabricante
var ids = new ManagementObjectSearcher(@"root\wmi",
    "SELECT InstanceName, UserFriendlyName, ManufacturerName FROM WmiMonitorID WHERE Active = True");

// Tecnologia de conexao e instancia
var conn = new ManagementObjectSearcher(@"root\wmi",
    "SELECT InstanceName, VideoOutputTechnology FROM WmiMonitorConnectionParams WHERE Active = True");
```

`VideoOutputTechnology`: `10` = DisplayPort, `5` = HDMI, `4` = DVI, `1` = VGA.
Se o `InstanceName` contem `USB`/`DisplayLink`, o monitor esta na dock.

Para provar que a tela esta na GPU, leia o pai PnP do dispositivo de monitor:
`DEVPKEY_Device_Parent` deve ser `PCI\VEN_1002&DEV_...` do adaptador dedicado.

## 3. Nucleos CPPC Gold / Silver (Zen 4)

O kernel do Windows registra a classificacao de qualidade de silicio por nucleo no
event log `Microsoft-Windows-Kernel-Processor-Power`, **Event ID 55**.

```csharp
string query = "*[System[Provider[@Name='Microsoft-Windows-Kernel-Processor-Power'] and (EventID=55)]]";
var logQuery = new EventLogQuery("System", PathType.LogName, query) { ReverseDirection = true };
using var reader = new EventLogReader(logQuery);
```

Cada evento traz `Number` (indice logico) e `MaximumPerformancePercent`. O maior
percentual marca o **Gold core**; o segundo maior, o **Silver**. Esses sao os nucleos
que o `amdppm.sys` prefere para threads prioritarias como jogos.

Se nao houver eventos, o driver de chipset AMD pode nao estar instalado — trate como
`Info`, nao como erro.

## 4. EPP (Energy Performance Preference)

EPP `0` = maximo desempenho agressivo. Em Zen 4 isso **impede** os nucleos ociosos de
entrarem em CC6, aquecendo o chip e reduzindo a margem para boost single-core.

```powershell
powercfg /query SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 36687f9e-e3a5-4dbf-b1dc-15eb381c6863
```

`33` e o valor equilibrado recomendado. O plano "Equilibrado" (`381b4222-...`) e o
correto para Ryzen 7000/9000; "Desempenho Maximo" piora o boost.

## 5. VBS / HVCI (Integridade de Memoria)

```csharp
// Status geral (2 = rodando)
var dg = new ManagementObjectSearcher(@"root\Microsoft\Windows\DeviceGuard",
    "SELECT VirtualizationBasedSecurityStatus FROM Win32_DeviceGuard");

// HVCI explicitamente
Registry.LocalMachine.OpenSubKey(
    @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity")
    ?.GetValue("Enabled");
```

Impacto no Zen 4: cada alocacao de kernel gera VM Exit e esvazia o BTB, custando
5-10% no 1% low de jogos competitivos. Reporte como `Info` (trade-off), nunca como erro.

## 6. Limites de fabrica por classe (Zen 4)

| Classe | Exemplos | PPT | TDC | EDC | TjMax |
| --- | --- | --- | --- | --- | --- |
| 105W TDP | 7600X, 7700X | 142 W | 110 A | 170 A | 95 C |
| 65W TDP | 7600, 7700, 7900 | 88 W | 75 A | 150 A | 95 C |
| 170W TDP | 7900X, 7950X | 230 W | 160 A | 225 A | 95 C |
| 120W X3D | 7800X3D, 7950X3D | 162 W | 120 A | 180 A | 89 C |

## 7. Perfis de tuning que fazem sentido

| Perfil | PPT | TjMax | Curve Optimizer | Uso |
| --- | --- | --- | --- | --- |
| Gaming single-core | 130 W | 85 C | -15 (gold/silver), -25 demais, +200 MHz boost override | FPS competitivo |
| Balanced sweet spot | 105 W | 85 C | -20 all-core | Uso diario, ~15-20 C mais frio |
| Eco workstation | 88 W | 75 C | -30 all-core | Silencio, ~40% menos consumo |

## 8. Memoria DDR5 no AM5

- `UCLK = MCLK` (1:1) e obrigatorio; `MCLK/2` dobra a latencia efetiva.
- `FCLK` em 2000 MHz e o alvo estavel universal no Zen 4.
- Perfil JEDEC (1.10 V, ex.: DDR5-5200) versus EXPO/XMP (1.25-1.35 V, 5600-6000).
  Leia `ConfiguredClockSpeed` e `ConfiguredVoltage` em `Win32_PhysicalMemory`.

## 9. Regras de seguranca ao automatizar tweaks

1. Leia o valor anterior **antes** de escrever e registre em um ledger local.
2. Toda acao precisa de reversao simetrica testada.
3. Ofereca criar Ponto de Restauracao do Windows antes de lotes.
4. Nunca aplique overclock ou mudanca de tensao automaticamente: exiba o preset e
   deixe o usuario aplicar na BIOS.
5. Prefira chaves de politica (`HKLM\SOFTWARE\Policies\...`) a desativar servicos
   quando possivel: e o padrao que a Microsoft suporta e reverte.
