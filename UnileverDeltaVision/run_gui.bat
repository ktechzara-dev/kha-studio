@echo off
title Unilever 3-Delta Robot Vision System (510 SPP)
color 0b
echo ======================================================================
echo    UNILEVER 3-DELTA ROBOT VISION SYSTEM (510 SPP STANDALONE)
echo    High-Speed Industrial Computer Vision - PyQt6, OpenCV ^& Schneider PLC
echo ======================================================================
echo.

cd /d "%~dp0"

set "PYTHON_EXE=C:\Users\KHA\AppData\Local\Programs\Python\Python314\python.exe"
if not exist "%PYTHON_EXE%" (
    set "PYTHON_EXE=python"
)

echo [+] Using Python: %PYTHON_EXE%
"%PYTHON_EXE%" app_gui.py

if %errorlevel% neq 0 (
    echo.
    echo [!] Application exited with error code %errorlevel%.
    pause
)
