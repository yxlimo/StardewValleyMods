# CLAUDE.md

## Rules

- **每次修改完代码都要检查 IDE 是否有报错** — 修改完成后立即调用 `mcp__ide__getDiagnostics` 检查文件是否有编译错误或警告，及时修复。

## C# 规范
### 永远使用 `this` 关键字
- `csharp_style_this = explicit:warning`

### 永远不要使用复合赋值语法糖
- 禁止 `??=`
- 允许 `+=`, `-=`, `*=`, `/=`, `%=` 等
