# Сборка BCL

**Требования:**

- MadOut2 9.4 с установленным BepInEx 5
- .NET SDK

**Процесс сборки:**

1. Установите BepInEx 5 в папку с игрой и запустите игру 1 раз.
2. Клонируйте и соберите проект, указав **абсолютный путь** к корневой папке игры:

```bat
git clone --recurse-submodules https://github.com/LxgacyTeam/BigCityLegacy.git
cd BigCityLegacy
dotnet build -c Release -p:GameDir="path\to\game\dir" -p:CopyToPlugins=true
```

> [!NOTE]
> При открытии проекта в Visual Studio **обязательно** укажите путь к корневой папке игры в атрибуте `<GameDir>` во всех файлах `.csproj`. После все необходимые Unity и BepInEx сборки подключатся автоматически

> [!WARNING]
> При передаче атрибута `GameDir` через CLI флаг, **требуется указывать абсолютный путь**. Использование `..\` или прочих относительных путей может привести к ошибке, т.к атрибут влияет и на зависимый проект LegacyUIFramework, находящийся в одной из вложенных директорий.

[LegacyHelpers](LegacyHelpers_RU.md) · [LegacyAlerts](LegacyAlerts_RU.md)
