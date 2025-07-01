@echo off
REM Install the GameServerManagerService as a Windows service
set SERVICE_NAME=GameServerManagerService
set EXE_PATH=%~dp0GameServerManagerService.exe

sc stop %SERVICE_NAME%
sc delete %SERVICE_NAME%
sc create %SERVICE_NAME% binPath= "%EXE_PATH%" start= auto
sc description %SERVICE_NAME% "Game Server Manager Service"
REM Set the service to delayed automatic start
sc config %SERVICE_NAME% start= delayed-auto
sc start %SERVICE_NAME%

echo Service installation complete.
pause
