@echo off
REM Uninstall the GameServerManagerService Windows service
set SERVICE_NAME=GameServerManagerService

sc stop %SERVICE_NAME%
sc delete %SERVICE_NAME%

echo Service uninstallation complete.
pause
