# Release 发布流程

适用于本仓库中所有 Mod 的通用发布规范。

## 前置条件
- 所有代码变更已完成并通过本地验证（编译、测试）
- 已确定版本号（遵循 SemVer）

## 步骤

### 1. 更新版本号

修改该 Mod 目录下的以下两个文件，版本号保持一致：

- `manifest.json` — SMAPI 模组清单
- `{ModName}.csproj` — 项目文件

```
manifest.json:        "Version": "x.y.z"
{ModName}.csproj:     <Version>x.y.z</Version>
```

### 2. Build Release

```bash
dotnet build {ModName}/ -c Release
```

Build 成功后会生成 zip 文件：
```
/mnt/f/StarVelleyModsBuilds/Release/{ModName} x.y.z.zip
```

### 3. 提交代码变更

将版本号变更和代码变更一并提交：

```bash
git add -A
git commit -m "{ModName} x.y.z: {英文变更摘要}"
```

### 4. 创建并推送 Tag

Tag 格式：`{项目名}-v{x.y.z}`

```bash
git tag {ModName}-vx.y.z
git push origin master --tags
```

### 5. 创建 GitHub Release

```bash
gh release create {ModName}-vx.y.z \
  --title "{ModName} x.y.z" \
  --notes "$(cat <<'EOF'
{ModName} x.y.z / x.y.z

- {英文变更1} {中文变更1}
EOF
)" \
  "/mnt/f/StarVelleyModsBuilds/Release/{ModName} x.y.z.zip"
```

## Release 说明格式规范

- 中文 + 英文双语
- 仅包含功能上的变更

```
{ModName} x.y.z / x.y.z

- Fix ... 修复 ...
- Add ... 添加 ...
```
