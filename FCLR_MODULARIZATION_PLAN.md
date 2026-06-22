# ForgeCLR 模块化拆分计划书

> 目标：将 ForgeCLR 拆分为 **Core** + **YooAssets 模块** + **HCLR 模块**。
> 模块通过主 SO 的 `List<ForgeCLRModuleConfigSO>` 中是否存在对应类型的子 SO 来决定启用/禁用。
> 每个模块有自己的 `ForgeCLRModuleSettingsProvider<T>` 实现，泛型自动处理 ConfigSO 的读写。
> 通过反射发现所有模块，新增模块只需继承泛型基类，**零改动 Core 代码**。

---

## 目录

1. [核心架构](#1-核心架构)
2. [泛型基类设计](#2-泛型基类设计)
3. [主 SO 设计](#3-主-so-设计)
4. [子 SO 设计](#4-子-so-设计)
5. [模块实现示例](#5-模块实现示例)
6. [反射发现机制](#6-反射发现机制)
7. [主 Provider 中的模块渲染](#7-主-provider-中的模块渲染)
8. [校验报告聚合](#8-校验报告聚合)
9. [构建管线调度](#9-构建管线调度)
10. [实施计划与文件清单](#10-实施计划与文件清单)

---

## 1. 核心架构

```
ForgeCLRSettings (主 SO)
├── launcherSceneLocation, fileServer*, streamingAssets*   ← Core 自有字段
├── moduleConfigs: List<ForgeCLRModuleConfigSO>             ← 统一存储所有子 SO
│   ├── YooAssetsConfigSO  (存在 = YooAssets 启用)
│   └── HCLRConfigSO       (存在 = HCLR 启用)
└── GetModuleConfig<T>() / SetModuleConfig<T>()             ← 泛型读写

ForgeCLRModuleSettingsProvider<T> (泛型抽象基类)
├── ConfigType = typeof(T)                                  ← 泛型自动
├── IsEnabled() → GetModuleConfig<T>() != null              ← 泛型自动
├── Enable()  → SetModuleConfig<T>(new T())                 ← 泛型自动
└── Disable() → SetModuleConfig<T>(null)                    ← 泛型自动

子类只需声明显式泛型参数，覆写 DisplayName / ModuleId / CreateModuleUI 等业务方法

ForgeCLRSettingsProvider (主面板)
├── Core UI (FileServer, Launcher)
├── ModulesContainer
│   ├── Box ── YooAssetsModuleProvider : ForgeCLRModuleSettingsProvider<YooAssetsConfigSO>
│   └── Box ── HCLRModuleProvider     : ForgeCLRModuleSettingsProvider<HCLRConfigSO>
├── 校验报告 ← 聚合所有 IsEnabled 的模块
└── 操作按钮 → 调度到各模块
```

---

## 2. 泛型基类设计

```csharp
// Shared/Editor/ForgeCLRModuleSettingsProvider.cs

/// <summary>
/// 所有 FCLR 子模块的 SettingsProvider 抽象基类。
/// 泛型参数 T 指定模块对应的 ConfigSO 类型。
/// GetConfig / SetConfig / IsEnabled / Enable / Disable 全部由泛型自动实现。
/// 子类只需覆写 DisplayName、ModuleId 和业务方法。
/// </summary>
public abstract class ForgeCLRModuleSettingsProvider
{
    // ===== 子类必须覆写 =====

    /// <summary>模块唯一标识</summary>
    public abstract string ModuleId { get; }

    /// <summary>模块显示名称，用于 Box 标题</summary>
    public abstract string DisplayName { get; }

    /// <summary>模块所依赖的 ModuleId 列表</summary>
    public virtual string[] Dependencies => Array.Empty<string>();

    /// <summary>
    /// 返回模块自己的 UI（放在 Box 中）。
    /// 仅在 IsEnabled == true 时被调用。
    /// </summary>
    public abstract VisualElement CreateModuleUI(
        ForgeCLRSettings settings,
        SerializedObject settingsSO);

    /// <summary>返回模块自己的校验检查集合</summary>
    public abstract IForgeCLRValidationCheck[] CreateValidationChecks(
        ForgeCLRSettings settings);

    /// <summary>执行模块的快速设置</summary>
    public abstract void ExecuteQuickSetup(ForgeCLRSettings settings);

    // ===== 泛型自动实现 =====

    /// <summary>模块对应的 ConfigSO 类型</summary>
    public abstract Type ConfigType { get; }

    /// <summary>从主 SO 中读取该模块的 ConfigSO</summary>
    public abstract ForgeCLRModuleConfigSO GetConfig(ForgeCLRSettings settings);

    /// <summary>向主 SO 写入该模块的 ConfigSO（null = 禁用）</summary>
    public abstract void SetConfig(ForgeCLRSettings settings, ForgeCLRModuleConfigSO config);

    /// <summary>是否启用：ConfigSO 引用是否存在</summary>
    public bool IsEnabled(ForgeCLRSettings settings)
        => GetConfig(settings) != null;

    /// <summary>启用模块：创建子 SO 并写入主 SO</summary>
    public void Enable(ForgeCLRSettings settings)
    {
        var config = (ForgeCLRModuleConfigSO)ScriptableObject.CreateInstance(ConfigType);
        SetConfig(settings, config);
        settings.SaveSettings();
    }

    /// <summary>禁用模块：从主 SO 移除子 SO 引用</summary>
    public void Disable(ForgeCLRSettings settings)
    {
        SetConfig(settings, null);
        settings.SaveSettings();
    }

    // ===== 构建回调（可选覆写） =====

    public virtual void OnPreBuildResource(BuildTarget target) { }
    public virtual void OnPostBuildResource(BuildTarget target) { }
    public virtual void OnPreBuildPlayer(BuildTarget target) { }
}

/// <summary>
/// 泛型版本：ConfigType / GetConfig / SetConfig 由泛型参数自动推导，
/// 子类继承此类后无需再实现这三个成员。
/// </summary>
public abstract class ForgeCLRModuleSettingsProvider<T> : ForgeCLRModuleSettingsProvider
    where T : ForgeCLRModuleConfigSO
{
    public override Type ConfigType => typeof(T);

    public override ForgeCLRModuleConfigSO GetConfig(ForgeCLRSettings settings)
        => settings.GetModuleConfig<T>();

    public override void SetConfig(ForgeCLRSettings settings, ForgeCLRModuleConfigSO config)
        => settings.SetModuleConfig(config as T);
}
```

---

## 3. 主 SO 设计

```csharp
// Editor/ForgeCLRSettings.cs

[FilePath("ProjectSettings/ForgeCLRSettings.asset",
          FilePathAttribute.Location.ProjectFolder)]
public sealed class ForgeCLRSettings : ScriptableSingleton<ForgeCLRSettings>
{
    public const string DefaultLauncherScenePath =
        "Assets/ForgeCLR/Scenes/Launcher.unity";

    // ===== Core 自有字段 =====

    [SerializeField] private string launcherSceneLocation = DefaultLauncherScenePath;
    [SerializeField] private string fileServerRootDirectory = "";
    [SerializeField] private int fileServerPort = 8899;
    [SerializeField] private string fileServerBindIPAddress = "";
    [SerializeField] private bool fileServerAutoRestart;
    [SerializeField] private bool streamingAssetsStrictMode;

    // ===== 统一模块存储（取代逐个字段声明 yooAssetsConfig / hclrConfig） =====

    [SerializeField]
    private List<ForgeCLRModuleConfigSO> moduleConfigs = new List<ForgeCLRModuleConfigSO>();

    // ===== 泛型读写 =====

    /// <summary>
    /// 获取指定类型的模块配置 SO。不存在时返回 null。
    /// </summary>
    public T GetModuleConfig<T>() where T : ForgeCLRModuleConfigSO
        => moduleConfigs.OfType<T>().FirstOrDefault();

    /// <summary>
    /// 设置指定类型的模块配置 SO。传入 null 表示移除（禁用模块）。
    /// </summary>
    public void SetModuleConfig<T>(T config) where T : ForgeCLRModuleConfigSO
    {
        moduleConfigs.RemoveAll(c => c is T);
        if (config != null)
            moduleConfigs.Add(config);
        SaveSettings();
    }

    // ===== Core 自有 getter/setter =====

    public string LauncherSceneLocation => NormalizeAssetPath(
        launcherSceneLocation, DefaultLauncherScenePath);

    public string FileServerRootDirectory =>
        string.IsNullOrWhiteSpace(fileServerRootDirectory)
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Bundles"))
            : fileServerRootDirectory.Trim();

    public int FileServerPort => Mathf.Clamp(fileServerPort, 1, 65535);
    public string FileServerBindIPAddress => fileServerBindIPAddress?.Trim() ?? "";
    public bool FileServerAutoRestart => fileServerAutoRestart;
    public bool StreamingAssetsStrictMode => streamingAssetsStrictMode;

    public void SetLauncherSceneLocation(string path) { ... SaveSettings(); }
    public void SetFileServerConfig(string root, int port, string bindIp) { ... SaveSettings(); }
    public void SetFileServerAutoRestart(bool enabled) { ... SaveSettings(); }
    public void SetStreamingAssetsStrictMode(bool enabled) { ... SaveSettings(); }

    // ===== 迁移兼容：移除的字段 =====
    // dllCopyDirectoryName     → 移至 HCLRConfigSO
    // runtimeSettings 引用      → 移至 YooAssetsConfigSO
    // 用户升级后首次打开面板时，从旧字段迁移到新子 SO（一次性逻辑）

    public void SaveSettings()
    {
        fileServerPort = Mathf.Clamp(fileServerPort, 1, 65535);
        fileServerBindIPAddress = fileServerBindIPAddress?.Trim() ?? "";
        Save(true);
    }
}
```

---

## 4. 子 SO 设计

### 4.1 基类

```csharp
// Shared/Editor/ForgeCLRModuleConfigSO.cs

public abstract class ForgeCLRModuleConfigSO : ScriptableObject
{
    /// <summary>模块唯一标识</summary>
    public abstract string ModuleId { get; }

    /// <summary>配置版本号，用于升级迁移</summary>
    [SerializeField, HideInInspector]
    protected int configVersion = 1;
}
```

### 4.2 YooAssetsConfigSO

```csharp
// Modules/YooAssets/Editor/YooAssetsConfigSO.cs

public class YooAssetsConfigSO : ForgeCLRModuleConfigSO
{
    public override string ModuleId => "com.voyageforge.forgeclr.yooassets";

    [SerializeField]
    private string defaultPackageName = "DefaultPackage";

    [SerializeField]
    private ForgeCLRRuntimeSettings runtimeSettings;

    public string DefaultPackageName => defaultPackageName;
    public ForgeCLRRuntimeSettings RuntimeSettings => runtimeSettings;

    public void SetDefaultPackageName(string name) { ... }
    public void SetRuntimeSettings(ForgeCLRRuntimeSettings rs) { ... }
}
```

### 4.3 HCLRConfigSO

```csharp
// Modules/HCLR/Editor/HCLRConfigSO.cs

public class HCLRConfigSO : ForgeCLRModuleConfigSO
{
    public override string ModuleId => "com.voyageforge.forgeclr.hclr";

    [SerializeField]
    private string dllCopyDirectoryName = "HotUpdateDll";

    public string DllCopyDirectoryName => dllCopyDirectoryName;

    public string HotUpdateDllCopyDirectory
        => $"Assets/{dllCopyDirectoryName}/HotUpdateDll";

    public string MetadataDllCopyDirectory
        => $"Assets/{dllCopyDirectoryName}/MetadataDll";

    public void SetDllCopyDirectoryName(string name) { ... }
}
```

---

## 5. 模块实现示例

### 5.1 YooAssetsModuleProvider

```csharp
// Modules/YooAssets/Editor/YooAssetsModuleProvider.cs

public class YooAssetsModuleProvider
    : ForgeCLRModuleSettingsProvider<YooAssetsConfigSO>
{
    // ConfigType / GetConfig / SetConfig / IsEnabled / Enable / Disable → 泛型基类自动

    public override string ModuleId      => "com.voyageforge.forgeclr.yooassets";
    public override string DisplayName   => "YooAssets 资源管理";
    public override string[] Dependencies => Array.Empty<string>();

    public override VisualElement CreateModuleUI(
        ForgeCLRSettings settings, SerializedObject settingsSO)
    {
        // Package 下拉框、PlayMode 下拉框等
        var config = settings.GetModuleConfig<YooAssetsConfigSO>();
        // ...
    }

    public override IForgeCLRValidationCheck[] CreateValidationChecks(
        ForgeCLRSettings settings)
    {
        return new IForgeCLRValidationCheck[]
        {
            new YooAssetsRuntimeCheck(),
            new YooAssetSettingsCheck(),
            new YooAssetsCollectorCheck(),
            new YooAssetsPackageCheck(),
        };
    }

    public override void ExecuteQuickSetup(ForgeCLRSettings settings)
    {
        ForgeCLRRuntimeSettingsEditorUtility.EnsureYooAssetSettings();
        ForgeCLRQuickSetup.EnsureYooAssetCollectorConfiguration();
        // ... 不涉及 HCLR 的步骤
    }
}
```

### 5.2 HCLRModuleProvider

```csharp
// Modules/HCLR/Editor/HCLRModuleProvider.cs

public class HCLRModuleProvider
    : ForgeCLRModuleSettingsProvider<HCLRConfigSO>
{
    // ConfigType / GetConfig / SetConfig / IsEnabled / Enable / Disable → 泛型基类自动

    public override string ModuleId      => "com.voyageforge.forgeclr.hclr";
    public override string DisplayName   => "HCLR 代码热更";
    public override string[] Dependencies => new[] { "com.voyageforge.forgeclr.yooassets" };

    public override void Enable(ForgeCLRSettings settings)
    {
        // 依赖检查
        if (settings.GetModuleConfig<YooAssetsConfigSO>() == null)
            throw new InvalidOperationException(
                "HCLR 依赖 YooAssets，请先启用 YooAssets 模块。");

        base.Enable(settings);   // 调用泛型基类的创建 + 写入逻辑
        // 创建 DLL 目录等
    }

    public override VisualElement CreateModuleUI(
        ForgeCLRSettings settings, SerializedObject settingsSO)
    {
        // DLL 拷贝目录配置、AOT 元数据路径配置等
        var config = settings.GetModuleConfig<HCLRConfigSO>();
        // ...
    }

    public override IForgeCLRValidationCheck[] CreateValidationChecks(
        ForgeCLRSettings settings)
    {
        return new IForgeCLRValidationCheck[]
        {
            new HybridCLRSettingsCheck(),
            new HybridCLRInstallerCheck(),
            new HotUpdateDllABCollectionCheck(),
            new MetadataDllABCollectionCheck(),
            new HotUpdateDllDirectoryStatusCheck(),
            new MetadataDllDirectoryStatusCheck(),
            new AndroidGraphicsAPICheck(),
        };
    }

    public override void ExecuteQuickSetup(ForgeCLRSettings settings)
    {
        var config = settings.GetModuleConfig<HCLRConfigSO>();
        // 创建 DLL copy 目录
        // 注册到 YooAssets Collector
    }

    public override void OnPreBuildResource(BuildTarget target)
    {
        CompileDllCommand.CompileDll(target);
        CopyHotUpdateDllToFolder.CopyAssemblies(target, false);
    }

    public override void OnPreBuildPlayer(BuildTarget target)
    {
        PrebuildCommand.GenerateAll();
    }
}
```

---

## 6. 反射发现机制

```csharp
// Shared/Editor/ForgeCLRModuleDiscovery.cs

public static class ForgeCLRModuleDiscovery
{
    private static List<ForgeCLRModuleSettingsProvider> _cached;

    /// <summary>
    /// 反射扫描所有非抽象 ForgeCLRModuleSettingsProvider 子类，返回实例列表。
    /// </summary>
    public static List<ForgeCLRModuleSettingsProvider> DiscoverAll()
    {
        if (_cached != null)
            return _cached;

        _cached = new List<ForgeCLRModuleSettingsProvider>();
        var baseType = typeof(ForgeCLRModuleSettingsProvider);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsAbstract || !baseType.IsAssignableFrom(type))
                        continue;
                    if (Activator.CreateInstance(type)
                        is ForgeCLRModuleSettingsProvider instance)
                        _cached.Add(instance);
                }
            }
            catch (ReflectionTypeLoadException) { }
        }

        return _cached;
    }

    /// <summary>
    /// 获取已启用的模块列表。
    /// </summary>
    public static List<ForgeCLRModuleSettingsProvider> GetEnabled(
        ForgeCLRSettings settings)
        => DiscoverAll()
            .Where(m => m.IsEnabled(settings))
            .ToList();
}
```

**关键：反射发现只知道 `ForgeCLRModuleSettingsProvider`，不知道任何具体子类名。泛型参数 `<T>` 在实例化时已融入 Provider 实例中，Core 代码完全不引用 `YooAssetsModuleProvider` 或 `HCLRModuleProvider`。**

---

## 7. 主 Provider 中的模块渲染

```csharp
// ForgeCLRSettingsProvider.BuildUi()

private void BuildUi(VisualElement rootElement)
{
    // ... UXML 加载 + Core 自有 UI ...

    var modulesContainer = rootElement.Q<VisualElement>("ModulesContainer");
    modulesContainer?.Clear();

    foreach (var module in ForgeCLRModuleDiscovery.DiscoverAll())
    {
        RenderModuleBox(modulesContainer, module);
    }

    RenderValidationReport(rootElement, CreateValidationReport());
}

private void RenderModuleBox(
    VisualElement container, ForgeCLRModuleSettingsProvider module)
{
    var box = new Box();
    box.AddToClassList("fclr-module-box");

    var header = new VisualElement();
    header.AddToClassList("fclr-module-box-header");

    var label = new Label($"{module.DisplayName} ({(module.IsEnabled(settings) ? "已启用" : "未启用")})");

    var toggle = new Toggle { value = module.IsEnabled(settings) };
    toggle.RegisterValueChangedCallback(evt =>
    {
        if (evt.newValue)
        {
            // 依赖检查
            foreach (var depId in module.Dependencies)
            {
                var dep = ForgeCLRModuleDiscovery.DiscoverAll()
                    .First(m => m.ModuleId == depId);
                if (!dep.IsEnabled(settings))
                    throw new InvalidOperationException(
                        $"[ForgeCLR] 模块 '{module.DisplayName}' 依赖 '{dep.DisplayName}'，" +
                        $"请先启用 '{dep.DisplayName}'。");
            }
            module.Enable(settings);    // 泛型自动：创建 ConfigSO + 写入 List
        }
        else
        {
            module.Disable(settings);   // 泛型自动：从 List 移除
        }
        BuildUi(rootElement);           // 重建
    });

    header.Add(label);
    header.Add(toggle);
    box.Add(header);

    if (module.IsEnabled(settings))
    {
        // 子模块 UI 由模块自己创建，主 Provider 只负责放到 Box 里
        box.Add(module.CreateModuleUI(settings, settingsObject));
    }

    container.Add(box);
}
```

---

## 8. 校验报告聚合

```csharp
// ForgeCLRSettingsProvider

private ForgeCLRValidationReport CreateValidationReport()
{
    var items = new List<ForgeCLRValidationItem>();

    // Core 自有检查
    items.AddRange(RunCoreValidationChecks());

    // 各已启用模块的检查
    foreach (var module in ForgeCLRModuleDiscovery.GetEnabled(settings))
    {
        var checks = module.CreateValidationChecks(settings);
        var context = new ForgeCLRValidationContext(settings);
        items.AddRange(checks.Select(c => c.Validate(context)));
    }

    // 未启用模块的依赖检查也加入报告
    foreach (var module in ForgeCLRModuleDiscovery.DiscoverAll()
                 .Where(m => !m.IsEnabled(settings)))
    {
        foreach (var depId in module.Dependencies)
        {
            var dep = ForgeCLRModuleDiscovery.DiscoverAll()
                .First(m => m.ModuleId == depId);
            if (dep.IsEnabled(settings))
                continue;
            items.Add(new ForgeCLRValidationItem(
                $"{module.DisplayName} 依赖缺失",
                $"未启用模块 '{module.DisplayName}' 依赖 '{dep.DisplayName}'，但 '{dep.DisplayName}' 也未启用。",
                ForgeCLRValidationStatus.Warning));
        }
    }

    return new ForgeCLRValidationReport(items);
}
```

---

## 9. 构建管线调度

```csharp
// ForgeCLRBuildPipeline.cs

public static class ForgeCLRBuildPipeline
{
    public static void BuildResourcePackage()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        var modules = ForgeCLRModuleDiscovery.GetEnabled(
            ForgeCLRSettings.instance);

        // PreBuild：HCLR 编译 DLL 等
        foreach (var m in modules) m.OnPreBuildResource(target);

        // 资源构建（目前是 YooAssets 模块负责，后续可抽象到接口）
        ForgeCLRQuickSetup.EnsureYooAssetCollectorConfiguration();
        var results = BuildYooAssetPackages(target);

        // PostBuild
        foreach (var m in modules) m.OnPostBuildResource(target);
    }

    public static void BuildPlayerPackage()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        var modules = ForgeCLRModuleDiscovery.GetEnabled(
            ForgeCLRSettings.instance);

        foreach (var m in modules) m.OnPreBuildPlayer(target);

        // Unity BuildPlayer...
    }
}
```

---

## 10. 实施计划与文件清单

### Phase 1 —— 基础设施 + 两模块拆分

| 操作 | 路径 | 说明 |
|------|------|------|
| **新增** | `Shared/Editor/ForgeCLRModuleConfigSO.cs` | 子 SO 基类 |
| **新增** | `Shared/Editor/ForgeCLRModuleSettingsProvider.cs` | 模块 Provider 基类 + 泛型版 |
| **新增** | `Shared/Editor/ForgeCLRModuleDiscovery.cs` | 反射发现 |
| **新增** | `Modules/YooAssets/Editor/YooAssetsConfigSO.cs` | YooAssets 子 SO |
| **新增** | `Modules/YooAssets/Editor/YooAssetsModuleProvider.cs` | YooAssets 模块 |
| **新增** | `Modules/HCLR/Editor/HCLRConfigSO.cs` | HCLR 子 SO |
| **新增** | `Modules/HCLR/Editor/HCLRModuleProvider.cs` | HCLR 模块 |
| **修改** | `Editor/ForgeCLRSettings.cs` | 移除旧字段，新增 `List<ForgeCLRModuleConfigSO>` + 泛型读写 |
| **修改** | `Editor/ForgeCLRSettingsProvider.cs` | 改为容器模式，反射渲染模块 Box |
| **修改** | `Editor/ForgeCLRBuildPipeline.cs` | 改为遍历已启用模块调度 |
| **修改** | `Editor/ForgeCLRQuickSetup.cs` | 改为遍历已启用模块调度 |
| **修改** | `Editor/UITK/ForgeCLRSettings.uxml` | 添加 `ModulesContainer` |
| **移动** | 校验检查 → 按模块归属到 `Modules/*/Editor/ValidationChecks/` | - |

### Phase 2 —— 运行时模块化（后续）

- 拆分 `ForgeCLRRuntimeSettings` 为 YooAssets 部分 + HCLR 部分
- PatchOperation 支持模块注入 FSM 节点
- 运行时初始化按模块调度

---

## 附录：新增模块只需三步

以未来新增 "Addressable 模块" 为例：

```csharp
// 1. 创建 ConfigSO
public class AddressableConfigSO : ForgeCLRModuleConfigSO
{
    public override string ModuleId => "com.voyageforge.forgeclr.addressable";
    [SerializeField] private string settingsPath;
}

// 2. 创建 Provider（泛型自动处理 ConfigType/GetConfig/SetConfig/IsEnabled）
public class AddressableModuleProvider
    : ForgeCLRModuleSettingsProvider<AddressableConfigSO>
{
    public override string ModuleId => "com.voyageforge.forgeclr.addressable";
    public override string DisplayName => "Addressable 资源管理";

    public override VisualElement CreateModuleUI(...) { ... }
    public override IForgeCLRValidationCheck[] CreateValidationChecks(...) { ... }
    public override void ExecuteQuickSetup(...) { ... }
}

// 3. 完成。无需修改 Core 任何代码。
```
