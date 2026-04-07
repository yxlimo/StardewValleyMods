# Smart Filtered Hopper 智能加料器

一个智能加料器模组，支持根据过滤物品在输入箱子和输出箱子之间自动转移物品，并集成 Automate。

## 功能

- **物品过滤** — 根据加料器中存储的过滤物品，自动将匹配物品从输入箱子转移到输出箱子
- **Automate 集成** — 与 Automate 存储网络配合，从所有连接的箱子中获取物品
- **品质比较** — 可选择同时根据物品品质进行匹配
- **可调的转移速度** — 调整物品自动转移的频率（按帧数）
- **通用模组配置菜单** — 所有选项可通过 GMCM 配置

## 工作原理

1. 在加料器上方放置一个箱子 — 这是**输入箱子**
2. 在加料器下方或旁边放置一个箱子 — 这是**输出箱子**
3. 将过滤物品放入**加料器**中（就像普通箱子一样操作）
4. 输入箱子中匹配过滤条件的物品将自动转移到输出箱子

### Automate 集成

当**连接 Automate 箱子组**启用且你安装了 [Automate](https://www.nexusmods.com/stardewvalley/mods/1063) 时：
- 如果输入箱子是 Automate 存储网络的一部分，加料器会从该网络中的**所有箱子**拉取物品
- 非常适合拥有复杂存储系统的大型农场

## 配置

| 设置 | 默认值 | 描述 |
|---|---|---|
| 启用调试 | 关闭 | 启用详细调试日志 |
| 比较品质 | 关闭 | 同时根据物品品质进行匹配 |
| 转移间隔 | 360 | 物品转移频率（1 = 每帧，60 = 每秒） |
| 连接 Automate 箱子组 | 关闭 | 从 Automate 组中的所有箱子拉取物品 |

## 安装

1. 安装 [SMAPI](https://smapi.io/)
2. 将 `SmartFilteredHopper` 文件夹放入你的 `Mods` 目录
3. 启动游戏

## 兼容性

- 星露谷 1.6+
- SMAPI 4.0+
- 支持 [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)
- 与 [Automate](https://www.nexusmods.com/stardewvalley/mods/1063) 配合使用

