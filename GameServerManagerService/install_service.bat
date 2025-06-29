@echo off
REM Install the SteamManagerService as a Windows service
set SERVICE_NAME=SteamManagerService
set EXE_PATH=%~dp0SteamManagerService.exe

sc stop %SERVICE_NAME%
sc delete %SERVICE_NAME%
sc create %SERVICE_NAME% binPath= "%EXE_PATH%" start= auto
sc description %SERVICE_NAME% "Steam Manager Service"
sc start %SERVICE_NAME%

echo Service installation complete.
pause
