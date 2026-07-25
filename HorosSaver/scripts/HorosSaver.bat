@echo off
rem ============================================================================
rem HorosSaver.bat - Alles-in-einem (HorosCode)
rem
rem Portable publish, stille Abhaengigkeiten, App starten.
rem
rem Verwendung (aus dem Repo, Ordner scripts\):
rem   HorosSaver.bat           Build bei fehlender EXE, dann portable App starten
rem   HorosSaver.bat build     Nur portable Publish (artifacts\portable\HorosSaver\)
rem   HorosSaver.bat start     Nur starten (ohne Build)
rem
rem Im portable Paket (als starter.bat kopiert):
rem   Doppelklick               App starten (data\ + logs\ anlegen, Bootstrap still)
rem   starter.bat start         wie Doppelklick
rem
rem Hinweis: data\ und logs\ im portable Output werden beim Publish NICHT geloescht.
rem ============================================================================
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

set "MODE=default"
if /I "%~1"=="build" set "MODE=build"
if /I "%~1"=="start" set "MODE=start"

rem --- Kontext erkennen: portable Paket oder Entwickler-Repo? ---
set "IN_PORTABLE=0"
if exist "%~dp0HorosSaver.exe" (
    set "IN_PORTABLE=1"
    set "APP_DIR=%~dp0"
    goto :dispatch
)
if exist "%~dp0portable.txt" (
    set "IN_PORTABLE=1"
    set "APP_DIR=%~dp0"
    goto :dispatch
)

rem Entwickler-Repo (scripts\)
for %%I in ("%~dp0..") do set "REPO_ROOT=%%~fI"
set "PORTABLE_DIR=%REPO_ROOT%\artifacts\portable\HorosSaver"
set "PROJECT=%REPO_ROOT%\src\HorosSaver\HorosSaver.csproj"
set "APP_DIR=%PORTABLE_DIR%"
goto :dispatch

:dispatch
if "%IN_PORTABLE%"=="1" (
    if /I "%MODE%"=="build" (
        echo.
        echo [HorosSaver] Build ist nur im Entwickler-Repo moeglich.
        echo Bitte im Quellcode-Ordner ausfuehren:  scripts\HorosSaver.bat build
        echo.
        pause
        exit /b 1
    )
    set "MODE=start"
    goto :do_start
)

if /I "%MODE%"=="build" goto :do_publish
if /I "%MODE%"=="start" goto :do_start

rem default: Build wenn EXE fehlt, dann starten
if not exist "%PORTABLE_DIR%\HorosSaver.exe" (
    call :do_publish
    if errorlevel 1 exit /b 1
)
goto :do_start

rem ============================================================================
rem Publish - self-contained win-x64 nach artifacts\portable\HorosSaver\
rem ============================================================================
:do_publish
for %%I in ("%PORTABLE_DIR%") do set "PORTABLE_DIR=%%~fI"
call :init_logs "%PORTABLE_DIR%"
call :log_bootstrap "Publish gestartet (MODE=%MODE%)"

echo.
echo ==^> HorosSaver portable publish (self-contained win-x64)
echo     Projekt: %PROJECT%
echo     Output : %PORTABLE_DIR%
echo.

if not exist "%PROJECT%" (
    call :log_bootstrap "FEHLER: Projekt nicht gefunden: %PROJECT%"
    echo FEHLER: Projekt nicht gefunden: %PROJECT%
    exit /b 1
)

call :ensure_dotnet_sdk
if errorlevel 1 (
    call :log_error "%PORTABLE_DIR%" "dotnet SDK nicht verfuegbar"
    echo FEHLER: .NET 9 SDK fehlt. Details in logs\bootstrap.log
    exit /b 1
)

if not exist "%PORTABLE_DIR%" mkdir "%PORTABLE_DIR%"

call :log_bootstrap "dotnet publish ..."
dotnet publish "%PROJECT%" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:UseAppHost=true ^
    -p:PublishSingleFile=false ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:SelfContained=true ^
    -p:DebugType=None ^
    -p:DebugSymbols=false ^
    -p:PublishReadyToRun=true ^
    -o "%PORTABLE_DIR%" >> "%BOOTSTRAP_LOG%" 2>&1

if errorlevel 1 (
    call :log_error "%PORTABLE_DIR%" "dotnet publish fehlgeschlagen (Exit %ERRORLEVEL%)"
    echo FEHLER: dotnet publish fehlgeschlagen. Details in logs\publish.log und logs\bootstrap.log
    exit /b 1
)

if not exist "%PORTABLE_DIR%\HorosSaver.exe" (
    call :log_error "%PORTABLE_DIR%" "HorosSaver.exe nach Publish nicht gefunden"
    echo FEHLER: HorosSaver.exe nach Publish nicht gefunden.
    exit /b 1
)

call :verify_self_contained_output "%PORTABLE_DIR%"
if errorlevel 1 (
    call :log_error "%PORTABLE_DIR%" "Publish nicht self-contained (hostfxr/coreclr/HorosSaver.dll fehlen)"
    echo FEHLER: Publish ist nicht self-contained. hostfxr.dll, coreclr.dll oder HorosSaver.dll fehlen.
    echo Bitte dotnet publish erneut ausfuehren oder SDK pruefen.
    exit /b 1
)

call :prepare_portable_layout "%PORTABLE_DIR%"

rem starter.bat aus dieser Datei ableiten (gleiche Logik im Paket)
copy /Y "%~f0" "%PORTABLE_DIR%\starter.bat" >nul

if exist "%REPO_ROOT%\README-PORTABLE.md" (
    copy /Y "%REPO_ROOT%\README-PORTABLE.md" "%PORTABLE_DIR%\README-PORTABLE.md" >nul
)

if exist "%REPO_ROOT%\zu saven" (
    copy /Y "%REPO_ROOT%\zu saven" "%PORTABLE_DIR%\zu saven" >nul
) else if exist "%REPO_ROOT%\..\zu saven" (
    copy /Y "%REPO_ROOT%\..\zu saven" "%PORTABLE_DIR%\zu saven" >nul
)

call :log_bootstrap "Publish erfolgreich: %PORTABLE_DIR%"
echo.
echo Portable-Paket fertig: %PORTABLE_DIR%
echo Starten mit: %PORTABLE_DIR%\starter.bat
echo.
if /I "%MODE%"=="build" exit /b 0
exit /b 0

rem ============================================================================
rem Portable-Layout (data, logs, Marker) - bestehende Daten bleiben erhalten
rem ============================================================================
:prepare_portable_layout
set "TARGET=%~1"
if not exist "%TARGET%\data" mkdir "%TARGET%\data"
if not exist "%TARGET%\logs" mkdir "%TARGET%\logs"
if not exist "%TARGET%\data\.gitkeep" type nul > "%TARGET%\data\.gitkeep"
if not exist "%TARGET%\logs\.gitkeep" type nul > "%TARGET%\logs\.gitkeep"
if not exist "%TARGET%\portable.txt" echo HorosSaver portable mode> "%TARGET%\portable.txt"
exit /b 0

rem ============================================================================
rem Logs initialisieren
rem ============================================================================
:init_logs
set "LOG_ROOT=%~1"
if not exist "%LOG_ROOT%\logs" mkdir "%LOG_ROOT%\logs"
set "BOOTSTRAP_LOG=%LOG_ROOT%\logs\bootstrap.log"
set "STARTER_LOG=%LOG_ROOT%\logs\starter.log"
exit /b 0

:get_timestamp
for /f "tokens=1-3 delims=.-/ " %%a in ("%date%") do set "_D=%%c-%%b-%%a"
for /f "tokens=1-3 delims=:., " %%a in ("%time%") do (
    set "_TH=%%a"
    set "_TM=%%b"
    set "_TS=%%c"
)
set "_TH=!_TH: =0!"
if "!_TH:~1,1!"=="" set "_TH=0!_TH!"
if not defined _TM set "_TM=00"
if not defined _TS set "_TS=00"
set "TIMESTAMP=!_D! !_TH!:!_TM!:!_TS!"
exit /b 0

:log_bootstrap
if not defined BOOTSTRAP_LOG exit /b 0
call :get_timestamp
>>"%BOOTSTRAP_LOG%" echo [!TIMESTAMP!] %~1
exit /b 0

:log_starter
if not defined STARTER_LOG exit /b 0
call :get_timestamp
>>"%STARTER_LOG%" echo [!TIMESTAMP!] %~1
exit /b 0

rem ============================================================================
rem winget verfuegbar?
rem ============================================================================
:has_winget
where winget >nul 2>&1
exit /b %ERRORLEVEL%

rem ============================================================================
rem Stille winget-Installation (Ausgabe nur ins Log)
rem ============================================================================
:winget_install_silent
set "WINGET_PKG=%~1"
set "WINGET_LABEL=%~2"
call :has_winget
if errorlevel 1 (
    call :log_bootstrap "WARN: winget fehlt - %WINGET_LABEL% nicht installierbar"
    exit /b 2
)
call :log_bootstrap "winget install %WINGET_PKG% (%WINGET_LABEL%)"
winget install --id %WINGET_PKG% -e --silent --accept-package-agreements --accept-source-agreements >> "%BOOTSTRAP_LOG%" 2>&1
set "WINGET_RC=!ERRORLEVEL!"
if !WINGET_RC! EQU 0 (
    call :log_bootstrap "winget OK: %WINGET_PKG%"
    exit /b 0
)
if !WINGET_RC! EQU -1978335189 (
    call :log_bootstrap "winget: %WINGET_PKG% bereits installiert"
    exit /b 0
)
if !WINGET_RC! EQU 2316632107 (
    call :log_bootstrap "winget: %WINGET_PKG% bereits installiert (2316632107)"
    exit /b 0
)
call :log_bootstrap "winget FEHLER %WINGET_PKG% Exit !WINGET_RC!"
exit /b 1

rem ============================================================================
rem .NET 9 SDK fuer Publish
rem ============================================================================
:ensure_dotnet_sdk
where dotnet >nul 2>&1
if not errorlevel 1 (
    dotnet --version 2>nul | findstr /R "^9\." >nul
    if not errorlevel 1 (
        call :log_bootstrap ".NET 9 SDK bereits vorhanden - uebersprungen"
        echo(.NET 9 SDK bereits vorhanden - uebersprungen
        exit /b 0
    )
    dotnet --list-sdks 2>nul | findstr /R " 9\." >nul
    if not errorlevel 1 (
        call :log_bootstrap ".NET 9 SDK bereits vorhanden - uebersprungen"
        echo(.NET 9 SDK bereits vorhanden - uebersprungen
        exit /b 0
    )
)

echo(.NET 9 SDK fehlt - versuche stille Installation ...
call :winget_install_silent "Microsoft.DotNet.SDK.9" ".NET 9 SDK"
set "SDK_WINGET_RC=!ERRORLEVEL!"
if !SDK_WINGET_RC! NEQ 0 (
    call :log_bootstrap "FEHLER: .NET 9 SDK konnte nicht installiert werden (winget Exit !SDK_WINGET_RC!)"
    echo FEHLER: .NET 9 SDK fehlt und winget-Installation fehlgeschlagen.
    echo Bitte manuell installieren: https://dotnet.microsoft.com/download/dotnet/9.0
    exit /b 1
)

rem PATH nach Installation aktualisieren (typische Installationspfade)
if exist "%ProgramFiles%\dotnet\dotnet.exe" set "PATH=%ProgramFiles%\dotnet;%PATH%"
if exist "%ProgramFiles(x86)%\dotnet\dotnet.exe" set "PATH=%ProgramFiles(x86)%\dotnet;%PATH%"

where dotnet >nul 2>&1
if errorlevel 1 (
    call :log_bootstrap "FEHLER: dotnet nach Installation nicht im PATH"
    exit /b 1
)

call :log_bootstrap ".NET SDK installiert/gefunden"
echo(.NET SDK bereit.
exit /b 0

rem ============================================================================
rem Self-contained? (hostfxr + coreclr + HorosSaver.dll neben EXE)
rem ============================================================================
:is_self_contained
if "%~1"=="" (
    set "SC_DIR=%APP_DIR%"
) else (
    set "SC_DIR=%~1"
)
if not exist "%SC_DIR%\HorosSaver.exe" exit /b 1
if not exist "%SC_DIR%\hostfxr.dll" exit /b 1
if not exist "%SC_DIR%\coreclr.dll" exit /b 1
if not exist "%SC_DIR%\HorosSaver.dll" exit /b 1
exit /b 0

rem ============================================================================
rem Nach Publish: self-contained Output validieren
rem ============================================================================
:verify_self_contained_output
set "SC_DIR=%~1"
call :is_self_contained "%SC_DIR%"
if errorlevel 1 (
    call :log_bootstrap "FEHLER: Output nicht self-contained in %SC_DIR%"
    exit /b 1
)
call :log_bootstrap "Self-contained Output verifiziert: %SC_DIR%"
exit /b 0

rem ============================================================================
rem .NET 9 Desktop Runtime (nur wenn Output nicht self-contained)
rem ============================================================================
:ensure_dotnet_runtime
call :is_self_contained
if not errorlevel 1 (
    call :log_bootstrap "Self-contained Runtime vorhanden - uebersprungen"
    echo(.NET Runtime self-contained bereits vorhanden - uebersprungen
    exit /b 0
)

call :log_bootstrap "WARN: Output nicht self-contained - Desktop Runtime erforderlich"

where dotnet >nul 2>&1
if not errorlevel 1 (
    dotnet --list-runtimes 2>nul | findstr /I /C:"Microsoft.WindowsDesktop.App 9" >nul
    if not errorlevel 1 (
        call :log_bootstrap ".NET 9 Desktop Runtime bereits vorhanden - uebersprungen"
        echo(.NET 9 Desktop Runtime bereits vorhanden - uebersprungen
        exit /b 0
    )
)

echo(.NET 9 Desktop Runtime fehlt - stille Installation ...
call :winget_install_silent "Microsoft.DotNet.DesktopRuntime.9" ".NET 9 Desktop Runtime"
set "RT_WINGET_RC=!ERRORLEVEL!"
if !RT_WINGET_RC! EQU 2 (
    call :log_starter "FEHLER: winget fehlt - .NET Runtime nicht installierbar"
    echo FEHLER: winget fehlt - .NET 9 Desktop Runtime bitte manuell installieren.
    echo https://dotnet.microsoft.com/download/dotnet/9.0
    exit /b 1
)
if !RT_WINGET_RC! NEQ 0 (
    call :log_starter "FEHLER: .NET 9 Desktop Runtime Installation fehlgeschlagen"
    echo FEHLER: .NET 9 Desktop Runtime konnte nicht installiert werden.
    echo Details in logs\bootstrap.log - manuell: https://dotnet.microsoft.com/download/dotnet/9.0
    exit /b 1
)

if exist "%ProgramFiles%\dotnet\dotnet.exe" set "PATH=%ProgramFiles%\dotnet;%PATH%"

dotnet --list-runtimes 2>nul | findstr /I /C:"Microsoft.WindowsDesktop.App 9" >nul
if errorlevel 1 (
    call :log_starter "FEHLER: .NET 9 Desktop Runtime nach Installation nicht gefunden"
    echo FEHLER: .NET 9 Desktop Runtime nach Installation nicht erkannt.
    exit /b 1
)

echo(.NET 9 Desktop Runtime installiert.
exit /b 0

rem ============================================================================
rem Dev-Repo: bei framework-dependent Output erneut publishen
rem ============================================================================
:try_republish_if_needed
if "%IN_PORTABLE%"=="1" exit /b 1
if not defined PROJECT exit /b 1
if not exist "%PROJECT%" exit /b 1

call :is_self_contained
if not errorlevel 1 exit /b 0

call :log_bootstrap "Output nicht self-contained - versuche Republish ..."
echo.
echo ==^> Portable Output unvollstaendig - Republish (self-contained win-x64)
echo.

set "SAVED_MODE=%MODE%"
set "MODE=build"
call :do_publish
set "MODE=%SAVED_MODE%"
if errorlevel 1 exit /b 1

call :is_self_contained
if errorlevel 1 exit /b 1
exit /b 0

rem ============================================================================
rem Visual C++ Redistributable x64 (wenn VCRUNTIME140.dll fehlt)
rem ============================================================================
:ensure_vcredist
if exist "%SystemRoot%\System32\VCRUNTIME140.dll" (
    call :log_bootstrap "VC++ Runtime (VCRUNTIME140.dll) vorhanden - uebersprungen"
    echo VC++ Runtime bereits vorhanden - uebersprungen
    exit /b 0
)

echo VC++ Redistributable x64 fehlt - stille Installation ...
call :winget_install_silent "Microsoft.VCRedist.2015+.x64" "VC++ Redistributable x64"
set "VC_WINGET_RC=!ERRORLEVEL!"
if !VC_WINGET_RC! EQU 2 (
    call :log_bootstrap "WARN: winget fehlt - VC++ nicht installiert"
    echo WARNUNG: winget fehlt - VC++ Redistributable x64 ggf. manuell installieren.
    exit /b 0
)
if !VC_WINGET_RC! NEQ 0 (
    call :log_bootstrap "WARN: VC++ Installation fehlgeschlagen"
    echo WARNUNG: VC++ konnte nicht automatisch installiert werden - siehe logs\bootstrap.log
    exit /b 0
)

if exist "%SystemRoot%\System32\VCRUNTIME140.dll" (
    echo VC++ Redistributable installiert.
) else (
    call :log_bootstrap "WARN: VCRUNTIME140.dll nach Installation nicht gefunden"
    echo WARNUNG: VCRUNTIME140.dll weiterhin nicht gefunden.
)
exit /b 0

rem ============================================================================
rem Bootstrap vor App-Start (Republish, Runtime + VC++)
rem ============================================================================
:bootstrap_dependencies
call :log_bootstrap "Bootstrap gestartet (APP_DIR=%APP_DIR%)"

if "%IN_PORTABLE%"=="0" (
    call :try_republish_if_needed
    if errorlevel 1 (
        call :log_bootstrap "Republish fehlgeschlagen oder Output weiterhin nicht self-contained"
    )
)

call :ensure_dotnet_runtime
if errorlevel 1 exit /b 1
call :ensure_vcredist
call :log_bootstrap "Bootstrap abgeschlossen"
exit /b 0

rem ============================================================================
rem App starten (portable Modus)
rem ============================================================================
:do_start
for %%I in ("%APP_DIR%") do set "APP_DIR=%%~fI"
cd /d "%APP_DIR%"

call :prepare_portable_layout "%APP_DIR%"
call :init_logs "%APP_DIR%"

set "HOROSSAVER_PORTABLE=1"
set "HOROSSAVER_DATA_ROOT=%APP_DIR%\data"
set "EXE=%APP_DIR%\HorosSaver.exe"

call :log_starter "HorosSaver starter (APP_DIR=%APP_DIR%, MODE=%MODE%)"

if not exist "%EXE%" (
    call :log_starter "ERROR: HorosSaver.exe not found"
    echo.
    echo HorosSaver.exe nicht gefunden in:
    echo   %APP_DIR%
    echo.
    if "%IN_PORTABLE%"=="0" (
        echo Bitte zuerst bauen:  scripts\HorosSaver.bat build
    ) else (
        echo Bitte das Paket neu bauen:  scripts\HorosSaver.bat build
    )
    echo.
    pause
    exit /b 1
)

call :bootstrap_dependencies
if not "!ERRORLEVEL!"=="0" goto :bootstrap_failed
goto :do_start_launch_gate

:bootstrap_failed
    echo(
    echo Bootstrap fehlgeschlagen. Details in logs\bootstrap.log und logs\starter.log
    echo HorosSaver.exe wird NICHT gestartet (kein .NET-Dialog).
    pause
    exit /b 2

:do_start_launch_gate
if not exist "%APP_DIR%\hostfxr.dll" goto :start_needs_runtime
if not exist "%APP_DIR%\coreclr.dll" goto :start_needs_runtime
if not exist "%APP_DIR%\HorosSaver.dll" goto :start_needs_runtime
call :log_starter "Start-Freigabe: self-contained Output OK"
goto :start_launch

:start_needs_runtime
where dotnet >nul 2>&1
if errorlevel 1 (
    call :log_starter "FEHLER: Start blockiert - weder self-contained noch dotnet im PATH"
    echo FEHLER: HorosSaver kann nicht gestartet werden.
    echo Weder self-contained Runtime noch installiertes .NET 9 Desktop Runtime gefunden.
    echo Bitte scripts\HorosSaver.bat build ausfuehren oder .NET 9 Desktop Runtime installieren.
    pause
    exit /b 4
)
dotnet --list-runtimes 2>nul | findstr /I /C:"Microsoft.WindowsDesktop.App 9" >nul
if errorlevel 1 (
    call :log_starter "FEHLER: Start blockiert - .NET 9 Desktop Runtime fehlt"
    echo FEHLER: HorosSaver kann nicht gestartet werden.
    echo(.NET 9 Desktop Runtime ist nicht installiert und Output ist nicht self-contained.
    echo Bitte scripts\HorosSaver.bat build ausfuehren oder Runtime installieren.
    pause
    exit /b 4
)
call :log_starter "Start-Freigabe: framework-dependent mit Desktop Runtime 9"

:start_launch
start "" "%EXE%"
if errorlevel 1 (
    call :log_starter "ERROR: start failed with %ERRORLEVEL%"
    echo.
    echo Start fehlgeschlagen. Details in logs\starter.log und logs\horossaver-*.log
    echo.
    pause
    exit /b 3
)

call :log_starter "HorosSaver.exe started"
exit /b 0

rem ============================================================================
rem Fehler ins Log schreiben (Publish)
rem ============================================================================
:log_error
set "LOG_DIR=%~1\logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"
call :get_timestamp
>>"%LOG_DIR%\publish.log" echo [!TIMESTAMP!] %~2
exit /b 0
