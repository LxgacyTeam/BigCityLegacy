#!/usr/bin/env bash
cd "$(dirname "$0")" || exit 1


# -------- CONFIG ----------

Port=7800
MaxPlayers=16
GameMode="None"
Master=true

maxCars=100
maxCarsByPlayer=15
maxNikLength=20
idleKickTimeout=120s

serversList=""
eventsList=""
respawnPoints=""
banList=""
useVanillaRespawn=false
ChatEvents=true
noCheckUpdates=false
killchat=true

verboseServerConsole=false
noServerConsole=false
noServerCli=false
masterDebug=false

# --------------------------


Args=()
[[ "$Master" == "true" ]] && Args+=("-masterServer")
[[ "$masterDebug" == "true" ]] && Args+=("-masterDebug")
[[ "$verboseServerConsole" == "true" ]] && Args+=("-verboseServerConsole")
[[ "$useVanillaRespawn" == "true" ]] && Args+=("-useVanillaRespawn")
[[ "$noServerConsole" == "true" ]] && Args+=("-noServerConsole")
[[ "$noServerCli" == "true" ]] && Args+=("-noServerCli")
[[ "$ChatEvents" == "false" ]] && Args+=("-noChatEvents")
[[ "$noCheckUpdates" == "true" ]] && Args+=("-noUpdCheck")
[[ "$killchat" == "false" ]] && Args+=("-noKillChat")
[[ -n "$serversList" ]] && Args+=("-serversList:\"$serversList\"")
[[ -n "$eventsList" ]] && Args+=("-eventsList:\"$eventsList\"")
[[ -n "$respawnPoints" ]] && Args+=("-respawnPoints:\"$respawnPoints\"")
[[ -n "$banList" ]] && Args+=("-banList:\"$banList\"")

./run_bepinex.sh ./game -bend_GameServer -batchmode -nographics \
  "-port:$Port" "-maxConn:$MaxPlayers" "-Mode:$GameMode" \
  "-maxCars:$maxCars" "-maxCarsByPlayer:$maxCarsByPlayer" "-maxNikLength:$maxNikLength" "-idleKickTimeout:$idleKickTimeout" \
  "${Args[@]}"
