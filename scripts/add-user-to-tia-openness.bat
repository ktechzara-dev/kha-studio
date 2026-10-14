@echo off
echo ========================================================
echo Cap quyen truy cap Siemens TIA Openness cho User hien tai
echo ========================================================
net localgroup "Siemens TIA Openness" "%USERNAME%" /add
if %ERRORLEVEL% EQU 0 (
    echo [OK] Da them nguoi dung %USERNAME% vao nhom Siemens TIA Openness thanh cong!
) else (
    echo [LOI] Can chay file nay voi quyen Run as administrator!
)
pause
