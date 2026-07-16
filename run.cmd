@echo off
REM ============================================================================
REM  InstrumentControl - build + run
REM
REM  Uzycie:
REM    run.cmd            -> build Release (build.ps1) i uruchomienie aplikacji
REM    run.cmd debug       -> build Debug i uruchomienie
REM    run.cmd release      -> build Release i uruchomienie (jawnie)
REM ============================================================================
setlocal EnableExtensions

set "ROOT=%~dp0"
set "CONFIG=Release"

if /I "%~1"=="debug"   set "CONFIG=Debug"
if /I "%~1"=="release" set "CONFIG=Release"

REM --- Upewnij sie, ze "dotnet" na PATH wskazuje na instalacje z SDK -----------
REM Na niektorych maszynach domyslny "dotnet" z PATH to instalacja tylko z
REM runtime (bez SDK), a prawdziwe SDK siedzi gdzie indziej. Jesli tak jest,
REM dokladamy jego katalog na poczatek PATH tylko na czas tego skryptu.
set "HAVE_SDK="
for /f "delims=" %%v in ('dotnet --list-sdks 2^>nul') do set "HAVE_SDK=1"

if not defined HAVE_SDK (
    if exist "D:\Programy\dotnet-sdk-10.0.301-win-x64\dotnet.exe" (
        echo [run.cmd] "dotnet" z PATH nie ma zainstalowanego SDK - uzywam D:\Programy\dotnet-sdk-10.0.301-win-x64
        set "PATH=D:\Programy\dotnet-sdk-10.0.301-win-x64;%PATH%"
    ) else (
        echo [run.cmd] UWAGA: nie znaleziono .NET SDK. Build prawdopodobnie sie nie powiedzie.
        echo [run.cmd] Pobierz SDK: https://dotnet.microsoft.com/download
    )
)

echo.
echo === InstrumentControl: build (%CONFIG%) ===
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%build.ps1" -Configuration %CONFIG%
if errorlevel 1 (
    echo.
    echo [run.cmd] Build nie powiodl sie.
    exit /b 1
)

set "EXE=%ROOT%bin\%CONFIG%\InstrumentControl.exe"
if not exist "%EXE%" (
    echo.
    echo [run.cmd] Nie znaleziono pliku wykonywalnego: %EXE%
    exit /b 1
)

echo.
echo === Uruchamianie: %EXE% ===
start "" "%EXE%"

endlocal
