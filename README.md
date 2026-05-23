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

## 配置项详解

### 运行时配置 SO

`ForgeCLRRuntimeSettings` 是运行时真正读取的配置，默认路径为：

`Assets/Resources/VoyageForge/Config/ForgeCLRRuntimeSettings.asset`

运行时由 `Launcher` 通过 Resources 加载。它保存 YooAssets 包名、PlayMode、AOT 元数据 DLL 路径、热更新 DLL 路径、是否加载首场景和首场景路径。Project Settings 里的 `运行时配置 SO` 字段只读，避免手动换成另一个 SO 后，一键构建填充到错误资产里。

### DLL 拷贝目录

Project Settings 中只允许修改中间目录名，例如默认 `HotUpdateDll`。最终路径固定为：

- 热更新程序集：`Assets/{目录名}/HotUpdateDll`
- AOT 元数据：`Assets/{目录名}/MetadataDll`

这样做是为了让构建流程、环境检测、YooAssets Collector 和运行时配置的路径规则保持一致。构建资源包时会把目录中的 `.dll.bytes` 写入 `ForgeCLRRuntimeSettings`，运行时再通过 YooAssets 加载。

### Launcher 场景与启动场景

这两个场景承担不同职责：

- `Launcher 场景`：Unity 软件包启动的第一个场景，通常只放 `Launcher`、加载界面和必要的启动 UI。构建软件包前会自动放到 Build Settings 第一位。
- `启动场景地址`：热更新程序集和资源包准备完成后加载的第一个业务场景。它需要被 YooAssets Collector 收集，保存的是完整资源路径，例如 `Assets/Scenes/Main.unity`。

如果关闭 `启动后加载首场景`，环境检测不会再把“启动场景 AB 收集”作为阻断项。

### 局域网文件服务器

文件服务器配置保存在 `ProjectSettings/ForgeCLRSettings.asset`，不会写入 `EditorPrefs`。这样团队成员可以共享默认根目录、端口、绑定 IP 和自动恢复开关。

入口：

`VoyageForge/ForgeCLR/File Server`

窗口使用 UI Toolkit，界面文件位于：

- `Assets/ForgeCLR/Editor/FileServer/VoyageForgeFileServerWindow.uxml`
- `Assets/ForgeCLR/Editor/FileServer/VoyageForgeFileServerWindow.uss`

字段说明：

- `根目录`：文件服务器暴露给局域网设备访问的目录，默认是项目根目录下的 `Bundles`。
- `端口`：HTTP 监听端口，默认 `8899`。端口被占用时可以点击 `自动端口` 查找可用端口。
- `绑定 IP`：为空时监听 `0.0.0.0`，适合大多数局域网调试；如果绑定到具体 IP，应优先选择 Wi-Fi 或 Ethernet 网卡。
- `域重载后自动恢复服务器`：进入 Play Mode 或脚本编译导致域重载后，如果服务器之前处于运行状态，会尝试自动恢复。

窗口顶部的 `配置自检` 会检查根目录、端口和绑定 IP。绿色表示可以直接启动；黄色通常表示端口占用或疑似虚拟网卡；红色表示根目录不存在或端口非法。

真机 HostPlayMode 测试时，把 Bridge 的 `Assets` 端点设置为文件服务器显示的访问地址。局域网设备无法访问时，优先检查 Windows 防火墙是否允许 Unity Editor 入站 TCP。

YooAssets 需要两个配置文件，ForgeCLR 会在快速设置和环境检测中检查它们：

- `Assets/Resources/YooAssetSettings.asset` — 运行时通过 Resources 加载
- `Assets/AssetBundleCollectorSetting.asset` — 仅编辑器使用，放在 Assets 根目录避免被打包进 Resources

YooAssets 的 Package、Collector、Builder、压缩方式、输出根目录、内置资源拷贝等配置仍然在 YooAssets 自己的窗口中维护：

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

构建开始前会弹出确认窗口，显示 BuildTarget、输出路径、Development Build、BuildOptions 和启用场景列表。点击取消时不会执行 `HybridCLR/Generate/All`，避免误触发长时间构建。

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

## 环境检测

环境检测系统采用面向接口设计，每个检测项独立为一个类，实现 `IForgeCLRValidationCheck` 接口。通过反射自动发现所有检测类，无需手动注册。详细架构、现有检测项列表和添加新检测的步骤见：

→ [VALIDATION.md](VALIDATION.md)

## 常见问题

### 资源包构建为什么不调用 HybridCLR/Generate/All？

`HybridCLR/Generate/All` 会生成主包构建所需的 AOT 裁剪数据和桥接数据，更适合软件包构建前执行。资源包构建只需要编译热更新 DLL、拷贝 DLL 并打 AB。如果在资源包流程里调用完整 Generate，可能导致空包内程序集数据和当前 DLL 内容不一致。

### 为什么 DLL 要打成 .dll.bytes？

Unity 会把 `.dll` 识别为托管程序集，容易被编辑器导入和编译。改成 `.dll.bytes` 后可以作为普通二进制资源交给 YooAssets 收集，运行时再读取 bytes 加载。

### 为什么 AssetBundleCollectorSetting 放在 Assets 根目录？

`AssetBundleCollectorSetting` 仅供编辑器使用（YooAssets 构建管线读取），不需要在运行时通过 Resources 加载。放在 `Assets/` 根目录避免被打包进 Resources，减少不必要的包体。运行时需要的配置是 `YooAssetSettings`，它仍然放在 `Assets/Resources/` 下。

### 文件服务器启动成功但手机访问不到怎么办？

先确认手机和电脑在同一个局域网，访问地址不要使用 `0.0.0.0`，要使用窗口显示的真实 IP。然后检查 Windows 防火墙，允许 Unity Editor 通过专用网络的入站 TCP 访问。VPN 或虚拟网卡较多时，优先绑定 Wi-Fi / Ethernet 的 IP。

## 待改进
1. 优化一键配置流程，减少手动步骤。
