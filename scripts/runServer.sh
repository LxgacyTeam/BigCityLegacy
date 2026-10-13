#!/usr/bin/env bash
cd "$(dirname "$0")" || exit 1

# Place this script in the game directory. 
# Settings: BigCityLegacy/config/ServerConfig.toml.

exec ./run_bepinex.sh ./game -bend_GameServer -batchmode -nographics \
  '-serverConfig:BigCityLegacy/config/ServerConfig.toml' "$@"
