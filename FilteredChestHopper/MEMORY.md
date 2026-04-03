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
  └─ 步骤3: GetTransferAmount() - 检查过滤器并返回转移数量
           │
           ├─ TransferWithQuantityLimit() - 统一处理所有转移
           │
           └─ TransferAllItems() - 计算全部转移数量，调用 TransferWithQuantityLimit
```

### 4. 转移数量返回值约定

| 返回值 | 含义 |
|--------|------|
| 0 | 未匹配过滤器，不转移 |
| -1 | 全部转移模式 |
| >0 | 精确数量模式 |

### 5. 加工产物识别

使用上下文标签 (context tags) 识别：
- `preserve_sheet_index_{ID}` - 记录原始物品ID
- 例如：蓝莓酒 = 348 + preserve_sheet_index_452

### 7. 代码重构记录

#### TransferWithQuantityLimit 统一转移逻辑

所有转移都通过 `TransferWithQuantityLimit` 处理：
- 计算目标箱中已存在的同类物品数量
- 创建加工产物（如葡萄酒）
- 限制转移数量
- 尝试放入目标箱
- 清理原箱中的物品

#### TransferAllItems 简化为计算器

`TransferAllItems` 现在只负责计算全部转移数量，然后调用 `TransferWithQuantityLimit`
```

### 8. 代码风格

- `{" 保持在行末（K&R 风格）
- 复杂 if 条件用多个 if 进行拆分
