# ForgeCLR 模板说明

ForgeCLR 模板把 HybridCLR、YooAssets、UniTask 串成一条可复用的热更新工程流程。

## 运行时流程

1. 场景中的 `VoyageForge.ForgeCLR.Runtime.Launcher` 启动。
2. `Launcher` 从 `ForgeCLRRuntimeSettings` 读取运行时配置；如果没有显式引用，会自动加载 `Resources/ForgeCLRRuntimeSettings`。
3. `PatchOperation` 初始化 YooAssets 包，更新版本和清单，下载远端资源并清理无用缓存。
4. `HotUpdateBootstrap` 从 YooAssets 原生文件中加载 AOT 补充元数据 DLL。
5. `HotUpdateBootstrap` 从 YooAssets 原生文件中加载热更新程序集 DLL。
6. `Launcher` 调用 `ForgeCLRSceneLoader.LoadStartupSceneAsync()`，按 `ForgeCLRRuntimeSettings` 加载第一个业务场景。

DLL 文件计划打进 AB 包中，所以热更新 DLL 和 AOT 元数据 DLL 必须先拷贝到 `Assets` 下，再由 YooAssets Collector 收集。运行时配置写入的是完整资源路径，例如 `Assets/HotUpdateDll/HotUpdateDll/HotUpdateAssembly.dll.bytes`，不依赖 YooAssets 是否开启 Addressable，也不依赖 Support Extensionless。

## Project Settings

打开：

`Edit/Project Settings/VoyageForge/ForgeCLR`

ForgeCLR 只配置自己负责的内容：

- `运行时配置 SO`：一键构建资源包时要自动填充的 `ForgeCLRRuntimeSettings`。
- `YooAssets 包`：包名来自项目中的 YooAssets Collector 配置文件，通过下拉框选择。
- `DLL 拷贝根目录名`：只允许配置 `Assets` 下的中间目录名，默认 `HotUpdateDll`。最终路径固定为 `Assets/{目录名}/HotUpdateDll` 和 `Assets/{目录名}/MetadataDll`。
- `启动后加载首场景`：热更新程序集加载完成后是否自动加载第一个业务场景。
- `启动场景地址`：通过下拉框选择项目中的 `.unity` 场景资源，并保存完整资源路径。

`运行时配置 SO` 引用由 ForgeCLR 快速设置自动创建和维护，在面板中是只读字段，避免手动替换导致一键构建填错配置。

YooAssets 需要两个配置文件，ForgeCLR 会在快速设置和环境检测中检查它们：

- `Assets/Resources/YooAssetSettings.asset`
- `Assets/Resources/AssetBundleCollectorSetting.asset`

`AssetBundleCollectorSetting` 必须放在 `Resources` 下，避免 YooAssets 编辑器和运行时加载到不同配置。YooAssets 的 Package、Collector、Builder、压缩方式、输出根目录、内置资源拷贝等配置仍然在 YooAssets 自己的窗口中维护：

- `YooAsset/AssetBundle Collector`
- `YooAsset/AssetBundle Builder`

## 菜单流程

`VoyageForge/ForgeCLR/快速设置`

创建默认目录、保存 Project Settings 配置，并在未配置时创建 `Assets/Resources/VoyageForge/Config/ForgeCLRRuntimeSettings.asset` 后写入 Project Settings 的 `运行时配置 SO` 引用。

快速设置还会创建默认 `Assets/Scenes/Main.unity`，并补齐 YooAssets Collector 中的 ForgeCLR 分组：

- 热更新 DLL 目录：`Assets/{目录名}/HotUpdateDll`
- AOT 元数据 DLL 目录：`Assets/{目录名}/MetadataDll`
- 首场景：`Assets/Scenes/Main.unity`

如果项目中没有 YooAssetSettings 或 YooAssets Collector 配置，快速设置会创建默认配置到 `Assets/Resources`。如果 Collector 配置中已经有 Package，ForgeCLR 会优先使用配置中的包名；只有配置中没有 Package 时才创建 `DefaultPackage`。

`ForgeCLRRuntimeSettings` 由 `Launcher` 引用，保存运行时启动需要的配置：

- YooAssets 资源包名称
- YooAssets PlayMode
- 是否加载 AOT 补充元数据
- AOT 元数据 DLL 完整资源路径列表
- 热更新 DLL 完整资源路径列表
- 是否启动后加载首场景
- 首场景完整资源路径

## 首场景加载

模板默认不在 `FsmStartGame` 里写业务场景跳转，而是在热更新程序集加载完成后由 `Launcher` 统一进入首个业务场景：

1. `Launcher` 负责 YooAssets 补丁流程、HybridCLR 热更新程序集加载和首场景加载。
2. `HotUpdateBootstrap` 只加载 AOT 补充元数据和热更新程序集，不再反射调用固定热更新入口。
3. `ForgeCLRSceneLoader` 优先使用 `Launcher` 初始化并缓存的 YooAssets Package 按完整资源路径加载 `启动场景地址`。
4. 如果当前没有可用的 YooAssets Package，会使用 `SceneManager.LoadSceneAsync` 回退加载，便于早期模板调试。

建议让启动场景本身只保留 `Launcher` 和必要的加载 UI，业务对象放在首个业务场景中。首个业务场景应由 YooAssets Collector 收集，`ForgeCLRRuntimeSettings` 中保存的是该场景的完整资源路径。

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
4. 自动填充 Project Settings 引用的 `ForgeCLRRuntimeSettings` 中的 PackageName、热更新 DLL 完整路径列表、AOT 元数据 DLL 完整路径列表和首场景路径。
5. 读取 YooAssets Collector 中配置的 Package。
6. 读取 YooAssets Builder 中每个 Package 的构建管线和构建参数。
7. 调用 YooAssets 构建 AB。

`VoyageForge/ForgeCLR/打开 Unity Build 面板`

打开 Unity Build Settings 面板。软件包平台、场景、输出路径、Development Build 等配置全部由 Unity 自己管理。

## 使用建议

1. 先配置 HybridCLR 热更新程序集。
2. 运行 `VoyageForge/ForgeCLR/快速设置`，自动创建默认场景并补齐 YooAssets Collector。
3. 运行 `VoyageForge/ForgeCLR/构建资源包`。
4. 资源包验证通过后，再打开 Unity Build Settings 面板构建软件包。

## 待改进
1. 构建软件包前增加更完整的运行前检查，例如首场景是否已经被 YooAssets 收集。
2. 后续可以把 YooAssets Builder 的默认构建参数也纳入快速设置建议，但仍保持最终配置由 YooAssets 自己维护。
3. 已经 将 FCLR 的配置放入Resources/VoyageForge/Config/ForgeCLRRuntimeSettings.asset，已经调整了Launcher 相关代码。
4. 打包前需要检测 FCLR 的配置是否存在，且正确配置。
5. 目前保存的 场景名称还是名称，需改为保存完整资源路径，同时添加场景自定义配置。
6. 如果是 android 平台 yooassets 需要 检查 配置启动项 -force-gles
7. android 使用LoadRawFileAsync ，会从 android 内置包去加载资源，导致加载失败
8. 构建资源包 不应该调用 PrebuildCommand.GenerateAll()，这回导致空包内程序集数据和现有数据不一致，应在构建软件包时调用
9. 添加局域网文件服务器，用于在不同设备上测试资源包加载
10. 已添加建议文件服务器 VoyageForgeFileServer，需要将配置保存到 projectsetting待完成，需要配合防火墙中关于UnityEditor的进出策略，unity入站tcp端口全公开