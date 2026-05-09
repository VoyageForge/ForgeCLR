# ForgeCLR 模板说明

ForgeCLR 模板把 HybridCLR、YooAssets、UniTask 串成一条可复用的热更新工程流程。

## 运行时流程

1. 场景中的 `VoyageForge.ForgeCLR.Runtime.Launcher` 启动。
2. `Launcher` 从 `ForgeCLRRuntimeSettings` 读取运行时配置；如果没有显式引用，会自动加载 `Resources/ForgeCLRRuntimeSettings`。
3. `PatchOperation` 初始化 YooAssets 包，更新版本和清单，下载远端资源并清理无用缓存。
4. `HotUpdateBootstrap` 从 YooAssets 原生文件中加载 AOT 补充元数据 DLL。
5. `HotUpdateBootstrap` 从 YooAssets 原生文件中加载热更新程序集 DLL。
6. 反射调用热更新入口，默认入口是 `HotUpdate.HotUpdateEntry.Start()`。

DLL 文件计划打进 AB 包中，所以热更新 DLL 和 AOT 元数据 DLL 必须先拷贝到 `Assets` 下，再由 YooAssets Collector 收集。

## Project Settings

打开：

`Edit/Project Settings/VoyageForge/ForgeCLR`

ForgeCLR 只配置自己负责的内容：

- `运行时配置 SO`：一键构建资源包时要自动填充的 `ForgeCLRRuntimeSettings`。
- `热更新 DLL 拷贝目录`：热更新程序集 `.dll.bytes` 输出目录，默认 `Assets/HotUpdateDll/HotUpdateDll`。
- `AOT 元数据 DLL 拷贝目录`：AOT 补充元数据 `.dll.bytes` 输出目录，默认 `Assets/HotUpdateDll/MetadataDll`。

YooAssets 的 Package、Collector、Builder、压缩方式、输出根目录、内置资源拷贝等配置仍然在 YooAssets 自己的窗口中维护：

- `YooAsset/AssetBundle Collector`
- `YooAsset/AssetBundle Builder`

## 菜单流程

`VoyageForge/ForgeCLR/快速设置`

创建默认目录、保存 Project Settings 配置，并在未配置时创建 `Assets/ForgeCLR/Resources/ForgeCLRRuntimeSettings.asset` 后写入 Project Settings 的 `运行时配置 SO` 引用。

`ForgeCLRRuntimeSettings` 由 `Launcher` 引用，保存运行时启动需要的配置：

- YooAssets 资源包名称
- YooAssets PlayMode
- 是否加载 AOT 补充元数据
- AOT 元数据 DLL 地址列表
- 热更新 DLL 地址列表
- 热更新入口类型和方法

## HybridCLR 程序集边界

HybridCLR 的“程序集集合”分成两类理解：

- 热更新程序集：在 `HybridCLRSettings` 中配置后，构建流程会按这些程序集编译热更新 DLL，并从主包裁剪/排除对应程序集。出包后，主包内不会再自动包含新的热更新程序集；如果想新增一个全新的热更程序集，通常需要先把它加入 HybridCLR 配置并重新出主包。已有热更程序集的 DLL 内容可以通过资源包更新。
- AOT 补充元数据程序集：AOT 程序集本体是主包 IL2CPP 构建时固定的，例如 `mscorlib`、`System`、项目主程序集等。运行时加载的 `.dll.bytes` 是这些 AOT 程序集的补充元数据，列表应来自当前目标平台构建后生成的 `AOTGenericReferences.PatchedAOTAssemblyList`。是否加载哪些补充元数据可以写在 `ForgeCLRRuntimeSettings` 中，但这些元数据必须和当前主包的 AOT 程序集版本匹配。

`VoyageForge/ForgeCLR/验证环境`

检测 HybridCLR、YooAssets、UniTask、HybridCLR Installer 和 YooAssets Collector 配置。

`VoyageForge/ForgeCLR/拷贝热更新 DLL`

把 HybridCLR 编译出的热更新 DLL 和 AOT 元数据 DLL 拷贝为 `.dll.bytes`。

`VoyageForge/ForgeCLR/构建资源包`

完整资源包流程：

1. 调用 `HybridCLR/Generate/All` 对应 API。
2. 编译当前平台热更新 DLL。
3. 拷贝热更新 DLL 和 AOT 元数据 DLL 到 Project Settings 配置目录。
4. 自动填充 Project Settings 引用的 `ForgeCLRRuntimeSettings` 中的 PackageName、热更新 DLL 列表和 AOT 元数据 DLL 列表。
5. 读取 YooAssets Collector 中配置的 Package。
6. 读取 YooAssets Builder 中每个 Package 的构建管线和构建参数。
7. 调用 YooAssets 构建 AB。

`VoyageForge/ForgeCLR/打开 Unity Build 面板`

打开 Unity Build Settings 面板。软件包平台、场景、输出路径、Development Build 等配置全部由 Unity 自己管理。

## 使用建议

1. 先配置 HybridCLR 热更新程序集。
2. 在 YooAssets Collector 中收集 `Assets/HotUpdateDll/HotUpdateDll` 和 `Assets/HotUpdateDll/MetadataDll`。
3. 运行 `VoyageForge/ForgeCLR/构建资源包`。
4. 资源包验证通过后，再打开 Unity Build Settings 面板构建软件包。
