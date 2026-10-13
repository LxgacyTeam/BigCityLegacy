@echo off
chcp 65001 >nul
cd /d "%~dp0"

:: Place this script in the game directory.
:: Settings: BigCityLegacy\config\ServerConfig.toml.

:: DO NOT PASS "-attachParentConsole" when running directly from an interactive console.

game.exe -bend_GameServer -batchmode -nographics -attachParentConsole -serverConfig:"BigCityLegacy\config\ServerConfig.toml" %*
