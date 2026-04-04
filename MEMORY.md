# 代码风格偏好

## C# 规范

### 永远使用 `this` 关键字
- `csharp_style_this = explicit:warning`

### 永远不要使用复合赋值语法糖
- 禁止 `??=`
- 允许 `+=`, `-=`, `*=`, `/=`, `%=` 等

### K&R 风格
- `{` 保持在语句末尾，不单独一行
- `csharp_new_line_before_open_brace = false`

### .editorconfig 配置

```ini
[*.cs]
csharp_new_line_before_open_brace = false
csharp_style_this = explicit:warning
dotnet_diagnostic.IDE0074.severity = silent  # 禁用 ??=
dotnet_diagnostic.IDE0003.severity = silent
```

### namespace 和 internal

- `namespace` = 代码组织 + 避免名字冲突，同项目内跨 namespace 可自由访问
- `internal` = 同 assembly 内可见，跨 DLL 时才有限制
- 同项目内 `internal` 和 `public` 效果相同

### static 使用场景
- 不访问实例成员时使用
- 工具类方法：`GetItemsFlavourID()`, `TryCreate()`
