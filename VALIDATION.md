# ForgeCLR 环境检测系统

## 架构

```
IForgeCLRValidationCheck          ← 接口：Validate() + Repair()
        ↑
   24 个检测类                     ← 按功能分入 4 个子文件夹
        ↓
ForgeCLRValidationContext          ← 上下文：Settings / CollectorSetting / StrictMode
        ↓
ForgeCLRValidationUtility          ← 入口：反射扫描 → CreateReport → ValidateForBuild
```

## 文件夹结构

```
Editor/ValidationChecks/
├── Common/                        # 接口、上下文、工具
│   ├── IForgeCLRValidationCheck   ← 检测接口
│   ├── ForgeCLRValidationContext  ← 检测上下文
│   ├── ForgeCLRValidationHelper   ← 路径、目录工具
│   └── UnityHubArgsHelper         ← Hub CLI 参数管理
├── Prerequisites/                 # 依赖环境检测
├── Settings/                      # ForgeCLR 配置检测
└── Build/                         # 构建资源检测
```

## 现有检测项

### Prerequisites（依赖环境）
| 类名 | 标题 | 可修复 | 说明 |
|------|------|--------|------|
| `PackagesManifestCheck` | Packages Manifest | | `Packages/manifest.json` 存在 |
| `HybridCLRSettingsCheck` | HybridCLR Settings | | `HybridCLRSettings.asset` 存在 |
| `YooAssetSettingsCheck` | YooAsset Settings | ✓ | `Resources/YooAssetSettings.asset` 存在 |
| `YooAssetsCollectorCheck` | YooAssets Collector | ✓ | `Assets/AssetBundleCollectorSetting.asset` 存在 |
| `YooAssetsRuntimeCheck` | YooAssets Runtime | | YooAsset 程序集已安装 |
| `UniTaskRuntimeCheck` | UniTask Runtime | | UniTask 程序集已安装 |
| `HybridCLRInstallerCheck` | HybridCLR Installer | | HybridCLR Installer 已完成 |

### Settings（ForgeCLR 配置）
| 类名 | 标题 | 可修复 | 说明 |
|------|------|--------|------|
| `RuntimeSettingsSOCheck` | 运行时配置 SO | ✓ | RuntimeSettings SO 已引用 |
| `YooAssetsPackageCheck` | YooAssets Package | ✓ | 包名在 Collector 中存在 |
| `DllCopyDirectoryNameCheck` | DLL 拷贝根目录名 | ✓ | 名称合法 |
| `HotUpdateDllCopyDirectoryCheck` | 热更新 DLL 拷贝目录 | ✓ | 路径在 Assets 下 |
| `MetadataDllCopyDirectoryCheck` | AOT 元数据 DLL 拷贝目录 | ✓ | 路径在 Assets 下 |
| `HotUpdateDllABCollectionCheck` | 热更新 DLL AB 收集 | ✓ | 目录已加入 YooAssets 包 |
| `MetadataDllABCollectionCheck` | AOT 元数据 DLL AB 收集 | ✓ | 目录已加入 YooAssets 包 |
| `StartupSceneABCollectionCheck` | 启动场景 AB 收集 | ✓ | 场景已加入 YooAssets 包 |
| `LauncherSceneCheck` | Launcher 场景 | | 场景文件存在 |
| `LauncherBuildSettingsCheck` | Launcher Build Settings | ✓ | 位于 Build Settings 第一位 |
| `FileServerRootDirectoryCheck` | 文件服务器根目录 | ✓ | 目录存在 |
| `FileServerPortCheck` | 文件服务器端口 | ✓ | 端口可用 |

### Build（构建资源）
| 类名 | 标题 | 可修复 | 说明 |
|------|------|--------|------|
| `HotUpdateDllDirectoryStatusCheck` | 热更新 DLL 拷贝目录状态 | ✓ | 目录存在 |
| `MetadataDllDirectoryStatusCheck` | AOT 元数据 DLL 拷贝目录状态 | ✓ | 目录存在 |
| `StreamingAssetsFileNameCheck` | StreamingAssets 文件名 | | 仅含 ASCII 安全字符 |
| `StreamingAssetsYooAssetFilesCheck` | StreamingAssets YooAssets 文件 | | BuildinCatalog 存在 |
| `AndroidGraphicsAPICheck` | Android 图形 API | ✓ | 含 `-force-gles` |

## 严格模式

`ForgeCLRSettings.streamingAssetsStrictMode` 控制部分检测的判定级别：

- **开启**：问题 → `Failed` → 阻断构建
- **关闭**：问题 → `Warning` → 仅提示

Project Settings 面板 → "环境检测" → "严格模式" 开关。

## 添加新检测

1. 在对应文件夹（Prerequisites / Settings / Build）新建类，实现 `IForgeCLRValidationCheck`：

```csharp
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 XXX 是否正常。
    /// </summary>
    public sealed class MyCustomCheck : IForgeCLRValidationCheck
    {
        public string Title => "我的检测";
        public bool CanRepair => false;

        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var ok = true; // 替换为实际检测逻辑
            return new ForgeCLRValidationItem(Title,
                ok ? "检测通过" : "检测失败",
                ok ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        public void Repair(ForgeCLRValidationContext context)
        {
            // 不支持修复时留空即可
        }
    }
}
```

2. 无需手动注册 — `ForgeCLRValidationUtility` 通过反射自动扫描所有程序集中实现了 `IForgeCLRValidationCheck` 的非抽象类。

3. 保存文件，域重载后新检测即出现在环境检测报告中。

### 关键接口

```csharp
public interface IForgeCLRValidationCheck
{
    string Title { get; }                                          // 检测标题，用于 UI 和修复匹配
    bool CanRepair { get; }                                        // 是否支持自动修复
    ForgeCLRValidationItem Validate(ForgeCLRValidationContext context);  // 执行检测
    void Repair(ForgeCLRValidationContext context);                // 执行修复
}
```

### 上下文

```csharp
public class ForgeCLRValidationContext
{
    public ForgeCLRSettings Settings { get; }              // ForgeCLR 项目配置
    public AssetBundleCollectorSetting CollectorSetting { get; }  // YooAssets 收集器配置（惰性加载）
    public bool HasYooAssetSettings { get; }              // YooAssetSettings 是否存在
    public bool StrictMode { get; }                       // 严格模式开关
}
```

### 检测结果

```csharp
public enum ForgeCLRValidationStatus { Passed, Failed, Warning }

public class ForgeCLRValidationItem
{
    public string Title { get; }
    public string Message { get; }
    public ForgeCLRValidationStatus Status { get; }
}
```

- `Failed` → UI 红色卡片，构建阻断
- `Warning` → UI 黄色卡片，不阻断
- `Passed` → UI 绿色卡片

### 返回 null

`Validate()` 可返回 `null` 表示当前检测不适用（如非 Android 平台时 `AndroidGraphicsAPICheck` 返回 null）。null 项会被自动过滤，不出现在报告中。
