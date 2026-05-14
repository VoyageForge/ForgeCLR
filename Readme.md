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
- `Launcher 场景`：软件包启动时的第一场景，构建软件包前会自动放到 Unity Build Settings 第一位。
- `DLL 拷贝根目录名`：只允许配置 `Assets` 下的中间目录名，默认 `HotUpdateDll`。最终路径固定为 `Assets/{目录名}/HotUpdateDll` 和 `Assets/{目录名}/MetadataDll`。
- `启动后加载首场景`：热更新程序集加载完成后是否自动加载第一个业务场景。
- `启动场景地址`：通过下拉框选择项目中的 `.unity` 场景资源，并保存完整资源路径。
- `局域网文件服务器`：保存资源包测试服务器的根目录、端口、绑定 IP 和域重载自动恢复开关，配置写入 `ProjectSettings/ForgeCLRSettings.asset`。

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
- Launcher 场景：`Assets/ForgeCLR/Scenes/Launcher.unity` 会被放到 Build Settings 第一位。
- 文件服务器根目录：默认使用项目根目录下的 `Bundles`，用于局域网设备访问 YooAssets 输出资源。

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

1. 编译当前平台热更新 DLL。
2. 拷贝热更新 DLL 和 AOT 元数据 DLL 到 Project Settings 配置目录。
3. 自动填充 Project Settings 引用的 `ForgeCLRRuntimeSettings` 中的 PackageName、热更新 DLL 完整路径列表、AOT 元数据 DLL 完整路径列表和首场景路径。
4. 检查并补齐 YooAssets Collector 中的 ForgeCLR 分组。
5. 读取 YooAssets Builder 中每个 Package 的构建管线和构建参数。
6. 调用 YooAssets 构建 AB。

资源包构建不会调用 `HybridCLR/Generate/All`，避免软件包裁剪数据和当前资源包 DLL 内容错位。

`VoyageForge/ForgeCLR/构建软件包`

完整软件包流程：

1. 执行 ForgeCLR 环境检测。
2. 将 Project Settings 中配置的 Launcher 场景放到 Unity Build Settings 第一位。
3. 调用 `HybridCLR/Generate/All`，生成主包构建所需的 HybridCLR 数据。
4. 复用 Unity Build Settings 当前的平台、输出路径和 Development Build 等配置执行软件包构建。

软件包构建只负责主包，资源包仍通过 `构建资源包` 单独生成。

`VoyageForge/ForgeCLR/打开 Unity Build 面板`

打开 Unity Build Settings 面板。软件包平台、场景、输出路径、Development Build 等配置全部由 Unity 自己管理。

## 使用建议

1. 先配置 HybridCLR 热更新程序集。
2. 运行 `VoyageForge/ForgeCLR/快速设置`，自动创建默认场景并补齐 YooAssets Collector。
3. 运行 `VoyageForge/ForgeCLR/构建资源包`。
4. 资源包验证通过后，运行 `VoyageForge/ForgeCLR/构建软件包` 或打开 Unity Build Settings 面板构建软件包。

## 首次启动流程

1. 打开 `Edit/Project Settings/VoyageForge/ForgeCLR`，确认 `运行时配置 SO`、`YooAssets 包`、`Launcher 场景` 和 `启动场景地址`。
2. 打开 `Edit/Project Settings/VoyageForge/Bridge`，确认默认环境为 `dev`，并为 `Assets` 端点配置资源服务器地址。
3. 运行 `VoyageForge/ForgeCLR/快速设置`，创建默认目录、默认首场景、运行时配置和 YooAssets 基础配置。
4. 运行 `VoyageForge/ForgeCLR/构建资源包`，生成热更新 DLL、AOT 元数据 DLL 和 YooAssets 资源包。
5. 打开 `VoyageForge/ForgeCLR/File Server`，确认配置自检通过后启动局域网文件服务器。
6. 如果要真机 HostPlayMode 测试，把 Bridge 的 `Assets` 地址指向文件服务器显示的访问地址。
7. 运行 `VoyageForge/ForgeCLR/构建软件包`，确认弹窗里的平台、输出路径、Development Build 和场景列表后开始构建。

## 待改进
1. 后续可以把 YooAssets Builder 的默认构建参数也纳入快速设置建议，但仍保持最终配置由 YooAssets 自己维护。
2. Android 真机测试仍需要结合项目实际图形 API 和启动参数确认是否使用 `-force-gles`。
3. 文件服务器的 Windows 防火墙策略仍需由使用者在系统层允许 Unity Editor 的入站 TCP 访问。
4. 后续新增检测项时优先追加到 `ForgeCLRValidationUtility`，避免构建、快速设置和 Project Settings 三处逻辑分散。
