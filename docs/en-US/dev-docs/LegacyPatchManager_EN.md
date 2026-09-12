# LegacyPatchManager

`LegacyPatchManager` is BigCityLegacy's internal manager for applying Harmony patches. It centralizes patch installation, separates patch sets for full mode and compatibility mode, and provides safe methods for optional patches that may be missing in older or different game builds.

The manager is intended to be used **inside the `LegacyPatchManager` class itself**. The production patch application methods are currently `private`, so this is not a public API for external plugins. To add new patches to the main mod, add calls to `PatchFullMode()` or `PatchCompatibilityMode()`.

## General workflow

The manager entry point:

```csharp
internal static void PatchForCurrentMode(Harmony harmony, ManualLogSource log)
```

It stores the `Harmony` instance and logger, then selects the patch set:

```csharp
if (LegacyCompatibility.IsCompatibilityMode)
{
    PatchCompatibilityMode();
    return;
}

PatchFullMode();
```

In full mode, complete patch classes are usually applied through `PatchClass(...)`.

In compatibility mode, safe string-based methods are used: `SafePrefix`, `SafePostfix`, `SafeConstructorPostfix`, and `SafeGetterPrefix`. They do not break mod loading if the target type, method, constructor, or getter is not found.

## When to use `PatchClass` and when to use `Safe*`

### Use `PatchClass(...)` when

- the patch is intended for the fully supported game version;
- the target types and methods are guaranteed to exist in that version;
- the patch class is already annotated with standard Harmony attributes;
- a patching error should be visible as a real problem.

### Use `SafePrefix(...)`, `SafePostfix(...)`, and similar methods when

- the patch is optional;
- the target method may be missing in another game version;
- the method signature may differ between builds;
- the patch is applied in compatibility mode;
- mod startup must not fail because a method is missing.

Safe methods log skipped patches as debug messages:

```text
Skipped optional patch <label>: <reason>
```

For example:

```text
Skipped optional patch MenuGroup.Start: target method not found
```

## Terms and parameters

Most methods take a similar set of parameters.

| Parameter | Purpose |
|---|---|
| `label` | Human-readable patch name for logs. Prefer `Type.Method` or `Type.Method(signature)`. |
| `typeName` | Target game type name. Resolved through `AccessTools.TypeByName(...)`. A simple type name or a full type name can be used. |
| `methodName` | Target method name without the type. For property getters, prefer `SafeGetterPrefix(...)`. |
| `propertyName` | Property name without `get_`. For example, `isTest`, not `get_isTest`. |
| `parameters` | Array of target method argument types. Required for overloads. |
| `patchClass` | Type of the class that contains the patch method. Usually `typeof(GeneralPatches)` or another class from `Patches`. |
| `patchMethodName` | Name of the patch method inside `patchClass`. |

`LegacyPatchManager` resolves types through `FindType(...)`. For nested types, both separator forms are supported: `Outer+Inner` and `Outer.Inner`.

---

# `PatchClass(Type patchClass)`

Applies all Harmony patches declared inside a class through `[HarmonyPatch]`, `[HarmonyPrefix]`, `[HarmonyPostfix]`, and other standard Harmony mechanisms.

Signature:

```csharp
private static void PatchClass(Type patchClass)
```

Internally it uses:

```csharp
_harmony.CreateClassProcessor(patchClass).Patch();
```

If the patch is applied successfully, the debug log receives:

```text
Patched <PatchClassName>
```

If applying the class fails, the error is logged as:

```text
Failed to patch <PatchClassName>: <exception>
```

## Example usage

Inside `PatchFullMode()`:

```csharp
private static void PatchFullMode()
{
    PatchClass(typeof(GeneralPatches));
    PatchClass(typeof(NetConnectAndControlPatches));
    PatchClass(typeof(NetworkPatches));
}
```

Patch class:

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

## When to choose this method

`PatchClass` is the main approach for full mode. It is easier to read and keeps the normal Harmony annotations next to the patch code.

---

# `SafePrefix(string label, string typeName, string methodName, Type patchClass, string patchMethodName)`

Safely applies a prefix to a target method without explicitly specifying its signature.

Signature:

```csharp
private static void SafePrefix(
    string label,
    string typeName,
    string methodName,
    Type patchClass,
    string patchMethodName)
```

The method:

1. finds the target type by `typeName`;
2. finds the declared method by `methodName`;
3. finds the patch method `patchMethodName` inside `patchClass`;
4. applies it as a Harmony prefix;
5. if something is not found, writes a debug skip message and does not interrupt loading.

## Example usage

```csharp
SafePrefix(
    "AlwaysOnline.Start",
    "AlwaysOnline",
    "Start",
    typeof(GeneralPatches),
    "AlwaysOnline_Start");
```

Patch method:

```csharp
private static bool AlwaysOnline_Start(AlwaysOnline __instance)
{
    AlwaysOnline.me = __instance;
    __instance.gameObject.SetActive(false);
    return false;
}
```

In a Harmony prefix, `return false` means the original `AlwaysOnline.Start()` method will not be executed.

## Optional patch example

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

This patch is safe for builds where `MenuEsc.RestorePurchases` may be missing.

## Important limitation

This overload is best used only for methods without overloads. If the target method has several overload variants, use the `SafePrefix(...)` overload with `Type[] parameters`.

---

# `SafePrefix(string label, string typeName, string methodName, Type[] parameters, Type patchClass, string patchMethodName)`

Safely applies a prefix to a specific method overload.

Signature:

```csharp
private static void SafePrefix(
    string label,
    string typeName,
    string methodName,
    Type[] parameters,
    Type patchClass,
    string patchMethodName)
```

The main difference from the previous method is that the target method is searched not only by name, but also by its parameter type array:

```csharp
AccessTools.DeclaredMethod(targetType, methodName, parameters)
```

This is the production variant for overloaded methods.

## Example: overload with `string, int`

```csharp
SafePrefix(
    "InApp_But.GetPriceAndValuta(string,int)",
    "InApp_But",
    "GetPriceAndValuta",
    new[] { typeof(string), typeof(int) },
    typeof(InAppPatches),
    "InAppBut_GetPriceFloat_Prefix");
```

Patch method:

```csharp
private static bool InAppBut_GetPriceFloat_Prefix(ref float __result)
{
    __result = 0f;
    return false;
}
```

Here the prefix fully replaces the original method and returns `0f` through `__result`.

## Example: overload with Unity UI `Text`

```csharp
SafePrefix(
    "InApp_But.GetPriceAndValuta(texts)",
    "InApp_But",
    "GetPriceAndValuta",
    new[] { typeof(string), typeof(Text), typeof(Text), typeof(Text), typeof(Text) },
    typeof(InAppPatches),
    "InAppBut_GetPriceTexts_Prefix");
```

Patch method:

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

## When `parameters` should always be specified

Specify `parameters` when:

- the method has overloads;
- overload variants may have appeared in different game versions;
- the patch must target a method with a specific signature;
- a patching error must not be hidden by accidentally patching another overload.

---

# `SafePostfix(string label, string typeName, string methodName, Type patchClass, string patchMethodName)`

Safely applies a postfix to a target method without explicitly specifying its signature.

Signature:

```csharp
private static void SafePostfix(
    string label,
    string typeName,
    string methodName,
    Type patchClass,
    string patchMethodName)
```

A postfix runs after the original method. It is useful when you need to extend the game's behavior rather than replace it.

## Example usage

```csharp
SafePostfix(
    "FirstScene.OnGUI",
    "FirstScene",
    "OnGUI",
    typeof(GeneralPatches),
    "FirstScene_OnGUI_Postfix");
```

Patch method:

```csharp
private static void FirstScene_OnGUI_Postfix()
{
    LegacyWatermark.Draw();
}
```

The original `FirstScene.OnGUI()` runs normally, and then BigCityLegacy draws the watermark.

## Postfix example for changing state after `Awake`

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

## Limitation

The current `LegacyPatchManager` does not provide a `SafePostfix` overload with `Type[] parameters`. If you need a postfix for an overloaded method, either add such an overload to the manager or use `PatchClass(...)` with normal Harmony attributes.

---

# `SafeConstructorPostfix(string label, string typeName, Type patchClass, string patchMethodName)`

Safely applies a postfix to the target type constructor.

Signature:

```csharp
private static void SafeConstructorPostfix(
    string label,
    string typeName,
    Type patchClass,
    string patchMethodName)
```

The current implementation searches only for a parameterless constructor:

```csharp
AccessTools.Constructor(targetType, Type.EmptyTypes)
```

If the type or constructor is not found, the patch is skipped without a loading error.

## Example usage

```csharp
SafeConstructorPostfix(
    "LocalizationManager.ctor",
    "LocalizationManager",
    typeof(GeneralPatches),
    "LocalizationManager_Ctor_Postfix");
```

Patch method:

```csharp
private static void LocalizationManager_Ctor_Postfix(LocalizationManager __instance)
{
    LegacyLocalizer.ApplyLocalizationManagerPatch(__instance);
}
```

This postfix runs after `LocalizationManager` is created and applies localization replacements.

## When to use

Use `SafeConstructorPostfix` when:

- code must run right after an object is created;
- there is no suitable `Awake`, `Start`, or `Init`, or it runs too late;
- the type may be missing in some game versions;
- a parameterless constructor is enough.

## Limitation

The method is not suitable for constructors with parameters. The current `LegacyPatchManager` has no overload variant with `Type[] parameters`. Use a standard Harmony patch class instead.

---

# `SafeGetterPrefix(string label, string typeName, string propertyName, Type patchClass, string patchMethodName)`

Safely applies a prefix to a property getter.

Signature:

```csharp
private static void SafeGetterPrefix(
    string label,
    string typeName,
    string propertyName,
    Type patchClass,
    string patchMethodName)
```

The method first searches for the getter through:

```csharp
AccessTools.PropertyGetter(targetType, propertyName)
```

If the getter is not found, it additionally tries to find the method by name:

```csharp
AccessTools.Method(targetType, "get_" + propertyName)
```

This is useful for properties that the decompiler shows as `property`, while in IL they are represented as a regular `get_PropertyName` method.

## Example usage

```csharp
SafeGetterPrefix(
    "InApp_But.isTest",
    "InApp_But",
    "isTest",
    typeof(InAppPatches),
    "InAppBut_IsTest_Prefix");
```

Patch method:

```csharp
private static bool InAppBut_IsTest_Prefix(ref bool __result)
{
    __result = true;
    return false;
}
```

This prefix fully replaces the `InApp_But.isTest` getter and forces it to return `true`.

## When to use

Use `SafeGetterPrefix` when you need to:

- replace a property result;
- disable execution of the original getter;
- avoid manually specifying `get_PropertyName`;
- keep compatibility-safe behavior.

---

# Example: adding a new optional patch

Suppose you need to disable the hidden gesture trigger in the `Reporter` class, but that class may be missing in older builds.

Inside `PatchCompatibilityMode()` or a separate server/client patch set:

```csharp
SafePrefix(
    "Reporter.isGestureDone",
    "Reporter",
    "isGestureDone",
    typeof(ReporterPatches),
    "Reporter_IsGestureDone_Prefix");
```

Patch class:

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

If `Reporter` or `isGestureDone` is missing, the mod continues loading and the debug log receives the skip reason.

---

# Example: adding a new full-mode patch

If a patch is intended only for the main supported game version, it is better to use `PatchClass`.

Inside `PatchFullMode()`:

```csharp
PatchClass(typeof(ServerTimeoutPatches));
```

Patch class:

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

This approach is easier to read and maintain because the target type and method are written directly above the patch method.

---

# Common patch method signatures

## Prefix that skips the original method

```csharp
private static bool Target_Method_Prefix()
{
    return false;
}
```

## Prefix that allows the original method to run

```csharp
private static bool Target_Method_Prefix()
{
    return true;
}
```

## Prefix that replaces the result

```csharp
private static bool Target_Method_Prefix(ref bool __result)
{
    __result = true;
    return false;
}
```

## Prefix with access to the instance

```csharp
private static bool Target_Method_Prefix(TargetType __instance)
{
    // Work with instance.
    return true;
}
```

## Postfix with access to the instance

```csharp
private static void Target_Method_Postfix(TargetType __instance)
{
    // Run after original method.
}
```

## Constructor postfix

```csharp
private static void Target_Ctor_Postfix(TargetType __instance)
{
    // Run after object construction.
}
```

---

# Error behavior

`PatchClass(...)` catches exceptions and writes `LogError`. This means a patch class failure is treated as a problem that should be noticed.

`Safe*` methods catch errors and write `LogDebug` through `LogSkipped(...)`. This means a missing target is considered an acceptable scenario.

---
