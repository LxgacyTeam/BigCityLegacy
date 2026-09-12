@echo off
chcp 65001 >nul
cd %~dp0


:: -------- CONFIG ----------

set Port=7800
set MaxPlayers=16
set GameMode=None
set Master=true

set maxCars=100
set maxCarsByPlayer=15
set maxNikLength=20
set idleKickTimeout=120s

set "serversList="
set "eventsList="
set "respawnPoints="
set "banList="
set useVanillaRespawn=false
set ChatEvents=true
set noCheckUpdates=false

set verboseServerConsole=false
set noServerConsole=false
set noServerCli=false
set masterDebug=false

:: --------------------------


set "Args="
if "%Master%" == "true" (set "Args=%Args%-masterServer ")
if "%masterDebug%" == "true" (set "Args=%Args%-masterDebug ")
if "%verboseServerConsole%" == "true" (set "Args=%Args%-verboseServerConsole ")
if "%useVanillaRespawn%" == "true" (set "Args=%Args%-useVanillaRespawn ")
if "%noServerConsole%" == "true" (set "Args=%Args%-noServerConsole ")
if "%noServerCli%" == "true" (set "Args=%Args%-noServerCli ")
if "%ChatEvents%" == "false" (set "Args=%Args%-noChatEvents ")
if "%noCheckUpdates%" == "true" (set "Args=%Args%-noUpdCheck ")

if not "%serversList%" == "" (set "Args=%Args%-serversList:^"%serversList%^" ")
if not "%eventsList%" == "" (set "Args=%Args%-eventsList:^"%eventsList%^" ")
if not "%respawnPoints%" == "" (set "Args=%Args%-respawnPoints:^"%respawnPoints%^" ")
if not "%banList%" == "" (set "Args=%Args%-banList:^"%banList%^" ")

game.exe -bend_GameServer -batchmode -nographics -port:%Port% -maxConn:%MaxPlayers% -maxCars:%maxCars% -maxCarsByPlayer:%maxCarsByPlayer% -maxNikLength:%maxNikLength% -idleKickTimeout:%idleKickTimeout% -Mode:%GameMode% %Args%
