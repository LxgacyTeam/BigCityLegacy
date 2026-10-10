# LegacyPatchManager

`LegacyPatchManager` 是 BigCityLegacy 内部用于应用 Harmony 补丁的管理器。它集中处理补丁安装，将完整模式与兼容模式的补丁集分开，并为旧版或不同游戏构建中可能缺少目标的可选补丁提供安全方法。

该管理器设计用于 **`LegacyPatchManager` 类内部**。当前实际应用补丁的方法均为 `private`，因此它不是供外部插件使用的公共 API。向主模组添加新补丁时，请在 `PatchFullMode()` 或 `PatchCompatibilityMode()` 中添加调用。

## 总体流程

管理器入口：

```csharp
internal static void PatchForCurrentMode(Harmony harmony, ManualLogSource log)
```

它保存 `Harmony` 实例和日志记录器，然后选择补丁集：

```csharp
if (LegacyCompatibility.IsCompatibilityMode)
{
    PatchCompatibilityMode();
    return;
}

PatchFullMode();
```

在完整模式下，通常通过 `PatchClass(...)` 应用完整的补丁类。

在兼容模式下，使用基于字符串的安全方法：`SafePrefix`、`SafePostfix`、`SafeConstructorPostfix` 和 `SafeGetterPrefix`。即使未找到目标类型、方法、构造函数或 getter，也不会中断模组加载。

## 何时使用 `PatchClass`，何时使用 `Safe*`

### 以下情况使用 `PatchClass(...)`

- 补丁面向完全受支持的游戏版本；
- 该版本中保证存在目标类型和方法；
- 补丁类已标注标准 Harmony 特性；
- 补丁应用错误应作为实际问题明确呈现。

### 以下情况使用 `SafePrefix(...)`、`SafePostfix(...)` 等方法

- 补丁是可选的；
- 其他游戏版本中可能缺少目标方法；
- 不同构建之间的方法签名可能不同；
- 补丁在兼容模式下应用；
- 模组启动不能因缺少某个方法而失败。

安全方法会将被跳过的补丁记录为调试消息：

```text
Skipped optional patch <label>: <reason>
```

例如：

```text
Skipped optional patch MenuGroup.Start: target method not found
```

## 术语与参数

大多数方法接受相似的一组参数。

| 参数 | 用途 |
|---|---|
| `label` | 日志中便于阅读的补丁名称。建议使用 `Type.Method` 或 `Type.Method(signature)`。 |
| `typeName` | 游戏目标类型的名称，通过 `AccessTools.TypeByName(...)` 解析。可以使用简单类型名或完整类型名。 |
| `methodName` | 不含类型名称的目标方法名。对于属性 getter，建议使用 `SafeGetterPrefix(...)`。 |
| `propertyName` | 不含 `get_` 的属性名。例如使用 `isTest`，而非 `get_isTest`。 |
| `parameters` | 目标方法参数类型的数组。处理重载时必须提供。 |
| `patchClass` | 包含补丁方法的类的类型。通常为 `typeof(GeneralPatches)`，或 `Patches` 中的其他类。 |
| `patchMethodName` | `patchClass` 内部的补丁方法名称。 |

`LegacyPatchManager` 通过 `FindType(...)` 解析类型。对于嵌套类型，支持两种分隔形式：`Outer+Inner` 和 `Outer.Inner`。

---

# `PatchClass(Type patchClass)`

通过 `[HarmonyPatch]`、`[HarmonyPrefix]`、`[HarmonyPostfix]` 及其他标准 Harmony 机制，应用类中声明的全部 Harmony 补丁。

签名：

```csharp
private static void PatchClass(Type patchClass)
```

内部使用：

```csharp
_harmony.CreateClassProcessor(patchClass).Patch();
```

补丁应用成功后，调试日志会记录：

```text
Patched <PatchClassName>
```

如果应用补丁类失败，则按以下格式记录错误：

```text
Failed to patch <PatchClassName>: <exception>
```

## 使用示例

在 `PatchFullMode()` 中：

```csharp
private static void PatchFullMode()
{
    PatchClass(typeof(GeneralPatches));
    PatchClass(typeof(NetConnectAndControlPatches));
    PatchClass(typeof(NetworkPatches));
}
```

补丁类：

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

## 何时选择此方法

`PatchClass` 是完整模式下的主要方式。它更易于阅读，并将常规 Harmony 标注保留在补丁代码旁边。

---

# `SafePrefix(string label, string typeName, string methodName, Type patchClass, string patchMethodName)`

在不明确指定签名的情况下，安全地向目标方法应用前置补丁（prefix）。

签名：

```csharp
private static void SafePrefix(
    string label,
    string typeName,
    string methodName,
    Type patchClass,
    string patchMethodName)
```

该方法会：

1. 根据 `typeName` 查找目标类型；
2. 根据 `methodName` 查找声明的方法；
3. 在 `patchClass` 内查找补丁方法 `patchMethodName`；
4. 将其作为 Harmony 前置补丁应用；
5. 如果未找到所需内容，记录跳过原因的调试消息，并继续加载。

## 使用示例

```csharp
SafePrefix(
    "AlwaysOnline.Start",
    "AlwaysOnline",
    "Start",
    typeof(GeneralPatches),
    "AlwaysOnline_Start");
```

补丁方法：

```csharp
private static bool AlwaysOnline_Start(AlwaysOnline __instance)
{
    AlwaysOnline.me = __instance;
    __instance.gameObject.SetActive(false);
    return false;
}
```

在 Harmony 前置补丁中，`return false` 表示不执行原始 `AlwaysOnline.Start()` 方法。

## 可选补丁示例

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

对于可能缺少 `MenuEsc.RestorePurchases` 的构建，该补丁也可以安全使用。

## 重要限制

此重载最好仅用于没有重载的方法。如果目标方法有多个重载版本，请使用带 `Type[] parameters` 的 `SafePrefix(...)` 重载。

---

# `SafePrefix(string label, string typeName, string methodName, Type[] parameters, Type patchClass, string patchMethodName)`

安全地向特定的方法重载应用前置补丁。

签名：

```csharp
private static void SafePrefix(
    string label,
    string typeName,
    string methodName,
    Type[] parameters,
    Type patchClass,
    string patchMethodName)
```

与前一种方法的主要区别在于，查找目标方法时不仅依据名称，还依据参数类型数组：

```csharp
AccessTools.DeclaredMethod(targetType, methodName, parameters)
```

这是实际应用中用于重载方法的版本。

## 示例：带 `string, int` 参数的重载

```csharp
SafePrefix(
    "InApp_But.GetPriceAndValuta(string,int)",
    "InApp_But",
    "GetPriceAndValuta",
    new[] { typeof(string), typeof(int) },
    typeof(InAppPatches),
    "InAppBut_GetPriceFloat_Prefix");
```

补丁方法：

```csharp
private static bool InAppBut_GetPriceFloat_Prefix(ref float __result)
{
    __result = 0f;
    return false;
}
```

这里的前置补丁完全替代原始方法，并通过 `__result` 返回 `0f`。

## 示例：带 Unity UI `Text` 参数的重载

```csharp
SafePrefix(
    "InApp_But.GetPriceAndValuta(texts)",
    "InApp_But",
    "GetPriceAndValuta",
    new[] { typeof(string), typeof(Text), typeof(Text), typeof(Text), typeof(Text) },
    typeof(InAppPatches),
    "InAppBut_GetPriceTexts_Prefix");
```

补丁方法：

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

## 何时必须指定 `parameters`

以下情况下应指定 `parameters`：

- 方法存在重载；
- 不同游戏版本可能新增过重载版本；
- 补丁必须作用于具有特定签名的方法；
- 不能让误修补其他重载的情况掩盖补丁应用错误。

---

# `SafePostfix(string label, string typeName, string methodName, Type patchClass, string patchMethodName)`

在不明确指定签名的情况下，安全地向目标方法应用后置补丁（postfix）。

签名：

```csharp
private static void SafePostfix(
    string label,
    string typeName,
    string methodName,
    Type patchClass,
    string patchMethodName)
```

后置补丁在原始方法之后运行，适用于需要扩展游戏行为的情况。

## 使用示例

```csharp
SafePostfix(
    "FirstScene.OnGUI",
    "FirstScene",
    "OnGUI",
    typeof(GeneralPatches),
    "FirstScene_OnGUI_Postfix");
```

补丁方法：

```csharp
private static void FirstScene_OnGUI_Postfix()
{
    LegacyWatermark.Draw();
}
```

原始 `FirstScene.OnGUI()` 正常运行，随后 BigCityLegacy 绘制水印。

## 示例：在 `Awake` 之后改变状态的后置补丁

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

## 限制

当前 `LegacyPatchManager` 不提供带 `Type[] parameters` 的 `SafePostfix` 重载。如果需要为重载方法添加后置补丁，可以向管理器添加此类重载，或使用 `PatchClass(...)` 配合常规 Harmony 特性。

---

# `SafeConstructorPostfix(string label, string typeName, Type patchClass, string patchMethodName)`

安全地向目标类型的构造函数应用后置补丁。

签名：

```csharp
private static void SafeConstructorPostfix(
    string label,
    string typeName,
    Type patchClass,
    string patchMethodName)
```

当前实现仅查找无参数构造函数：

```csharp
AccessTools.Constructor(targetType, Type.EmptyTypes)
```

如果未找到类型或构造函数，则跳过该补丁，不会造成加载错误。

## 使用示例

```csharp
SafeConstructorPostfix(
    "LocalizationManager.ctor",
    "LocalizationManager",
    typeof(GeneralPatches),
    "LocalizationManager_Ctor_Postfix");
```

补丁方法：

```csharp
private static void LocalizationManager_Ctor_Postfix(LocalizationManager __instance)
{
    LegacyLocalizer.ApplyLocalizationManagerPatch(__instance);
}
```

此后置补丁在 `LocalizationManager` 创建后运行，并应用本地化替换。

## 适用场景

以下情况下可使用 `SafeConstructorPostfix`：

- 代码必须在对象创建后立即运行；
- 没有合适的 `Awake`、`Start` 或 `Init`，或者它们运行得太晚；
- 某些游戏版本中可能缺少该类型；
- 使用无参数构造函数已足够。

## 限制

该方法不适用于带参数的构造函数。当前 `LegacyPatchManager` 没有带 `Type[] parameters` 的重载版本。请改用标准 Harmony 补丁类。

---

# `SafeGetterPrefix(string label, string typeName, string propertyName, Type patchClass, string patchMethodName)`

安全地向属性 getter 应用前置补丁。

签名：

```csharp
private static void SafeGetterPrefix(
    string label,
    string typeName,
    string propertyName,
    Type patchClass,
    string patchMethodName)
```

该方法首先通过以下方式查找 getter：

```csharp
AccessTools.PropertyGetter(targetType, propertyName)
```

如果未找到 getter，则额外尝试按方法名查找：

```csharp
AccessTools.Method(targetType, "get_" + propertyName)
```

这适用于反编译器显示为 `property`，但在 IL 中表现为常规 `get_PropertyName` 方法的属性。

## 使用示例

```csharp
SafeGetterPrefix(
    "InApp_But.isTest",
    "InApp_But",
    "isTest",
    typeof(InAppPatches),
    "InAppBut_IsTest_Prefix");
```

补丁方法：

```csharp
private static bool InAppBut_IsTest_Prefix(ref bool __result)
{
    __result = true;
    return false;
}
```

此前置补丁完全替代 `InApp_But.isTest` 的 getter，并强制返回 `true`。

## 适用场景

需要执行以下操作时，可使用 `SafeGetterPrefix`：

- 替换属性的返回结果；
- 禁止执行原始 getter；
- 避免手动指定 `get_PropertyName`；
- 保持兼容性安全行为。

---

# 示例：添加新的可选补丁

假设需要禁用 `Reporter` 类中隐藏的手势触发器，但旧构建中可能缺少该类。

在 `PatchCompatibilityMode()` 或独立的服务器/客户端补丁集中：

```csharp
SafePrefix(
    "Reporter.isGestureDone",
    "Reporter",
    "isGestureDone",
    typeof(ReporterPatches),
    "Reporter_IsGestureDone_Prefix");
```

补丁类：

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

如果缺少 `Reporter` 或 `isGestureDone`，模组会继续加载，并在调试日志中记录跳过原因。

---

# 示例：添加新的完整模式补丁

如果补丁仅面向主要受支持的游戏版本，最好使用 `PatchClass`。

在 `PatchFullMode()` 中：

```csharp
PatchClass(typeof(ServerTimeoutPatches));
```

补丁类：

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

这种方式更易于阅读和维护，因为目标类型和方法直接写在补丁方法的上方。

---

# 常见补丁方法签名

## 跳过原始方法的前置补丁

```csharp
private static bool Target_Method_Prefix()
{
    return false;
}
```

## 允许原始方法运行的前置补丁

```csharp
private static bool Target_Method_Prefix()
{
    return true;
}
```

## 替换返回结果的前置补丁

```csharp
private static bool Target_Method_Prefix(ref bool __result)
{
    __result = true;
    return false;
}
```

## 可访问实例的前置补丁

```csharp
private static bool Target_Method_Prefix(TargetType __instance)
{
    // Work with instance.
    return true;
}
```

## 可访问实例的后置补丁

```csharp
private static void Target_Method_Postfix(TargetType __instance)
{
    // Run after original method.
}
```

## 构造函数后置补丁

```csharp
private static void Target_Ctor_Postfix(TargetType __instance)
{
    // Run after object construction.
}
```

---

# 错误处理行为

`PatchClass(...)` 捕获异常并写入 `LogError`。这意味着补丁类应用失败被视为需要关注的问题。

`Safe*` 方法捕获错误，并通过 `LogSkipped(...)` 写入 `LogDebug`。这意味着目标缺失被视为可以接受的情况。

---
