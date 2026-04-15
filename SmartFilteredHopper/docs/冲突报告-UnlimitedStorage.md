# SmartFilteredHopper 与 UnlimitedStorage 冲突分析

## 1. 冲突概述

当 SmartFilteredHopper 和 UnlimitedStorage 同时启用时，SmartFilteredHopper 的 hopper 容量补丁可能无法正确生效，导致 UI 仍显示 36 格而非配置的容量值。

## 2. 技术原理

### 2.1 SmartFilteredHopper 的补丁方式

SmartFilteredHopper 使用 **Harmony Postfix** 修改 `Chest.GetActualCapacity`：

```csharp
// HopperChestPatches.cs
harmony.Patch(
    original: AccessTools.Method(typeof(Chest), nameof(Chest.GetActualCapacity)),
    postfix: new HarmonyMethod(typeof(HopperChestPatches), nameof(GetActualCapacity_Postfix))
);

private static void GetActualCapacity_Postfix(Chest __instance, ref int __result) {
    if (__instance.modData.TryGetValue(CapacityModDataKey, out string val)
        && int.TryParse(val, out int cap) && cap > 0) {
        __result = cap;  // 修改返回值
    }
}
```

### 2.2 UnlimitedStorage 的补丁方式

UnlimitedStorage 使用 **Transpiler** 修改 `ItemGrabMenu` 构造函数：

```csharp
// ModPatches.cs (UnlimitedStorage)
harmony.Patch(
    AccessTools.GetDeclaredConstructors(typeof(ItemGrabMenu))[...],
    transpiler: new HarmonyMethod(typeof(ModPatches), nameof(ItemGrabMenu_constructor_transpiler))
);

// Transpiler 逻辑：把 GetActualCapacity() 调用替换成 wrapper
private static IEnumerable<CodeInstruction> ItemGrabMenu_constructor_transpiler(...) {
    // 在构造函数中查找 GetActualCapacity() 调用
    // 替换成：GetActualCapacity(GetActualCapacity(), context)
    matcher.InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldarg_S, (short)16),  // 加载 context 参数
        CodeInstruction.Call(typeof(ModPatches), nameof(GetActualCapacity))
    );
}

private static int GetActualCapacity(int capacity, object? context) =>
    (context as Chest)?.SpecialChestType switch {
        Chest.SpecialChestTypes.BigChest => 70,
        not null => ModState.Config.BigChestMenu ? 70 : 36,
        _ => capacity  // context 为 null 时返回原始值
    };
```

## 3. 冲突机制

### 3.1 Postfix vs Transpiler 执行顺序

| 补丁类型 | 执行时机 | 能否被绕过 |
|----------|----------|------------|
| **Postfix** | 原方法执行完毕后 | 可以被 Transpiler 绕过 |
| **Transpiler** | IL 编译阶段 | 无法被 Postfix 影响 |

### 3.2 冲突过程

1. `ItemGrabMenu` 构造函数中有调用 `GetActualCapacity()`
2. **Transpiler 先执行**：把调用改成 `GetActualCapacityWrapper(GetActualCapacity(), context)`
3. **Postfix 后执行**：但它修改的是原始 `GetActualCapacity()` 的返回值
4. `stloc` 指令存入的是 **wrapper 的返回值**，而非 postfix 修改后的值

```
原 IL:
    call GetActualCapacity()      ← Postfix 在这里修改了 __result
    stloc capacity

Transpiler 插入后:
    call GetActualCapacity()      ← Postfix 把 __result 改成 54
    ldarg.s 16                    ← Transpiler 插入
    call GetActualCapacityWrapper ← wrapper 返回 36
    stloc capacity                ← 最终存的是 36，不是 54！
```

## 4. 为什么 Hopper 不受 UnlimitedStorage 影响

UnlimitedStorage 的 `SpecialChestType` Postfix 条件：

```csharp
if (ModState.Config.BigChestMenu &&
    __result is Chest.SpecialChestTypes.None or Chest.SpecialChestTypes.JunimoChest) {
    __result = Chest.SpecialChestTypes.BigChest;
}
```

Hopper 的 `SpecialChestType` 是 `AutoLoader`，不是 `None` 或 `JunimoChest`，所以：
- `SpecialChestType` 不会被改成 `BigChest`
- `GetActualCapacity` wrapper 会走 `_ => capacity` 分支，返回原始值

**因此 UnlimitedStorage 的 BigChestMenu 功能对 hopper 不生效。**

## 5. 解决方案

### 方案 A：使用 Transpiler（推荐）

改用 Transpiler 直接修改 IL，检测到是我们的 chest 时直接返回我们的值：

```csharp
private static int GetActualCapacityOverride(Chest? chest, int originalCapacity) {
    if (chest != null &&
        chest.modData.TryGetValue("SmartFilteredHopper/Capacity", out string val) &&
        int.TryParse(val, out int cap) && cap > 0) {
        return cap;
    }
    return originalCapacity;  // 返回原始调用结果，兼容其他 mod
}
```

### 方案 B：检测冲突 mod

在 `ModEntry` 中检测 `UnlimitedStorage` 是否存在，运行时调整策略。

### 方案 C：联系作者协调

建议用户向 UnlimitedStorage 作者反馈，协商补丁执行顺序或使用不同的注入点。

### 方案 D：等待游戏/框架支持

使用 SMAPI 的 `IModPatcher` 接口（如果有）来协调补丁顺序。

## 6. 当前状态

**问题**：Postfix 方式在有其他 Transpiler 修改相同调用时可能失效。

**影响**：
- SmartFilteredHopper 的容量补丁对 hopper **仍然生效**（日志显示 `__result` 被正确修改为配置值）
- UI 显示问题可能是 `ItemGrabMenu` 内部布局算法导致的，非补丁失效

**验证**：日志 `[GetActualCapacity] AFTER: __result=54` 说明 postfix 正确执行，修改已生效。

## 7. 参考资料

- [Harmony 官方文档 - patching](https://harmony.pardeiki.net/?.readthedocs=en/stable/articles/patching.html)
- [Harmony Transpiler 教程](https://harmony.pardeiki.net/?.readthedocs=en/stable/articles/ patching/transpiling.html)
- [UnlimitedStorage 源码](../OtherStardewValleyMods/UnlimitedStorage)

## 8. 相关文件

| 文件 | 说明 |
|------|------|
| `Framework/HopperChestPatches.cs` | SmartFilteredHopper 容量补丁实现 |
| `ModEntry.cs` | Mod 入口，注册补丁 |
| `../OtherStardewValleyMods/UnlimitedStorage/Services/ModPatches.cs` | UnlimitedStorage 补丁实现 |
