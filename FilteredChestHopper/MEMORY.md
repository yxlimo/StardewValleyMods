# FilteredChestHopper MOD 学习笔记

## 2025-04-03 会话内容

### 1. 代码结构理解

这个 MOD 实现的功能：
- 给游戏中的 Hopper（投送器）添加输入输出功能
- 读取 Hopper 上方宝箱的物品，根据白名单过滤，传送到下方宝箱

### 2. 关键文件

| 文件 | 作用 |
|------|------|
| Mod.cs | 主入口，事件监听 |
| Pipeline.cs | 核心传输逻辑 |
| ModConfig.cs | 配置文件 |

### 3. Pipeline.cs 核心逻辑

```
 AttemptTransfer()
  │
  ├─ 步骤1: CollectInputAndOutputChests() - 收集输入输出宝箱
  │
  ├─ 步骤2: ProcessInputChest() - 处理单个输入宝箱
  │
  └─ 步骤3: ShouldTransfer() - 检查过滤器
       │
       └─ TransferItem() - 执行转移
```

### 4. 转移数量返回值约定

| 返回值 | 含义 |
|--------|------|
| true | 匹配过滤器，执行转移 |
| false | 未匹配，不转移 |

### 5. 加工产物识别

使用上下文标签 (context tags) 识别：
- `preserve_sheet_index_{ID}` - 记录原始物品ID
- 例如：蓝莓酒 = 348 + preserve_sheet_index_452

### 6. 关键类

- `Pipeline` - 主管道逻辑
- `HopperIOGroup` - 按 hopper 分组的输入输出数据结构 `{ Hopper, Inputs[], Output }`
- `ChestLeftToRight` - hopper 从左到右排序比较器

### 7. ModConfig 注册模式

配置选项注册在 `ModConfig.RegisterOptions()` 方法中，由 `Mod.GameLaunched()` 调用。

### 8. 配置项

| 配置项 | 作用 |
|--------|------|
| CompareQuality | 是否比较物品品质 |
| TransferInterval | 转移间隔（帧数） |
| AutomateRespect | 是否尊重 Automate mod |
