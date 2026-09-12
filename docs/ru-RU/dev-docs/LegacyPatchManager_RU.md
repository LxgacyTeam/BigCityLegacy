# LegacyPatchManager

`LegacyPatchManager` - внутренний менеджер применения Harmony-патчей в BigCityLegacy. Он централизует установку патчей, разделяет наборы патчей для полного режима и compatibility mode, а также даёт безопасные методы для optional-патчей, которые могут отсутствовать в старых или отличающихся сборках игры.

Менеджер рассчитан на использование **внутри самого класса `LegacyPatchManager`**. Production-методы применения патчей сейчас являются `private`, поэтому это не публичный API для внешних плагинов. Для добавления новых патчей в основной мод нужно добавлять вызовы в `PatchFullMode()` или `PatchCompatibilityMode()`.

## Общая схема работы

Точка входа менеджера:

```csharp
internal static void PatchForCurrentMode(Harmony harmony, ManualLogSource log)
```

Она сохраняет экземпляр `Harmony` и логгер, после чего выбирает набор патчей:

```csharp
if (LegacyCompatibility.IsCompatibilityMode)
{
    PatchCompatibilityMode();
    return;
}

PatchFullMode();
```

В полном режиме обычно применяются целые классы патчей через `PatchClass(...)`.

В compatibility mode используются безопасные string-based методы `SafePrefix`, `SafePostfix`, `SafeConstructorPostfix`, `SafeGetterPrefix`. Они не валят загрузку мода, если целевой тип, метод, конструктор или getter не найден.

## Когда использовать `PatchClass`, а когда `Safe*`

### Используйте `PatchClass(...)`, если

- патч предназначен для полной поддерживаемой версии игры;
- целевые типы и методы гарантированно есть в этой версии;
- patch-класс уже размечен стандартными Harmony-атрибутами;
- ошибка применения такого патча должна быть видна как реальная проблема.

### Используйте `SafePrefix(...)`, `SafePostfix(...)` и похожие методы, если

- патч optional;
- целевой метод может отсутствовать в другой версии игры;
- сигнатура метода могла отличаться между сборками;
- патч применяется в compatibility mode;
- нужно не завалить старт мода из-за отсутствующего метода.

Safe-методы логируют пропуск как debug-сообщение:

```text
Skipped optional patch <label>: <reason>
```

Например:

```text
Skipped optional patch MenuGroup.Start: target method not found
```

## Термины и параметры

Большинство методов принимают похожий набор параметров.

| Параметр | Назначение |
|---|---|
| `label` | Человекочитаемое имя патча для логов. Лучше указывать `Type.Method` или `Type.Method(signature)`. |
| `typeName` | Имя целевого типа из игры. Ищется через `AccessTools.TypeByName(...)`. Можно указывать простое имя или полное имя типа. |
| `methodName` | Имя целевого метода без типа. Для property getter лучше использовать `SafeGetterPrefix(...)`. |
| `propertyName` | Имя свойства без `get_`. Например `isTest`, а не `get_isTest`. |
| `parameters` | Массив типов аргументов целевого метода. Нужен для перегрузок. |
| `patchClass` | Тип класса, где находится метод-патч. Обычно `typeof(GeneralPatches)` или другой класс из `Patches`. |
| `patchMethodName` | Имя метода-патча внутри `patchClass`. |

`LegacyPatchManager` ищет типы через `FindType(...)`. Для nested-типов поддерживаются оба варианта разделителей: `Outer+Inner` и `Outer.Inner`.

---

# `PatchClass(Type patchClass)`

Применяет все Harmony-патчи, объявленные внутри класса через атрибуты `[HarmonyPatch]`, `[HarmonyPrefix]`, `[HarmonyPostfix]` и другие стандартные механизмы Harmony.

Сигнатура:

```csharp
private static void PatchClass(Type patchClass)
```

Внутри используется:

```csharp
_harmony.CreateClassProcessor(patchClass).Patch();
```

Если патч успешно применён, в debug-лог пишется:

```text
Patched <PatchClassName>
```

Если при применении класса произошла ошибка, она логируется как error:

```text
Failed to patch <PatchClassName>: <exception>
```

## Пример использования

В `PatchFullMode()`:

```csharp
private static void PatchFullMode()
{
    PatchClass(typeof(GeneralPatches));
    PatchClass(typeof(NetConnectAndControlPatches));
    PatchClass(typeof(NetworkPatches));
}
```

Patch-класс:

```csharp
[HarmonyPatch]
internal static class GeneralPatches
{
    [HarmonyPatch(typeof(AlwaysOnline), "Start")]
    [HarmonyPrefix]
    private static bool AlwaysOnline_Start(AlwaysOnline __instance)
    {
        AlwaysOnline.me = __instance;
        __instance.gameObject.SetActive(false);
        return false;
    }
}
```

## Когда выбирать этот метод

`PatchClass` — основной способ для full mode. Он удобнее, читабельнее и даёт обычную Harmony-разметку рядом с кодом патча.

---

# `SafePrefix(string label, string typeName, string methodName, Type patchClass, string patchMethodName)`

Безопасно применяет prefix к целевому методу без явного указания сигнатуры.

Сигнатура:

```csharp
private static void SafePrefix(
    string label,
    string typeName,
    string methodName,
    Type patchClass,
    string patchMethodName)
```

Метод:

1. ищет целевой тип по `typeName`;
2. ищет объявленный метод по `methodName`;
3. ищет метод-патч `patchMethodName` внутри `patchClass`;
4. применяет его как Harmony prefix;
5. если что-то не найдено — пишет debug skip и не прерывает загрузку.

## Пример использования

```csharp
SafePrefix(
    "AlwaysOnline.Start",
    "AlwaysOnline",
    "Start",
    typeof(GeneralPatches),
    "AlwaysOnline_Start");
```

Метод-патч:

```csharp
private static bool AlwaysOnline_Start(AlwaysOnline __instance)
{
    AlwaysOnline.me = __instance;
    __instance.gameObject.SetActive(false);
    return false;
}
```

`return false` в Harmony prefix означает, что оригинальный метод `AlwaysOnline.Start()` не будет выполнен.

## Пример optional-патча

```csharp
SafePrefix(
    "MenuEsc.RestorePurchases",
    "MenuEsc",
    "RestorePurchases",
    typeof(GeneralPatches),
    "MenuEsc_RestorePurchases");
```

```csharp
private static bool MenuEsc_RestorePurchases()
{
    return false;
}
```

Такой патч безопасен для сборок, где `MenuEsc.RestorePurchases` может отсутствовать.

## Важное ограничение

Эту перегрузку лучше использовать только для методов без перегрузок. Если у целевого метода есть несколько overload-вариантов, используй перегрузку `SafePrefix(...)` с `Type[] parameters`.

---

# `SafePrefix(string label, string typeName, string methodName, Type[] parameters, Type patchClass, string patchMethodName)`

Безопасно применяет prefix к конкретной перегрузке метода.

Сигнатура:

```csharp
private static void SafePrefix(
    string label,
    string typeName,
    string methodName,
    Type[] parameters,
    Type patchClass,
    string patchMethodName)
```

Главное отличие от предыдущего метода — целевой метод ищется не только по имени, но и по массиву типов параметров:

```csharp
AccessTools.DeclaredMethod(targetType, methodName, parameters)
```

Это production-вариант для перегруженных методов.

## Пример: перегрузка с `string, int`

```csharp
SafePrefix(
    "InApp_But.GetPriceAndValuta(string,int)",
    "InApp_But",
    "GetPriceAndValuta",
    new[] { typeof(string), typeof(int) },
    typeof(InAppPatches),
    "InAppBut_GetPriceFloat_Prefix");
```

Метод-патч:

```csharp
private static bool InAppBut_GetPriceFloat_Prefix(ref float __result)
{
    __result = 0f;
    return false;
}
```

Здесь prefix полностью заменяет оригинальный метод и возвращает `0f` через `__result`.

## Пример: перегрузка с Unity UI `Text`

```csharp
SafePrefix(
    "InApp_But.GetPriceAndValuta(texts)",
    "InApp_But",
    "GetPriceAndValuta",
    new[] { typeof(string), typeof(Text), typeof(Text), typeof(Text), typeof(Text) },
    typeof(InAppPatches),
    "InAppBut_GetPriceTexts_Prefix");
```

Метод-патч:

```csharp
private static bool InAppBut_GetPriceTexts_Prefix(
    InApp_But __instance,
    Text text,
    Text priceAfterDot,
    Text valuta1)
{
    if (text)
    {
        text.text = "0";
    }

    if (priceAfterDot)
    {
        priceAfterDot.text = string.Empty;
    }

    if (valuta1)
    {
        valuta1.text = string.Empty;
    }

    return false;
}
```

## Когда обязательно указывать `parameters`

Указывайте `parameters`, если:

- у метода есть перегрузки;
- в разных версиях игры могли появиться overload-варианты;
- патч должен попасть строго в метод с конкретной сигнатурой;
- ошибка применения не должна быть замаскирована попаданием в другую перегрузку.

---

# `SafePostfix(string label, string typeName, string methodName, Type patchClass, string patchMethodName)`

Безопасно применяет postfix к целевому методу без явного указания сигнатуры.

Сигнатура:

```csharp
private static void SafePostfix(
    string label,
    string typeName,
    string methodName,
    Type patchClass,
    string patchMethodName)
```

Postfix вызывается после оригинального метода. Он удобен, когда нужно дополнить поведение игры, а не заменить его.

## Пример использования

```csharp
SafePostfix(
    "FirstScene.OnGUI",
    "FirstScene",
    "OnGUI",
    typeof(GeneralPatches),
    "FirstScene_OnGUI_Postfix");
```

Метод-патч:

```csharp
private static void FirstScene_OnGUI_Postfix()
{
    LegacyWatermark.Draw();
}
```

Оригинальный `FirstScene.OnGUI()` выполнится как обычно, а затем BigCityLegacy дорисует watermark.

## Пример postfix для изменения состояния после `Awake`

```csharp
SafePostfix(
    "App_DeliveryCar.Awake",
    "App_DeliveryCar",
    "Awake",
    typeof(GeneralPatches),
    "AppDeliveryCar_Awake_Postfix");
```

```csharp
private static void AppDeliveryCar_Awake_Postfix()
{
    SetStaticFloat(typeof(App_DeliveryCar), "deliveryCloseTime", 1f);
}
```

## Ограничение

В текущем `LegacyPatchManager` у `SafePostfix` нет overload-варианта с `Type[] parameters`. Если нужен postfix на перегруженный метод, нужно либо добавить такую перегрузку в менеджер, либо использовать `PatchClass(...)` с обычными Harmony-атрибутами.

---

# `SafeConstructorPostfix(string label, string typeName, Type patchClass, string patchMethodName)`

Безопасно применяет postfix к конструктору целевого типа.

Сигнатура:

```csharp
private static void SafeConstructorPostfix(
    string label,
    string typeName,
    Type patchClass,
    string patchMethodName)
```

Текущая реализация ищет только конструктор без параметров:

```csharp
AccessTools.Constructor(targetType, Type.EmptyTypes)
```

Если тип или конструктор не найден, патч пропускается без ошибки загрузки.

## Пример использования

```csharp
SafeConstructorPostfix(
    "LocalizationManager.ctor",
    "LocalizationManager",
    typeof(GeneralPatches),
    "LocalizationManager_Ctor_Postfix");
```

Метод-патч:

```csharp
private static void LocalizationManager_Ctor_Postfix(LocalizationManager __instance)
{
    LegacyLocalizer.ApplyLocalizationManagerPatch(__instance);
}
```

Этот postfix срабатывает после создания `LocalizationManager` и применяет замены локализации.

## Когда использовать

Используй `SafeConstructorPostfix`, если:

- нужно выполнить код сразу после создания объекта;
- нужного `Awake`, `Start` или `Init` нет либо он вызывается слишком поздно;
- тип может отсутствовать в некоторых версиях игры;
- достаточно конструктора без параметров.

## Ограничение

Метод не подходит для конструкторов с параметрами. В текущем `LegacyPatchManager` отсутствует overload-вариант с `Type[] parameters`. Используйте стандартный Harmony patch-класс.

---

# `SafeGetterPrefix(string label, string typeName, string propertyName, Type patchClass, string patchMethodName)`

Безопасно применяет prefix к getter’у свойства.

Сигнатура:

```csharp
private static void SafeGetterPrefix(
    string label,
    string typeName,
    string propertyName,
    Type patchClass,
    string patchMethodName)
```

Метод сначала ищет getter через:

```csharp
AccessTools.PropertyGetter(targetType, propertyName)
```

Если getter не найден, дополнительно пробует найти метод по имени:

```csharp
AccessTools.Method(targetType, "get_" + propertyName)
```

Это полезно для свойств, которые декомпилятор показывает как `property`, но в IL они представлены обычным `get_PropertyName`.

## Пример использования

```csharp
SafeGetterPrefix(
    "InApp_But.isTest",
    "InApp_But",
    "isTest",
    typeof(InAppPatches),
    "InAppBut_IsTest_Prefix");
```

Метод-патч:

```csharp
private static bool InAppBut_IsTest_Prefix(ref bool __result)
{
    __result = true;
    return false;
}
```

Такой prefix полностью заменяет getter `InApp_But.isTest` и заставляет его возвращать `true`.

## Когда использовать

Используйте `SafeGetterPrefix`, если нужно:

- заменить результат свойства;
- отключить выполнение оригинального getter’а;
- избежать ручного указания `get_PropertyName`;
- сохранить compatibility-safe поведение.

---

# Пример добавления нового optional-патча

Допустим, нужно отключить скрытый gesture-trigger у класса `Reporter`, но класс может отсутствовать в старых сборках.

В `PatchCompatibilityMode()` или отдельном server/client наборе:

```csharp
SafePrefix(
    "Reporter.isGestureDone",
    "Reporter",
    "isGestureDone",
    typeof(ReporterPatches),
    "Reporter_IsGestureDone_Prefix");
```

Patch-класс:

```csharp
internal static class ReporterPatches
{
    private static bool Reporter_IsGestureDone_Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}
```

Если `Reporter` или `isGestureDone` отсутствует, мод продолжит загрузку, а в debug-лог попадёт причина пропуска.

---

# Пример добавления нового full-mode патча

Если патч предназначен только для основной поддерживаемой версии игры, лучше использовать `PatchClass`.

В `PatchFullMode()`:

```csharp
PatchClass(typeof(ServerTimeoutPatches));
```

Patch-класс:

```csharp
[HarmonyPatch]
internal static class ServerTimeoutPatches
{
    [HarmonyPatch(typeof(NetInputControl), "CheckDisconnect")]
    [HarmonyPrefix]
    private static bool NetInputControl_CheckDisconnect_Prefix(NetInputControl __instance)
    {
        // Custom timeout logic.
        return true;
    }
}
```

Такой подход проще читать и поддерживать, потому что целевой тип и метод указаны прямо над patch-методом.

---

# Типовые сигнатуры patch-методов

## Prefix, который пропускает оригинал

```csharp
private static bool Target_Method_Prefix()
{
    return false;
}
```

## Prefix, который разрешает оригинал

```csharp
private static bool Target_Method_Prefix()
{
    return true;
}
```

## Prefix, который заменяет результат

```csharp
private static bool Target_Method_Prefix(ref bool __result)
{
    __result = true;
    return false;
}
```

## Prefix с доступом к экземпляру

```csharp
private static bool Target_Method_Prefix(TargetType __instance)
{
    // Work with instance.
    return true;
}
```

## Postfix с доступом к экземпляру

```csharp
private static void Target_Method_Postfix(TargetType __instance)
{
    // Run after original method.
}
```

## Postfix конструктора

```csharp
private static void Target_Ctor_Postfix(TargetType __instance)
{
    // Run after object construction.
}
```

---

# Поведение при ошибках

`PatchClass(...)` ловит исключения и пишет `LogError`. Это значит, что ошибка patch-класса считается проблемой, которую нужно заметить.

`Safe*` методы ловят ошибки и пишут `LogDebug` через `LogSkipped(...)`. Это значит, что отсутствие цели считается допустимым сценарием.

---
