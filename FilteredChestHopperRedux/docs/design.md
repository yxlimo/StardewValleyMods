# 代码组织设计

## 架构概览

```
ModEntry
  └── Dictionary<GameLocation, LocationManager>
        └── LocationManager
              └── List<HopperIOGroup>
                    ├── Hopper (Chest)
                    ├── IInputGroup (ChestWrap / AutomateChestGroup)
                    └── Output Chest
```

## 核心类

### LocationManager
管理单个 GameLocation 下的所有 Hopper IO 组。
- `IOGroups`: 该 location 下所有 hopper 的输入输出组
- `Add(Chest hopper)`: 添加 hopper 并自动查找其上下的 chest
- `RemoveGroupByHopper(Chest hopper)`: 根据 hopper 移除对应的 IOGroup
- `AttemptTransfer()`: 遍历所有 IOGroup 执行物品转移

### HopperIOGroup
管理单个 hopper 的输入输出关系。
- `Hopper`: 实际的 hopper 对象
- `InputGroup`: 输入组接口，支持单个 chest 或 Automate 连接的一组 chest
- `Output`: 输出 chest

### IInputGroup 接口
抽象输入组，支持两种实现：
- **ChestWrap**: 包装单个 chest
- **AutomateChestGroup**: Automate 连接的一组 chest（flood fill 算法）

接口定义：
```csharp
internal interface IInputGroup {
    Chest StartChest { get; }
    bool Contains(Chest chest);
    void RemoveItem(Item item, int count);
    List<Item> GetItems();
}
```

## 事件处理流程

### ObjectListChanged 处理原则
1. **类型检查在 ObjectListChanged 完成**：遍历 `e.Removed` / `e.Added`，根据对象类型分发
2. **Handler 接收强类型参数**：例如 `HandleHopperAdded(Chest hopper)` 而非 `HandleHopperAdded(Object obj)`
3. **非目标类型直接跳过**：不在 handler 中做类型检查

### 分发逻辑
```csharp
private void ObjectListChanged(object sender, ObjectListChangedEventArgs e) {
    foreach (var pair in e.Removed) {
        if (Utill.TryExtractHopper(pair.Value, out var hopper)) {
            this.HandleHopperRemoved(hopper, e.Location);
        } else if (pair.Value is Chest) {
            this.HandleChestChanged(e.Location);
        }
    }
    // similar for e.Added
}
```

### Handler 职责
- **HandleHopperRemoved**: 从 LocationManager 中移除对应 IOGroup，清理 modData
- **HandleHopperAdded**: 获取或创建 LocationManager，调用 Add(hopper)
- **HandleChestChanged**: 重建该 location 下的所有 Pipelines（因为 Automate Group 可能包含多个 chest）

## 设计原则

1. **单一职责**：每个类/方法只做一件事
2. **接口抽象**：IInputGroup 支持不同的输入组实现
3. **事件驱动**：ObjectListChanged 触发时进行精细处理，而非全局重建
4. **Location 隔离**：按 GameLocation 组织管理不同区域的 hopper

## 代码规范

### 事件处理规范
- **类型检查在事件分发层完成**: 在 `ObjectListChanged` 等事件处理器中进行类型检查
- **Handler 接收强类型参数**: 例如 `HandleHopperAdded(Chest hopper)` 而非 `HandleHopperAdded(Object obj)`
- **Handler 内部不再做类型检查**: 假设参数类型正确

### Manager 类设计
- **Manager 初始化为空**: 通过 `Add()` 方法添加成员
- **提供 `RemoveXxx(identifier)` 方法**: 移除特定成员
- **不在构造函数中直接注入**: 需要复杂查找逻辑的依赖

## 今日工作摘要 (2026-04-05)

### 1. ObjectListChanged 重构
按照 PRD step 3 重构了事件处理逻辑：
- 新增 `HandleHopperRemoved`: 从 LocationManager 移除对应 IOGroup，清理 modData
- 新增 `HandleHopperAdded`: 获取或创建 LocationManager，调用 Add(hopper)
- 新增 `HandleChestChanged`: 重建该 location 下的所有 Pipelines
- 类型检查提到 ObjectListChanged 中，handler 接收强类型参数

### 2. LocationManager.RemoveGroupByHopper
新增方法，通过遍历 IOGroups 匹配 hopper 并移除，同时清理 modData

### 3. 全项目重命名
- `Pipeline` → `LocationManager`
- `Pipeline.cs` → `LocationManager.cs`
- `BuildPipeline` → `BuildLocationManager`
