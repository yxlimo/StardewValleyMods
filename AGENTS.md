# CLAUDE.md

yxlimo 的星露谷 Mod 合集

## Memory
修改项目前需确保已阅读相关的文档
- [智能加载器(SmartFilteredHopper)](./SmartFilteredHopper/docs/design.md)
- [Release 发布流程](./docs/release-workflow.md)

## Rules

- **每次修改完代码都要检查 IDE 是否有报错** — 修改完成后立即调用 `mcp__ide__getDiagnostics` 检查文件是否有编译错误或警告，及时修复。

## 代码规范
### `this` 关键字
- 禁止省略 `this` 关键词

### 复合赋值语法糖规范
- 禁止 `??=`
- 允许 `+=`, `-=`, `*=`, `/=`, `%=` 等

### 大小写规定
- class 内私有变量/函数以小写开头
- public 变量/函数以大写开头
