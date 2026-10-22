@echo off
setlocal
cd /d "%~dp0"
echo ==============================================================================
echo   OptiFlow Vision Studio - Industrial Machine Vision IDE
echo   Starting Python Engine Sidecar and Electron Desktop Shell
echo ==============================================================================

rem Check if Python sidecar is running on port 5200
powershell -Command "$s = New-Object Net.Sockets.TcpClient; try { $s.Connect('127.0.0.1', 5200); $s.Close(); exit 0 } catch { exit 1 }"
if %ERRORLEVEL% NEQ 0 (
    echo [Launcher] Starting Python Sidecar Engine on http://127.0.0.1:5200...
    start /b "" "C:\Users\KHA\AppData\Local\Programs\Python\Python314\python.exe" engine\app.py --port 5200
    timeout /t 2 /nobreak >nul
) else (
    echo [Launcher] Python Sidecar is already online.
)

rem Run Electron directly via binary to prevent any PowerShell ExecutionPolicy issues
set "ELECTRON_BIN=%~dp0..\node_modules\electron\dist\electron.exe"
if exist "%ELECTRON_BIN%" (
    echo [Launcher] Launching Electron Desktop Shell via binary...
    start "" "%ELECTRON_BIN%" "%~dp0frontend\electron\main.cjs"
) else (
    echo [Launcher] Electron binary not found, falling back to npm.cmd...
    cd ..
    call npm.cmd run electron:start
)

endlocal
exit 0
