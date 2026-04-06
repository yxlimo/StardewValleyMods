# TODO

## FilteredItem 简化

### 问题分析

**背景**：Stardew Valley 的 preserves（葡萄酒、果酱、泡菜等）有"风味"概念。例如 Copper Bar + Wine Preserve → Copper Wine。

**`GetFlavoredObjectVariant` 的逻辑**：
```csharp
StardewValley.Object processedItem = new(processedItemID, 1);  // 用 preserve ID 新建对象
newItem = Utill.GetFlavoredObjectVariant(item as StardewValley.Object, processedItem).CreateItem();
```

`processedItemID` 从 `item` 的 context tag `"preserve_sheet_index_348"` 提取（348 = Wine Preserve）。然后用 `processedItemID` 重建一个 `StardewValley.Object`，再传给 `CreateFlavoredWine`。

**为什么这样设计**：`CreateFlavoredWine(processedItem)` 需要知道 preserve 的类型（通过 `processedItem.ItemId`）来决定生成什么"风味"的葡萄酒。

**问题**：既然 `item` 本身已经携带了 preserve 信息（context tags），为什么还要从 `processedItemID` 重建一个 `StardewValley.Object` 再传进去？`CreateFlavoredX` 内部到底用 `processedItem` 做什么？

**可能的解法**：

1. **直接传入 `item`**：如果 `CreateFlavoredX` 只需要 preserve ID，用 `item` 的 preserve ID 直接调用即可，不需要重建对象

2. **移除 SMAPI 内部依赖**：调研 Stardew 是否有公开 API 创建 flavored item，或直接用 `(Item)new Object(preserveId, 1)` + `CreateFlavoredX` 的简化版

3. **如果 `CreateFlavoredX` 只需 `processedItem.ItemId`**：可以简化 `GetFlavoredObjectVariant` 接收 `string preserveId` 直接创建，不需要 `FilteredItem` 包装

## 多个 Hopper 共享 Automate Group 问题

**问题**：当两个 hopper 的 input 指向同一个 Automate group 时，物品传递行为不正确。

**场景**：
- Hopper-A filter: 物品 A
- Hopper-B filter: 物品 B
- 两个 hopper 共享同一个 Automate group 的 input chests
- 放入物品 B 时，应该走 Hopper-B output，却走了 Hopper-A output

**根因分析**：待确认 `ProcessInputChest` 中 `shouldTransfer` 的匹配逻辑

**状态**：日志已添加，待游戏内验证
