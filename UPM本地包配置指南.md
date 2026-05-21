# UPM 本地包配置指南（ForgeCLR）

## 背景

ForgeCLR 使用 git 子模块管理 Bridge、Depot 和 UniTask，但 Unity Package Manager (UPM) 不会自动识别 `Assets` 下的文件夹为"包"。这就导致两个问题：

1. `Samples~` 目录无法通过 Package Manager 导入
2. 包之间的依赖无法通过 `package.json` 正确解析

解决方案：将这些文件夹注册为 **本地 UPM 包**（`file:` 引用），让 Unity 把它们当作正式包处理。

---

## 涉及到的四个包

| 包名 | 路径 | 说明 |
|------|------|------|
| `com.voyageforge.forgeclr` | `Assets/ForgeCLR` | ForgeCLR 热更新模板 |
| `com.voyageforge.bridge` | `Assets/ForgeCLR/Plugins/Bridge` | 通信连接模块 |
| `com.voyageforge.depot` | `Assets/ForgeCLR/Plugins/Depot` | 基础工具仓库 |
| `com.cysharp.unitask` | `Assets/ForgeCLR/Plugins/UniTask` | 异步任务库 |

每个包根部都有一个 `package.json`，这是 UPM 识别包的唯一标识。

---

## 操作步骤

### 第一步：确保每个包都有 `package.json`

对于 ForgeCLR，我们创建了以下 `package.json`：

```json
{
  "name": "com.voyageforge.forgeclr",
  "version": "0.1.0",
  "displayName": "ForgeCLR",
  "description": "ForgeCLR integrates HybridCLR, YooAssets, and UniTask into a reusable hot-update workflow.",
  "unity": "2022.3",
  "dependencies": {
    "com.voyageforge.bridge": "0.0.3",
    "com.voyageforge.depot": "0.0.2"
  },
  "author": {
    "name": "VoyageForge",
    "url": "https://github.com/VoyageForge/ForgeCLR"
  },
  "license": "MIT"
}
```

关键字段说明：

- `name`：包的唯一标识符，全局不可重复。必须与 asmdef 引用中使用的名称一致
- `version`：语义化版本号，依赖它的其他包会据此解析版本
- `dependencies`：声明此包依赖的其他包及其最低版本要求

Bridge 已有的 `package.json`：

```json
{
  "name": "com.voyageforge.bridge",
  "version": "0.0.3",
  "dependencies": {
    "com.voyageforge.depot": "0.0.2",
    "com.unity.nuget.newtonsoft-json": "3.2.1",
    "com.cysharp.unitask": "2.5.10"
  }
}
```

这里 Bridge 声明依赖 `com.cysharp.unitask`。UPM 解析依赖时，会去 `manifest.json` 中查找这个包名。如果找不到，就会报错：

```
Package com.voyageforge.bridge has invalid dependencies:
  com.cysharp.unitask (dependency): Package cannot be found
```

### 第二步：在 `manifest.json` 中注册本地包

文件路径：`Packages/manifest.json`

在 `dependencies` 中添加四行 `file:` 引用：

```json
{
  "dependencies": {
    "com.cysharp.unitask": "file:../Assets/ForgeCLR/Plugins/UniTask",
    "com.voyageforge.depot": "file:../Assets/ForgeCLR/Plugins/Depot",
    "com.voyageforge.bridge": "file:../Assets/ForgeCLR/Plugins/Bridge",
    "com.voyageforge.forgeclr": "file:../Assets/ForgeCLR",
    
    // 以下为原有内容...
    "com.code-philosophy.hybridclr": "...",
    "com.tuyoogame.yooasset": "2.3.18"
  }
}
```

`file:` 路径相对于项目根目录。路径可以指向 `Assets` 下的任意文件夹。

**注册后的效果：**
- Package Manager 窗口中可以看到这四个包
- 有 `Samples~` 目录的包（如 Depot）会显示 **Import Samples** 按钮
- 包之间的依赖关系自动解析

---

## asmdef GUID 引用问题

### 为什么需要修改 GUID 引用？

这是最关键的一步，也是最容易被忽略的。

Unity 中每个文件都有唯一的 GUID（存在 `.meta` 文件中）。`.asmdef` 文件通过 GUID 来引用其他程序集：

```json
{
    "name": "VoyageForge.ForgeCLR.Runtime",
    "references": [
        "GUID:057b9764972b6ce4fa25125cdb2ad34f",  // Bridge.Runtime
        "GUID:0286d4cd81cb0c245a7c127e333f132c"   // Depot.Runtime
    ]
}
```

**问题在于**：当一个文件夹被注册为 UPM 包后，Unity 会**重新分配**该文件夹内所有文件的 GUID（因为包被视为一个新的导入上下文）。旧的 GUID 全部失效，所有引用断裂，编译报错。

### 解决方案：改用程序集名称引用

asmdef 支持三种引用方式：

| 方式 | 示例 | 稳定性 |
|------|------|--------|
| GUID 引用 | `"GUID:057b9764..."` | 包注册后断裂 |
| 程序集名称引用 | `"VoyageForge.Bridge.Runtime"` | 始终稳定 |
| 包名引用 | `"com.voyageforge.bridge"` | 始终稳定 |

我们将所有**跨包**的 GUID 引用改为程序集名称引用，这样无论 GUID 如何变化，引用都能正确解析。

### 本项目中哪些 GUID 需要改？

下表是所有涉及到的程序集和它们的 GUID：

| GUID | 程序集名 | 归属 | 处理 |
|------|---------|------|------|
| `0286d4cd81cb0c245a7c127e333f132c` | `VoyageForge.Depot.Runtime` | Depot 包 | **改为程序集名** |
| `f1e71fae4e4c45f43b78676eaa8a47d6` | `VoyageForge.Depot.Editor` | Depot 包 | **改为程序集名** |
| `057b9764972b6ce4fa25125cdb2ad34f` | `VoyageForge.Bridge.Runtime` | Bridge 包 | **改为程序集名** |
| `d9d0df41c4b250e4ca62392c728c3c98` | `VoyageForge.Bridge.Editor` | Bridge 包 | **改为程序集名** |
| `e328a2dfe90e5a2499a12d35988ac5f3` | `VoyageForge.ForgeCLR.Runtime` | ForgeCLR 包 | **改为程序集名** |
| `a43e01d64a6355c48a4aaea6f4b07f9d` | `VoyageForge.ForgeCLR.Editor` | ForgeCLR 包 | **改为程序集名** |
| `f51ebe6a0ceec4240a699833d6309b23` | `UniTask` | UniTask 包 | **改为程序集名** |
| `e34a5702dd353724aa315fb8011f08c3` | `YooAsset` | UPM 外部包 | **不动** |
| `4d1926c9df5b052469a1c63448b7609a` | `YooAsset.Editor` | UPM 外部包 | **不动** |
| `13ba8ce62aa80c74598530029cb2d649` | `HybridCLR.Runtime` | UPM 外部包 | **不动** |
| `2373f786d14518f44b0f475db77ba4de` | `HybridCLR.Editor` | UPM 外部包 | **不动** |

**规律**：凡是属于 ForgeCLR / Bridge / Depot / UniTask 这些"刚转为本地 UPM 包"的程序集，它们的 GUID 引用都要改为程序集名称。HybridCLR 和 YooAsset 是外部 UPM 包（非 `file:` 引用），GUID 不会变，不动。

### 具体修改示例

**ForgeCLR.Runtime 改前：**

```json
{
    "name": "VoyageForge.ForgeCLR.Runtime",
    "references": [
        "GUID:1278a46ce459c5a46b4eaeda148684ef",  // UniTask.YooAsset → 外部，不动
        "GUID:057b9764972b6ce4fa25125cdb2ad34f",  // Bridge.Runtime → 改！
        "GUID:e34a5702dd353724aa315fb8011f08c3",  // YooAsset → 外部，不动
        "GUID:f51ebe6a0ceec4240a699833d6309b23",  // UniTask → 改！
        "GUID:13ba8ce62aa80c74598530029cb2d649",  // HybridCLR → 外部，不动
        "GUID:0286d4cd81cb0c245a7c127e333f132c"   // Depot.Runtime → 改！
    ]
}
```

**ForgeCLR.Runtime 改后：**

```json
{
    "name": "VoyageForge.ForgeCLR.Runtime",
    "references": [
        "GUID:1278a46ce459c5a46b4eaeda148684ef",
        "VoyageForge.Bridge.Runtime",
        "GUID:e34a5702dd353724aa315fb8011f08c3",
        "UniTask",
        "GUID:13ba8ce62aa80c74598530029cb2d649",
        "VoyageForge.Depot.Runtime"
    ]
}
```

### 如何查出 GUID 对应的程序集名？

如果后续新增了程序集、需要查出 GUID 对应关系，用以下方法：

**方法一：查找 `.meta` 文件**

asmdef 的 GUID 就在同目录下的 `.meta` 文件中：

```bash
# Linux / macOS
grep "guid:" path/to/Something.asmdef.meta

# Windows PowerShell
Select-String "guid:" "path\to\Something.asmdef.meta"
```

**方法二：Unity Editor 中查看**

选中 `.asmdef` 文件 → Inspector 面板 → 右上角 ⋮ → "Select GUID" → 然后查找引用

### 如何判断一个 GUID 引用是否应该改？

按这个流程：

1. 找到 GUID 对应的 `.meta` 文件，确认程序集名
2. 看 `.meta` 文件是否在 **本次转为本地包的文件夹内**
3. 是 → 改为程序集名引用
4. 否（如 YooAsset、HybridCLR 等通过 UPM 远程安装的包） → **不动**

---

## 完整改动清单

本次操作涉及的所有文件变更：

### 新建文件

| 文件 | 说明 |
|------|------|
| `Assets/ForgeCLR/package.json` | ForgeCLR 包定义 |

### 修改文件

| 文件 | 改动 |
|------|------|
| `Packages/manifest.json` | 新增 4 个 `file:` 本地包引用 |
| `Scripts/VoyageForge.ForgeCLR.Runtime.asmdef` | `Bridge.Runtime`、`Depot.Runtime` 改为程序集名 |
| `Editor/VoyageForge.ForgeCLR.Editor.asmdef` | `ForgeCLR.Runtime` 改为程序集名 |
| `Plugins/Bridge/Runtime/VoyageForge.Bridge.Runtime.asmdef` | `Depot.Runtime`、`Depot.Editor` 改为程序集名 |
| `Plugins/Bridge/Editor/VoyageForge.Bridge.Editor.asmdef` | `Bridge.Runtime`、`Depot.Runtime`、`Depot.Editor` 改为程序集名 |
| `Plugins/Depot/Editor/Scripts/VoyageForge.Depot.Editor.asmdef` | `Depot.Runtime` 改为程序集名 |
| `Plugins/UniTask/Runtime/External/YooAsset/UniTask.YooAsset.asmdef` | `UniTask` 改为程序集名 |

---

## 团队协作注意事项

`Packages/manifest.json` 在**项目根目录**下，不在 ForgeCLR 的 git 仓库内。有两种处理方式：

**方案 A（推荐）：项目根目录也纳入版本控制**

```bash
cd "项目根目录"
git init
git add Packages/manifest.json
# ... 添加其他需要共享的项目配置
```

**方案 B：团队成员手动添加**

每个成员克隆项目后，手动将以下四行加入 `Packages/manifest.json`：

```json
"com.cysharp.unitask": "file:../Assets/ForgeCLR/Plugins/UniTask",
"com.voyageforge.depot": "file:../Assets/ForgeCLR/Plugins/Depot",
"com.voyageforge.bridge": "file:../Assets/ForgeCLR/Plugins/Bridge",
"com.voyageforge.forgeclr": "file:../Assets/ForgeCLR",
```

---

## 验证方式

完成上述操作后：

1. 重启 Unity Editor
2. 打开 **Window > Package Manager**，左上角下拉选择 "In Project"
3. 应能看到 ForgeCLR、Bridge、Depot、UniTask 四个包
4. 选中 Depot 包，右侧会显示 **Samples** 区域和导入按钮
5. Console 无编译错误

---

## 后续新增程序集时的注意事项

如果在 ForgeCLR / Bridge / Depot / UniTask 中新增了 `.asmdef` 文件，并且它需要引用其他"本地包"内的程序集，**不要使用 GUID 引用**，直接用程序集名称：

```json
{
    "name": "VoyageForge.NewModule.Runtime",
    "references": [
        "VoyageForge.Depot.Runtime",
        "UniTask"
    ]
}
```

这样可以避免每次转为 UPM 包时重新修改引用。
