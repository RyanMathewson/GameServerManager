@echo off
REM Uninstall the Game Server Manager Windows service
set SERVICE_NAME="Game Server Manager"

sc stop %SERVICE_NAME%
sc delete %SERVICE_NAME%

echo Service uninstallation complete.
pause
