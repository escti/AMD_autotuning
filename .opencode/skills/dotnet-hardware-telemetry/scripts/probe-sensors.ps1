<#
.SYNOPSIS
    Script de diagnostico de telemetria da skill dotnet-hardware-telemetry.
    Varre e lista todos os sensores de hardware detectados via WMI e performance counters.

.DESCRIPTION
    Auxilia no mapeamento de sensores de temperatura, carga e frequencia no Windows.
#>

param(
    [switch]$Detailed
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "       Hardware Telemetry Sensor Prober (Diagnostic)        " -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Informacoes do Processador
Write-Host "`n[1/3] Informacoes do Processador (WMI)..." -ForegroundColor Cyan
Get-CimInstance -ClassName Win32_Processor | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed | Format-List

# 2. ACPI Thermal Zone (se exposto pelo firmware)
Write-Host "[2/3] ACPI Thermal Zone (root\wmi)..." -ForegroundColor Cyan
$thermalZones = Get-CimInstance -Namespace "root\wmi" -ClassName "MSAcpi_ThermalZoneTemperature" -ErrorAction SilentlyContinue
if ($thermalZones) {
    foreach ($tz in $thermalZones) {
        $celsius = ($tz.CurrentTemperature - 2732) / 10.0
        Write-Host "  ThermalZone $($tz.InstanceName): $celsius C" -ForegroundColor Green
    }
} else {
    Write-Host "  ACPI Thermal Zone nao exposto pela placa-mae (Padrao em plataformas desktop modernas AM5/AM4)." -ForegroundColor Yellow
    Write-Host "  Leituras de Tctl/Tdie exigem acesso a registradores MSR via driver (LibreHardwareMonitorLib)." -ForegroundColor DarkGray
}

# 3. Informacoes de Energia e Frequencia
Write-Host "`n[3/3] Parametros de Boost e Frequencia (powercfg)..." -ForegroundColor Cyan
$activeScheme = (powercfg /getactivescheme)
Write-Host "  Plano Ativo: $activeScheme" -ForegroundColor DarkCyan

Write-Host "`nDiagnostico de telemetria concluido com sucesso!" -ForegroundColor Green
