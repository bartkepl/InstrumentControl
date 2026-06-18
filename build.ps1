param(
    [string]$Configuration = "Release",
    [switch]$Debug
)

if ($Debug) { $Configuration = "Debug" }

function jp {
    $p = $args[0]
    for ($i = 1; $i -lt $args.Count; $i++) { $p = Join-Path $p $args[$i] }
    return $p
}

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$OutDir = jp $Root "bin" $Configuration
# Docelowa platforma (musi odpowiadac TargetFramework z Directory.Build.props).
$Tfm = "net10.0-windows"

Write-Host ""
Write-Host "=== InstrumentControl Build ===" -ForegroundColor Cyan
Write-Host "Konfiguracja: $Configuration"
Write-Host "Katalog wyjsciowy: $OutDir"
Write-Host ""

Write-Host "1. Budowanie Core..." -ForegroundColor Yellow
dotnet build (jp $Root "src" "InstrumentControl.Core" "InstrumentControl.Core.csproj") -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Blad budowania Core!"; exit 1 }
Write-Host "   OK" -ForegroundColor Green

Write-Host "2. Budowanie HP34401A..." -ForegroundColor Yellow
dotnet build (jp $Root "instruments" "HP34401A" "HP34401A.csproj") -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Blad budowania HP34401A!"; exit 1 }
Write-Host "   OK" -ForegroundColor Green

Write-Host "3. Budowanie Agilent34970A..." -ForegroundColor Yellow
dotnet build (jp $Root "instruments" "Agilent34970A" "Agilent34970A.csproj") -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Blad budowania Agilent34970A!"; exit 1 }
Write-Host "   OK" -ForegroundColor Green

Write-Host "4. Budowanie Keithley2000..." -ForegroundColor Yellow
dotnet build (jp $Root "instruments" "Keithley2000" "Keithley2000.csproj") -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Blad budowania Keithley2000!"; exit 1 }
Write-Host "   OK" -ForegroundColor Green

Write-Host "5. Budowanie ItechIT6922B..." -ForegroundColor Yellow
dotnet build (jp $Root "instruments" "ItechIT6922B" "ItechIT6922B.csproj") -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Blad budowania ItechIT6922B!"; exit 1 }
Write-Host "   OK" -ForegroundColor Green

Write-Host "6. Budowanie RTB2004..." -ForegroundColor Yellow
dotnet build (jp $Root "instruments" "RTB2004" "RTB2004.csproj") -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Blad budowania RTB2004!"; exit 1 }
Write-Host "   OK" -ForegroundColor Green

Write-Host "7. Budowanie CTSChamber..." -ForegroundColor Yellow
dotnet build (jp $Root "instruments" "CTSChamber" "CTSChamber.csproj") -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Blad budowania CTSChamber!"; exit 1 }
Write-Host "   OK" -ForegroundColor Green

Write-Host "8. Budowanie RigolDS1000Z..." -ForegroundColor Yellow
dotnet build (jp $Root "instruments" "RigolDS1000Z" "RigolDS1000Z.csproj") -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Blad budowania RigolDS1000Z!"; exit 1 }
Write-Host "   OK" -ForegroundColor Green

Write-Host "9. Budowanie i publikowanie aplikacji WPF..." -ForegroundColor Yellow
dotnet publish (jp $Root "src" "InstrumentControl.App" "InstrumentControl.App.csproj") `
    -c $Configuration -r win-x64 --self-contained true -o $OutDir --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Blad publikowania!"; exit 1 }
Write-Host "   OK" -ForegroundColor Green

# Copy instrument DLLs to instruments/ subfolder next to EXE
$InstrumentsOut = jp $OutDir "instruments"
New-Item -ItemType Directory -Force -Path $InstrumentsOut | Out-Null

$hp = jp $Root "instruments" "HP34401A" "bin" $Configuration $Tfm "HP34401A.dll"
if (Test-Path $hp) {
    Copy-Item $hp $InstrumentsOut -Force
    Write-Host "   Skopiowano: HP34401A.dll" -ForegroundColor Green
}

$ag = jp $Root "instruments" "Agilent34970A" "bin" $Configuration $Tfm "Agilent34970A.dll"
if (Test-Path $ag) {
    Copy-Item $ag $InstrumentsOut -Force
    Write-Host "   Skopiowano: Agilent34970A.dll" -ForegroundColor Green
}

$k2 = jp $Root "instruments" "Keithley2000" "bin" $Configuration $Tfm "Keithley2000.dll"
if (Test-Path $k2) {
    Copy-Item $k2 $InstrumentsOut -Force
    Write-Host "   Skopiowano: Keithley2000.dll" -ForegroundColor Green
}

$it = jp $Root "instruments" "ItechIT6922B" "bin" $Configuration $Tfm "ItechIT6922B.dll"
if (Test-Path $it) {
    Copy-Item $it $InstrumentsOut -Force
    Write-Host "   Skopiowano: ItechIT6922B.dll" -ForegroundColor Green
}

$rtb = jp $Root "instruments" "RTB2004" "bin" $Configuration $Tfm "RTB2004.dll"
if (Test-Path $rtb) {
    Copy-Item $rtb $InstrumentsOut -Force
    Write-Host "   Skopiowano: RTB2004.dll" -ForegroundColor Green
}

$cts = jp $Root "instruments" "CTSChamber" "bin" $Configuration $Tfm "CTSChamber.dll"
if (Test-Path $cts) {
    Copy-Item $cts $InstrumentsOut -Force
    Write-Host "   Skopiowano: CTSChamber.dll" -ForegroundColor Green
}

$rigol = jp $Root "instruments" "RigolDS1000Z" "bin" $Configuration $Tfm "RigolDS1000Z.dll"
if (Test-Path $rigol) {
    Copy-Item $rigol $InstrumentsOut -Force
    Write-Host "   Skopiowano: RigolDS1000Z.dll" -ForegroundColor Green
}

$exe = jp $OutDir "InstrumentControl.exe"
Write-Host ""
Write-Host "=== Build zakonczony pomyslnie ===" -ForegroundColor Green
Write-Host "Uruchomienie: $exe" -ForegroundColor Cyan
Write-Host ""
