# SmartFilteredHopper MOD 学习笔记

## 2026-04-06 会话内容

### 1. 代码结构理解

```
ModEntry
  └── Dictionary<GameLocation, LocationManager.Manager>
        └── Manager.IOGroups: List<HopperIOGroup>
              ├── Hopper (Chest)
              ├── IInputGroup (ChestWrap / AutomateChestGroup)
              └── Output Chest
```

### 2. 核心类

- **Manager**: 管理单个 GameLocation 下的所有 Hopper IO 组
- **HopperIOGroup**: 管理单个 hopper 的输入输出关系
- **IInputGroup**: 接口，抽象输入组
  - **ChestWrap**: 包装单个 chest
  - **AutomateChestGroup**: flood fill 查找所有连接的 chest 和 machine

### 3. 事件处理流程

```
ObjectListChanged
  ├── e.Removed: HandleHopperRemoved / HandleChestChanged
  └── e.Added: HandleHopperAdded / HandleChestChanged

HandleChestChanged → buildLocationManager(location) 重建整个 manager
```

### 4. IInputGroup 接口

```csharp
internal interface IInputGroup {
    Chest StartChest { get; }
    bool Contains(Chest chest);
    void RemoveItem(Item item, int count);
    List<Item> GetItems();
}
```

### 5. Filter Items 来源

Filter items 存储在 **hopper 本身**的 Items 里，不是 input chest。

### 6. Automate API

使用 `GetMachineStates(location, area)` 获取 machine 状态，用于 flood fill 时判断是否为 machine。

### 7. Config 保存时机

配置保存时（用户点 Save）才触发 rebuild，通过 `RegisterConfigMenu` 的 `save` 回调传入 `onAutomateChanged` action。

### 8. 配置项

| 配置项 | 作用 |
|--------|------|
| LogLevel | 日志级别 |
| CompareQuality | 是否比较物品品质 |
| TransferInterval | 转移间隔（帧数） |
| GrabAutomateChestGroup | 是否启用 Automate Group |
