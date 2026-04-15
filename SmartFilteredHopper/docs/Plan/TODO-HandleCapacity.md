# 滚动支持 TODO

## 状态：待实现

当前方案 A 不包含滚动支持，最大容量限制为 **72 格**。

如需支持超过 72 格的容量，需要实现滚动条。

## 参考实现

### BiggerAutoGrabber 方案（InventorySlice）

关键文件：
- `Framework/InventorySlice.cs` — 窗口化视图，支持滚动
- `Framework/ChestPatches.cs` — 滚动相关补丁

需要添加的补丁：

| # | 目标方法 | 类型 | 作用 |
|---|----------|------|------|
| 1 | `IClickableMenu.receiveScrollWheelAction` | Postfix | 滚动支持 |
| 2 | `ItemGrabMenu.draw` | Postfix | 绘制滚动箭头 |
| 3 | `ItemGrabMenu.receiveLeftClick` | Prefix | 处理箭头点击 |

### BetterChests 方案（Skip/Take）

关键文件：
- `BetterChests/Framework/Services/MenuManager.cs`

```csharp
// OnItemsDisplaying - 用 Skip/Take 分页
e.Edit(items => items.Skip(this.scrolled * this.Columns).Take(this.Capacity));
```

## 触发条件

当需要支持超过 72 格时，开启此 TODO。
