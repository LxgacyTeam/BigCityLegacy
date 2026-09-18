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

set serversList=""
set eventsList=""
set respawnPoints=""
set banList=""
set useVanillaRespawn=false
set ChatEvents=true
set noCheckUpdates=false
set killchat=true

set verboseServerConsole=false
set noServerConsole=false
set noServerCli=false
set masterDebug=false
set noLogFile=true

:: --------------------------


set Args=
if [%Master%] == [true] (set Args=%Args%-masterServer )
if [%masterDebug%] == [true] (set Args=%Args%-masterDebug )
if [%verboseServerConsole%] == [true] (set Args=%Args%-verboseServerConsole )
if [%useVanillaRespawn%] == [true] (set Args=%Args%-useVanillaRespawn )
if [%noServerConsole%] == [true] (set Args=%Args%-noServerConsole )
if [%noServerCli%] == [true] (set Args=%Args%-noServerCli )
if [%ChatEvents%] == [false] (set Args=%Args%-noChatEvents )
if [%noCheckUpdates%] == [true] (set Args=%Args%-noUpdCheck )
if [%killchat%] == [false] (set Args=%Args%-noKillChat )
if [%noLogFile%] == [true] (set Args=%Args%-noLogFile )
set "match1=0"
if [%serversList%] == [""] set "match1=1"
if [%serversList%] == [] set "match1=1"
if %match1% == 0 (set Args=%Args%-serversList:%serversList% )
set "match2=0"
if [%eventsList%] == [""] set "match2=1"
if [%eventsList%] == [] set "match2=1"
if %match2% == 0 (set Args=%Args%-eventsList:%eventsList% )
set "match3=0"
if [%respawnPoints%] == [""] set "match3=1"
if [%respawnPoints%] == [] set "match3=1"
if %match3% == 0 (set Args=%Args%-respawnPoints:%respawnPoints% )
set "match4=0"
if [%banList%] == [""] set "match4=1"
if [%banList%] == [] set "match4=1"
if %match4% == 0 (set Args=%Args%-banList:%banList% )

:: DO NOT PASS "-attachParentConsole" when running directly from an interactive console.

game.exe -bend_GameServer -batchmode -nographics -attachParentConsole -port:%Port% -maxConn:%MaxPlayers% -maxCars:%maxCars% -maxCarsByPlayer:%maxCarsByPlayer% -maxNikLength:%maxNikLength% -idleKickTimeout:%idleKickTimeout% -Mode:%GameMode% %Args%
