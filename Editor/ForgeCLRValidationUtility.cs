using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor.Installer;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using YooAsset.Editor;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// ForgeCLR 配置检测与自动修复入口。
    /// 新增检测项时优先在这里追加规则，避免快速设置、Project Settings 和构建流程各自维护一份逻辑。
    /// </summary>
    public static class ForgeCLRValidationUtility
    {
        /// <summary>
        /// 创建当前 ForgeCLR 环境检测报告。
        /// </summary>
        /// <returns>环境检测报告。</returns>
        public static ForgeCLRValidationReport CreateReport()
        {
            var items = new List<ForgeCLRValidationItem>();
            var settings = ForgeCLRSettings.instance;

            // 基础文件和第三方依赖检测：这些项缺失时，后续构建流程通常无法继续。
            AppendCheck(items, File.Exists("Packages/manifest.json"), "Packages Manifest", "Packages/manifest.json 已找到",
                "未找到 Packages/manifest.json");
            AppendCheck(items, File.Exists("ProjectSettings/HybridCLRSettings.asset"), "HybridCLR Settings",
                "HybridCLRSettings.asset 已找到", "未找到 HybridCLRSettings.asset");
            AppendCheck(items, ForgeCLRRuntimeSettingsEditorUtility.TryGetYooAssetSettings(out _), "YooAsset Settings",
                "YooAssetSettings.asset 已在 Resources 下找到", "未找到 Resources/YooAssetSettings.asset，可执行快速设置创建");
            AppendCheck(items,
                ForgeCLRRuntimeSettingsEditorUtility.TryGetYooAssetCollectorSetting(out var collectorSetting),
                "YooAssets Collector", "YooAssets Collector 设置已找到", "未找到 YooAssets Collector 设置，可执行快速设置创建");
            AppendCheck(items, Type.GetType("YooAsset.YooAssets, YooAsset") != null, "YooAssets Runtime",
                "YooAssets Runtime 已安装", "未检测到 YooAssets Runtime");
            AppendCheck(items, Type.GetType("Cysharp.Threading.Tasks.UniTask, UniTask") != null, "UniTask Runtime",
                "UniTask Runtime 已安装", "未检测到 UniTask Runtime");

            AppendHybridClrInstallerCheck(items);

            // ForgeCLR 自身配置检测：Project Settings、Runtime SO 和 YooAssets Collector 必须互相指向同一个包。
            AppendCheck(items, settings.RuntimeSettings != null, "运行时配置 SO", "ForgeCLR Project Settings 已引用运行时 SO",
                "ForgeCLR Project Settings 未引用运行时 SO，可执行快速设置创建");
            AppendCheck(items, HasRuntimePackageInCollector(settings.RuntimeSettings, collectorSetting),
                "YooAssets Package", "运行时资源包名称存在于 YooAssets Collector 配置中", "运行时资源包名称未在 YooAssets Collector 配置中找到");
            AppendCheck(items, IsValidFolderName(settings.DllCopyDirectoryName), "DLL 拷贝根目录名",
                $"DLL 拷贝根目录名有效：{settings.DllCopyDirectoryName}", "DLL 拷贝根目录名不能为空，也不能包含路径分隔符");
            AppendCheck(items, IsAssetPath(settings.HotUpdateDllCopyDirectory), "热更新 DLL 拷贝目录",
                "热更新 DLL 拷贝目录位于 Assets 下", "热更新 DLL 拷贝目录必须位于 Assets 下");
            AppendCheck(items, IsAssetPath(settings.MetadataDllCopyDirectory), "AOT 元数据 DLL 拷贝目录",
                "AOT 元数据 DLL 拷贝目录位于 Assets 下", "AOT 元数据 DLL 拷贝目录必须位于 Assets 下");
            AppendCheck(items,
                HasCollectorForPath(settings.RuntimeSettings, collectorSetting, settings.HotUpdateDllCopyDirectory),
                "热更新 DLL AB 收集", "热更新 DLL 目录已加入当前 YooAssets 包", "热更新 DLL 目录尚未加入当前 YooAssets 包，可点击修复补齐");
            AppendCheck(items,
                HasCollectorForPath(settings.RuntimeSettings, collectorSetting, settings.MetadataDllCopyDirectory),
                "AOT 元数据 DLL AB 收集", "AOT 元数据 DLL 目录已加入当前 YooAssets 包", "AOT 元数据 DLL 目录尚未加入当前 YooAssets 包，可点击修复补齐");
            var startupSceneCollected = settings.RuntimeSettings != null &&
                                        (settings.RuntimeSettings.LoadStartupScene == false ||
                                         HasCollectorForPath(settings.RuntimeSettings, collectorSetting,
                                             settings.RuntimeSettings.StartupSceneLocation));
            AppendCheck(items, startupSceneCollected, "启动场景 AB 收集", "启动场景已加入当前 YooAssets 包，或已关闭自动加载首场景",
                "启动场景尚未加入当前 YooAssets 包，可点击修复补齐");

            // 软件包入口检测：Launcher 场景必须存在，并且应该是 Build Settings 中第一个启用场景。
            AppendCheck(items, AssetDatabase.LoadAssetAtPath<SceneAsset>(settings.LauncherSceneLocation) != null,
                "Launcher 场景", $"Launcher 场景存在：{settings.LauncherSceneLocation}",
                "Launcher 场景不存在，请在 Project Settings 中选择有效场景");
            AppendCheck(items, IsLauncherSceneFirstInBuildSettings(settings.LauncherSceneLocation),
                "Launcher Build Settings", "Launcher 场景已位于 Build Settings 第一位",
                "Launcher 场景未位于 Build Settings 第一位，可点击修复");
            AppendFileServerChecks(items, settings);
            AppendAndroidGraphicsCheck(items);

            AppendDirectoryWarning(items, settings.HotUpdateDllCopyDirectory, "热更新 DLL 拷贝目录状态");
            AppendDirectoryWarning(items, settings.MetadataDllCopyDirectory, "AOT 元数据 DLL 拷贝目录状态");

            return new ForgeCLRValidationReport(items);
        }

        /// <summary>
        /// 构建前验证配置；失败项会直接阻断构建。
        /// </summary>
        /// <param name="context">构建上下文名称。</param>
        /// <returns>环境检测报告。</returns>
        public static ForgeCLRValidationReport ValidateForBuild(string context)
        {
            var report = CreateReport();
            if (report.FailedCount == 0)
            {
                return report;
            }

            var message = string.Join("\n", report.Items
                .Where(item => item.Status == ForgeCLRValidationStatus.Failed)
                .Select(item => $"{item.Title}：{item.Message}"));
            throw new BuildFailedException($"ForgeCLR {context} 前置检查失败：\n{message}");
        }

        /// <summary>
        /// 判断指定环境检测项是否支持自动修复。
        /// </summary>
        /// <param name="title">检测项标题。</param>
        /// <returns>支持自动修复时返回 true。</returns>
        public static bool CanRepair(string title)
        {
            return title switch
            {
                "YooAsset Settings" => true,
                "YooAssets Collector" => true,
                "运行时配置 SO" => true,
                "YooAssets Package" => true,
                "DLL 拷贝根目录名" => true,
                "热更新 DLL 拷贝目录" => true,
                "AOT 元数据 DLL 拷贝目录" => true,
                "热更新 DLL AB 收集" => true,
                "AOT 元数据 DLL AB 收集" => true,
                "启动场景 AB 收集" => true,
                "Launcher Build Settings" => true,
                "文件服务器根目录" => true,
                "文件服务器端口" => true,
                "热更新 DLL 拷贝目录状态" => true,
                "AOT 元数据 DLL 拷贝目录状态" => true,
                _ => false
            };
        }

        /// <summary>
        /// 自动修复指定环境检测项。
        /// </summary>
        /// <param name="title">检测项标题。</param>
        /// <returns>修复成功时返回 true；不支持该项时返回 false。</returns>
        public static bool TryRepair(string title)
        {
            if (CanRepair(title) == false)
            {
                return false;
            }

            var settings = ForgeCLRSettings.instance;
            switch (title)
            {
                case "YooAsset Settings":
                    // YooAssetSettings 是运行时初始化 YooAssets 的基础配置，必须在 Resources 下。
                    ForgeCLRRuntimeSettingsEditorUtility.EnsureYooAssetSettings();
                    break;
                case "YooAssets Collector":
                    // Collector 也固定放在 Resources 下，确保编辑器和运行时读取路径一致。
                    ForgeCLRRuntimeSettingsEditorUtility.EnsureYooAssetCollectorSetting();
                    break;
                case "运行时配置 SO":
                    ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
                    break;
                case "YooAssets Package":
                    // 包名修复会同时补齐 Collector 和 Runtime SO，避免只改一边。
                    ForgeCLRQuickSetup.EnsureYooAssetCollectorConfiguration();
                    ForgeCLRRuntimeSettingsEditorUtility.AutoFillRuntimeSettings();
                    break;
                case "DLL 拷贝根目录名":
                    settings.SaveSettings();
                    break;
                case "热更新 DLL 拷贝目录":
                case "热更新 DLL 拷贝目录状态":
                    CreateDirectory(settings.HotUpdateDllCopyDirectory);
                    break;
                case "AOT 元数据 DLL 拷贝目录":
                case "AOT 元数据 DLL 拷贝目录状态":
                    CreateDirectory(settings.MetadataDllCopyDirectory);
                    break;
                case "热更新 DLL AB 收集":
                case "AOT 元数据 DLL AB 收集":
                case "启动场景 AB 收集":
                    // Collector 修复只处理 ForgeCLR 管辖的目录和启动场景，不覆盖 YooAssets 其它用户配置。
                    ForgeCLRQuickSetup.EnsureYooAssetCollectorConfiguration();
                    break;
                case "Launcher Build Settings":
                    EnsureLauncherSceneInBuildSettings();
                    break;
                case "文件服务器根目录":
                    CreateDirectory(settings.FileServerRootDirectory);
                    break;
                case "文件服务器端口":
                    var port = VoyageForgeFileServer.FindAvailablePort(settings.FileServerPort);
                    if (port > 0)
                    {
                        settings.SetFileServerConfig(settings.FileServerRootDirectory, port,
                            settings.FileServerBindIPAddress);
                    }

                    break;
            }

            settings.SaveSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ForgeCLR] 已尝试修复环境检测项：{title}");
            return true;
        }

        /// <summary>
        /// 确保 Launcher 场景位于 Build Settings 第一位。
        /// </summary>
        public static void EnsureLauncherSceneInBuildSettings()
        {
            var settings = ForgeCLRSettings.instance;
            var launcherScene = settings.LauncherSceneLocation;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(launcherScene) == null)
            {
                launcherScene = ForgeCLRSettings.DefaultLauncherScenePath;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(launcherScene) == null)
            {
                throw new BuildFailedException($"Launcher 场景不存在：{settings.LauncherSceneLocation}");
            }

            var scenes = EditorBuildSettings.scenes
                .Where(scene => string.IsNullOrWhiteSpace(scene.path) == false)
                .Where(scene => NormalizeAssetPath(scene.path) != NormalizeAssetPath(launcherScene))
                .ToList();

            scenes.Insert(0, new EditorBuildSettingsScene(launcherScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[ForgeCLR] 已设置 Launcher 场景为 Build Settings 第一位：{launcherScene}");
        }

        /// <summary>
        /// 检查 HybridCLR Installer 状态。
        /// </summary>
        /// <param name="items">检测项集合。</param>
        private static void AppendHybridClrInstallerCheck(List<ForgeCLRValidationItem> items)
        {
            try
            {
                var installer = new InstallerController();
                AppendCheck(items, installer.HasInstalledHybridCLR(), "HybridCLR Installer", "HybridCLR Installer 已完成",
                    "HybridCLR Installer 尚未完成");
            }
            catch (Exception e)
            {
                items.Add(new ForgeCLRValidationItem("HybridCLR Installer", $"HybridCLR Installer 检测失败：{e.Message}",
                    ForgeCLRValidationStatus.Failed));
            }
        }

        /// <summary>
        /// 追加文件服务器配置检测项。
        /// </summary>
        /// <param name="items">检测项集合。</param>
        /// <param name="settings">ForgeCLR 项目配置。</param>
        private static void AppendFileServerChecks(List<ForgeCLRValidationItem> items, ForgeCLRSettings settings)
        {
            var rootDirectory = settings.FileServerRootDirectory;
            var port = settings.FileServerPort;
            bool rootExists = Directory.Exists(rootDirectory);
            items.Add(new ForgeCLRValidationItem(
                "文件服务器根目录",
                rootExists ? $"文件服务器根目录已存在：{rootDirectory}" : $"文件服务器根目录不存在，可点击修复创建：{rootDirectory}",
                rootExists ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Warning));

            bool validPort = port > 0 && port <= 65535;
            bool runningOnPort = VoyageForgeFileServerSingleton.Server != null &&
                                 VoyageForgeFileServerSingleton.Server.IsRunning &&
                                 VoyageForgeFileServerSingleton.Server.Port == port;
            bool portAvailable = runningOnPort || VoyageForgeFileServer.IsPortAvailable(port);
            items.Add(new ForgeCLRValidationItem(
                "文件服务器端口",
                validPort && portAvailable ? $"文件服务器端口可用：{port}" : $"文件服务器端口不可用：{port}",
                validPort && portAvailable ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Warning));
        }

        /// <summary>
        /// Android 平台补充图形 API 提醒。
        /// </summary>
        /// <param name="items">检测项集合。</param>
        private static void AppendAndroidGraphicsCheck(List<ForgeCLRValidationItem> items)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                return;
            }
            
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        
            // 2. 拼接 UnityHub 文件夹和 projectsInfo.json 文件
            string hubProjectsFile = Path.Combine(appDataPath, "UnityHub", "projectsInfo.json");
        
            Debug.Log("Hub 项目信息文件路径：" + hubProjectsFile);
            

            var isGles = Environment.GetCommandLineArgs().Contains("-force-gles");

            items.Add(new ForgeCLRValidationItem(
                "Android 图形 API",
                isGles
                    ? "Unity Editor 启动命令行已包含 -force-gles"
                    : "Unity Editor 启动命令行未包含  -force-gles",
                isGles ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed));
        }

        /// <summary>
        /// 检查运行时配置中的包名是否存在于 YooAssets Collector 配置。
        /// </summary>
        private static bool HasRuntimePackageInCollector(
            VoyageForge.ForgeCLR.Runtime.ForgeCLRRuntimeSettings runtimeSettings,
            AssetBundleCollectorSetting collectorSetting)
        {
            if (runtimeSettings == null || collectorSetting == null || collectorSetting.Packages == null)
            {
                return false;
            }

            return collectorSetting.Packages.Any(package => package.PackageName == runtimeSettings.PackageName);
        }

        /// <summary>
        /// 检查指定资源路径是否已经加入运行时包名对应的 YooAssets Collector。
        /// </summary>
        private static bool HasCollectorForPath(
            VoyageForge.ForgeCLR.Runtime.ForgeCLRRuntimeSettings runtimeSettings,
            AssetBundleCollectorSetting collectorSetting,
            string collectPath)
        {
            if (runtimeSettings == null || collectorSetting == null || string.IsNullOrWhiteSpace(collectPath))
            {
                return false;
            }

            var package =
                collectorSetting.Packages?.FirstOrDefault(item => item.PackageName == runtimeSettings.PackageName);
            if (package == null)
            {
                return false;
            }

            var normalizedPath = NormalizeAssetPath(collectPath);
            return package.Groups.Any(group =>
                group.Collectors.Any(collector => NormalizeAssetPath(collector.CollectPath) == normalizedPath));
        }

        /// <summary>
        /// 检查 Launcher 场景是否位于 Build Settings 第一位。
        /// </summary>
        /// <param name="launcherSceneLocation">Launcher 场景路径。</param>
        /// <returns>位于第一位并启用时返回 true。</returns>
        private static bool IsLauncherSceneFirstInBuildSettings(string launcherSceneLocation)
        {
            var firstScene = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled);
            return firstScene != null &&
                   NormalizeAssetPath(firstScene.path) == NormalizeAssetPath(launcherSceneLocation);
        }

        /// <summary>
        /// 追加一条通过或失败检测结果。
        /// </summary>
        private static void AppendCheck(List<ForgeCLRValidationItem> items, bool success, string title,
            string successMessage, string errorMessage)
        {
            items.Add(new ForgeCLRValidationItem(
                title,
                success ? successMessage : errorMessage,
                success ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed));
        }

        /// <summary>
        /// 追加目录存在性警告。
        /// </summary>
        private static void AppendDirectoryWarning(List<ForgeCLRValidationItem> items, string path, string title)
        {
            if (!IsAssetPath(path))
            {
                return;
            }

            bool exists = Directory.Exists(path);
            items.Add(new ForgeCLRValidationItem(
                title,
                exists ? $"目录已存在：{path}" : $"目录尚不存在，执行快速设置或拷贝 DLL 时会创建：{path}",
                exists ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Warning));
        }

        /// <summary>
        /// 创建目录。
        /// </summary>
        private static void CreateDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) == false && Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// 判断路径是否是 Assets 相对路径。
        /// </summary>
        private static bool IsAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) == false && path.StartsWith("Assets/");
        }

        /// <summary>
        /// 判断目录名称是否是单级目录名。
        /// </summary>
        private static bool IsValidFolderName(string value)
        {
            return string.IsNullOrWhiteSpace(value) == false &&
                   value.Contains("/") == false &&
                   value.Contains("\\") == false;
        }

        /// <summary>
        /// 规范化 Unity 资源路径分隔符。
        /// </summary>
        private static string NormalizeAssetPath(string path)
        {
            return path?.Replace("\\", "/") ?? string.Empty;
        }
    }
}