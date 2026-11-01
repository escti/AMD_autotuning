---
name: .NET Hardware Telemetry (LibreHardwareMonitorLib)
description: Integrar monitoramento físico de hardware em aplicações desktop .NET/C# (temperatura real Tctl/Tdie, package power em Watts, clocks por núcleo, uso de VRAM e GPU) via LibreHardwareMonitorLib, com ciclo de vida seguro, extração em single-file e fallback gracioso sem crashes de permissão.
---

# .NET Hardware Telemetry (LibreHardwareMonitorLib)

Como ler sensores físicos reais (temperatura, consumo elétrico, clock e uso) em aplicações desktop C#/.NET no Windows utilizando a biblioteca open-source **LibreHardwareMonitorLib**.

---

## 1. Por que WMI não é suficiente em hardwares modernos?

Em plataformas modernas (como AMD AM5 / Zen 4 / Zen 5 e Intel Core de 13ª/14ª geração):
- A classe padrão `root\wmi -> MSAcpi_ThermalZoneTemperature` é **omitida ou mantida estática** pelo firmware UEFI da maioria das placas-mãe desktop (MSI, ASUS, Gigabyte).
- O WMI padrão não expõe o consumo do socket (`Package Power`) nem a temperatura interna do die (`Tctl/Tdie`).
- Tentar estimar temperatura por carga de CPU gera telemetria irreal e prejudica a confiança do usuário.

A **`LibreHardwareMonitorLib`** resolve isso incorporando um driver de acesso direto a registradores MSR de hardware (WinRing0) em tempo de execução, lendo os mesmos sensores que ferramentas consolidadas como HWiNFO e FanControl leem.

---

## 2. Instalação e Configuração (`.csproj`)

Adicione o pacote oficial via NuGet:

```xml
<ItemGroup>
  <PackageReference Include="LibreHardwareMonitorLib" Version="0.9.6" />
</ItemGroup>
```

Como o app roda como executável único (`PublishSingleFile=true`), o driver nativo é gerenciado automaticamente pela biblioteca na memória temporária.

---

## 3. Padrão de Inicialização Seguro (Lifecycle & Memory Leaks)

A classe `Computer` carrega drivers e deve ser instanciada como **singleton ou mantida aberta** durante a sessão de monitoramento, sempre com descarte seguro (`Dispose` / `Close`):

```csharp
using LibreHardwareMonitor.Hardware;

public class HardwareMonitorService : IDisposable
{
    private Computer? _computer;
    private IHardware? _cpuHardware;

    public void Initialize()
    {
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true, // opcional
                IsMemoryEnabled = false,
                IsMotherboardEnabled = false,
                IsControllerEnabled = false,
                IsStorageEnabled = false
            };

            _computer.Open();
            _cpuHardware = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        }
        catch (Exception ex)
        {
            // Fallback gracioso: previne crash se o driver for bloqueado por antivírus ou falta de admin
            _computer = null;
            _cpuHardware = null;
        }
    }

    public void Dispose()
    {
        try
        {
            _computer?.Close();
        }
        catch { }
    }
}
```

---

## 4. Mapeamento de Sensores Críticos (AMD Ryzen & Intel)

Antes de ler valores, **é obrigatório chamar `hardware.Update()`** para forçar o poll do barramento físico:

### A. Temperatura Física (`Core Tctl/Tdie`)
```csharp
_cpuHardware.Update();

var tempSensor = _cpuHardware.Sensors
    .Where(s => s.SensorType == SensorType.Temperature)
    .FirstOrDefault(s => s.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)) ??
    _cpuHardware.Sensors
    .Where(s => s.SensorType == SensorType.Temperature)
    .FirstOrDefault(s => s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)) ??
    _cpuHardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);

float? tempCelsius = tempSensor?.Value;
```

### B. Consumo Real do Socket (`CPU Package Power`)
```csharp
var powerSensor = _cpuHardware.Sensors
    .Where(s => s.SensorType == SensorType.Power)
    .FirstOrDefault(s => s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase));

float? powerWatts = powerSensor?.Value;
```

### C. Clock Real Mais Alto (Peak Core Clock)
```csharp
var clockSensors = _cpuHardware.Sensors
    .Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core"))
    .ToList();

float maxClockMhz = clockSensors.Count > 0 ? clockSensors.Max(s => s.Value ?? 0) : 0;
float maxClockGhz = maxClockMhz / 1000f;
```

---

## 5. Regras de Ouro de Arquitetura

1. **Privilégios de Administrador (`requireAdministrator`):** A leitura de registradores MSR de hardware exige privilégios elevados no Windows. Sem admin, `computer.Open()` não consegue carregar o driver de baixo nível.
2. **Taxa de Atualização Amigável:** O tempo de atualização ideal é entre **1.000 ms e 1.500 ms**. Polling abaixo de 500 ms gera carga parasitária desnecessária no barramento SMU.
3. **Fallback Obrigatório:** Sempre tenha uma rotina de fallback caso a máquina não suporte o driver MSR (ex: em ambientes virtualizados ou máquinas com restrições corporativas de kernel).
