# SmartFilteredHopper 技术文档

## 架构概览

```
ModEntry
  └── Dictionary<GameLocation, LocationManager>
        └── LocationManager
              └── List<HopperIOGroup>
                    ├── Hopper (Chest, SpecialChestTypes.AutoLoader)
                    ├── IInputGroup (ChestWrap / AutomateChestGroup)
                    └── Output Chest
```

## 核心概念

### HopperIOGroup

管理单个 Hopper 的输入输出关系：
- `Hopper`: 实际的 hopper 对象（Chest with SpecialChestTypes.AutoLoader）
- `InputGroup`: 输入源接口，支持单个 Chest 或通过 Automate flood-fill 发现的 Chest 组
- `Output`: 输出 Chest（hopper 下方紧邻的 Chest）

### IInputGroup 接口

```csharp
internal interface IInputGroup {
    /// 起点 tile 坐标
    Vector2 StartTile { get; }
    bool Contains(Chest chest);
    void RemoveItem(Item item, int count);
    List<Item> GetItems();
}
```

两种实现：
- **ChestWrap**: 包装单个 Chest，`StartTile` 即该 Chest 的 TileLocation
- **AutomateChestGroup**: 从种子位置开始 BFS flood-fill，发现所有连接的 Chest 和 Machine

### ConnectorType 枚举

表示输入 tile 的类型（`LocationManager/Manager.cs`）：

```csharp
internal enum ConnectorType {
    None,     // 不可连接
    Chest,    // Chest tile
    Machine,  // Automate 认识的机器 tile
    Flooring  // 地板 path（用户启用时）
}
```

## 配置项

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `LogLevel` | int | 0 | 日志级别 |
| `HopperCapacity` | int | 36 | Hopper 容量（仅支持 12 的倍数） |
| `CompareQuality` | bool | false | 过滤器同时检查物品品质 |
| `CompareArtifactSource` | bool | true | 工匠物品检查来源（如果酒风味） |
| `TransferInterval` | int | 360 | 转移间隔（帧数） |
| `GrabAutomateChestGroup` | bool | false | 连接 Automate 箱子组 |
| `FlooringAsInput` | bool | false | 允许地板作为 Hopper 输入（仅在 GrabAutomateChestGroup 启用时生效） |

## findHopperConnector

查找 hopper 上下方的连接件：

```csharp
private (Vector2? inputPos, Chest output) findHopperConnector(Chest hopper)
```

- **output**: hopper 紧下方（`TileLocation + (0, 1)`）的 Chest
- **inputPos**:
  - 若上方是 Chest → 返回该 Chest tile 坐标
  - 若上方非 Chest 且 `AutomateEnabled()` → 由 `getConnectorType` 判断是否 Machine 或 Flooring
  - 否则返回 `null`

### getConnectorType

```csharp
private ConnectorType getConnectorType(Vector2 tile, GameLocation location)
```

| 类型 | 判断条件 |
|------|----------|
| Chest | `location.objects[tile] is Chest` 且非 hopper |
| Machine | `GetAutomateMachineStates()[tile]` 存在 |
| Flooring | `Config.FlooringAsInput` && `location.terrainFeatures[tile] is Flooring` |
| None | 以上都不满足 |

## AutomateChestGroup Flood-Fill

从种子 `Vector2 startPos` 开始 BFS，遍历四向邻居，收集：

- 所有非 hopper 的 Chest
- 所有 Automate 认识的 Machine
- Flooring 作为 connector（`feature is Flooring`）

### isConnectableTile（BFS 邻居判断）

严格按 Automate 标准，**不额外扩展** machine 检测范围：

```csharp
private bool isConnectableTile(Vector2 tile, GameLocation location) {
    // Chest（排除 hopper）
    if (location.objects.TryGetValue(tile, out var obj) && obj is Chest chest) {
        return !Utill.IsHopper(chest);
    }
    // Machine via Automate API
    if (this.ctx.GetAutomateMachineStates(location).ContainsKey(tile)) return true;
    // Flooring connector
    if (location.terrainFeatures.TryGetValue(tile, out var feature)
        && feature is StardewValley.TerrainFeatures.Flooring) return true;
    return false;
}
```

## 事件处理

| 事件 | 触发条件 | 处理逻辑 |
|------|----------|----------|
| `SaveLoaded` | 存档加载完成 | 重建所有 LocationManager |
| `DayStarted` | 游戏日开始 | 重建所有 LocationManager |
| `ObjectListChanged` | 物体增删 | Hopper/Chest 变动分发到对应 Handler |
| `TerrainFeatureListChanged` | 地形特征变动 | `FlooringAsInput` 启用时，仅 Flooring 变动触发重建 |
| `UpdateTicked` | 每帧 | 按 `TransferInterval` 执行 `AttemptTransfer` |

### Handler 分工

- `HandleHopperRemoved`: 从 LocationManager 移除对应 IOGroup，清理 modData
- `HandleHopperAdded`: 获取或创建 LocationManager，调用 `Add(hopper)`
- `HandleChestChanged`: 调用 `buildLocationManager(location)` 重建该 location

## Hopper 容量（Harmony Patch）

通过 Harmony Postfix 拦截 `Chest.GetActualCapacity`：

```csharp
[HarmonyPatch(typeof(Chest), nameof(Chest.GetActualCapacity))]
private static class ChestGetActualCapacityPatch {
    [HarmonyPostfix]
    private static void GetActualCapacity_Postfix(Chest __instance, ref int __result) {
        if (__instance.modData.TryGetValue(CapacityModDataKey, out string val)
            && int.TryParse(val, out int cap)
            && cap > 0) {
            __result = cap;
        }
    }
}
```

容量值存储在 `modData["SmartFilteredHopper/Capacity"]`，通过 `stampHopperCapacity()` 在存档加载、游戏开始、配置保存时更新。

## 设计原则

1. **单一职责**: 每个类/方法只做一件事
2. **接口抽象**: IInputGroup 支持不同的输入组实现
3. **事件驱动**: 精细处理变更，而非全局重建
4. **Location 隔离**: 按 GameLocation 组织管理不同区域的 hopper
5. **Automate 兼容**: 严格对齐 Automate 标准，不做超出其能力的假设
