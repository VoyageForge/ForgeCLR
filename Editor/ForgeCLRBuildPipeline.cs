using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Installer;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// ForgeCLR 一键构建流程，串联 HybridCLR、DLL 拷贝和 YooAssets，并提供 Unity Build 面板入口。
    /// </summary>
    public static class ForgeCLRBuildPipeline
    {
        /// <summary>
        /// 构建资源包菜单路径。
        /// </summary>
        private const string BuildResourceMenuPath = "VoyageForge/ForgeCLR/构建资源包";

        /// <summary>
        /// 打开 Unity Build 面板菜单路径。
        /// </summary>
        private const string OpenBuildPanelMenuPath = "VoyageForge/ForgeCLR/打开 Unity Build 面板";

        /// <summary>
        /// 构建软件包菜单路径。
        /// </summary>
        private const string BuildPlayerMenuPath = "VoyageForge/ForgeCLR/构建软件包";

        /// <summary>
        /// 构建资源包：编译热更 DLL、拷贝 DLL，再调用 YooAssets 打 AB。
        /// </summary>
        [MenuItem(BuildResourceMenuPath)]
        public static void BuildResourcePackage()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;

            // 资源包构建依赖 HybridCLR、YooAssets 和 UniTask，但不执行 Generate/All。
            // Generate/All 会生成主包构建数据，应放在软件包构建流程中执行。
            ValidateEnvironment(true);

            Debug.Log("<color=red>[ForgeCLR] 编译 HybridCLR 热更新 DLL。</color>");
            CompileDllCommand.CompileDll(target, EditorUserBuildSettings.development);

            Debug.Log("[ForgeCLR] 拷贝热更新 DLL 和 AOT 元数据 DLL。");
            var copyResult = CopyHotUpdateDllToFolder.CopyAssemblies(target, false);

            Debug.Log("[ForgeCLR] 自动填充运行时 SO。");
            ForgeCLRRuntimeSettingsEditorUtility.AutoFillRuntimeSettings(copyResult);

            Debug.Log("[ForgeCLR] 检查 YooAssets 收集配置。");
            ForgeCLRQuickSetup.EnsureYooAssetCollectorConfiguration();

            ForgeCLRValidationUtility.ValidateForBuild("资源包构建");

            Debug.Log("[ForgeCLR] 开始 YooAssets 资源构建。");
            var results = BuildYooAssetPackages(target);
            var failedResult = results.FirstOrDefault(result => result.Success == false);
            if (failedResult != null)
                throw new BuildFailedException($"YooAssets 构建失败：{failedResult.ErrorInfo}");

            var outputDirectories = string.Join("\n", results.Select(result => result.OutputPackageDirectory));
            EditorUtility.DisplayDialog("ForgeCLR 构建资源包", $"资源包构建完成：\n{outputDirectories}", "确定");
            Debug.Log($"[ForgeCLR] 资源包构建完成：\n{outputDirectories}");
        }

        /// <summary>
        /// 构建软件包：执行 HybridCLR 打包前生成、校正 Launcher 场景，然后调用 Unity 默认 Build 面板流程。
        /// </summary>
        [MenuItem(BuildPlayerMenuPath)]
        public static void BuildPlayerPackage()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            ValidateEnvironment(true);

            // 软件包必须从 Launcher 场景启动，否则运行时补丁流程和首场景加载不会执行。
            Debug.Log("[ForgeCLR] 检查 Launcher 场景和 Build Settings。");
            ForgeCLRValidationUtility.EnsureLauncherSceneInBuildSettings();
            ForgeCLRValidationUtility.ValidateForBuild("软件包构建");

            // 先读取 Unity Build Settings 当前配置，让用户确认实际输出路径和构建选项。
            // 这样 ForgeCLR 不额外维护平台、Development Build 或输出目录配置。
            var options = BuildPlayerWindow.DefaultBuildMethods.GetBuildPlayerOptions(new BuildPlayerOptions
            {
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target)
            });

            ForgeCLRValidationUtility.EnsureLauncherSceneInBuildSettings();
            options.scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (ConfirmBuildPlayer(options) == false)
            {
                Debug.Log("[ForgeCLR] 已取消构建软件包。");
                return;
            }

            // 确认后才执行 HybridCLR Generate/All，避免用户误点菜单就触发耗时生成和构建。
            Debug.Log("[ForgeCLR] 开始 HybridCLR Generate/All。");
            PrebuildCommand.GenerateAll();

            Debug.Log("[ForgeCLR] 使用 Unity Build Settings 当前配置构建软件包。");
            BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        }

        /// <summary>
        /// 打开 Unity Build 面板，软件包平台、路径和 Development Build 由 Unity 自己管理。
        /// </summary>
        [MenuItem(OpenBuildPanelMenuPath)]
        public static void OpenUnityBuildPanel()
        {
            if (EditorApplication.ExecuteMenuItem("File/Build Settings...") == false)
                Debug.LogWarning("[ForgeCLR] 打开 Unity Build Settings 面板失败，请手动打开 File/Build Settings...");
        }

        /// <summary>
        /// 验证 HybridCLR、YooAssets 和 UniTask 是否满足构建要求。
        /// </summary>
        /// <param name="requireHybridClrInstalled">是否要求 HybridCLR 已完成 Installer 安装。</param>
        public static void ValidateEnvironment(bool requireHybridClrInstalled)
        {
            if (Type.GetType("HybridCLR.RuntimeApi, HybridCLR.Runtime") == null)
                throw new BuildFailedException("未检测到 HybridCLR.Runtime，请确认 HybridCLR 包已安装。");

            if (Type.GetType("YooAsset.YooAssets, YooAsset") == null)
                throw new BuildFailedException("未检测到 YooAssets，请确认 YooAssets 包已安装。");

            if (Type.GetType("Cysharp.Threading.Tasks.UniTask, UniTask") == null)
                throw new BuildFailedException("未检测到 UniTask，请确认 UniTask 已安装或源码已导入。");

            if (requireHybridClrInstalled)
            {
                var installer = new InstallerController();
                if (installer.HasInstalledHybridCLR() == false)
                    throw new BuildFailedException("HybridCLR 尚未执行 Installer，请先打开 HybridCLR/Installer 完成安装。");
            }

            if (ForgeCLRRuntimeSettingsEditorUtility.TryGetYooAssetSettings(out _) == false)
                throw new BuildFailedException("未找到 YooAssetSettings 设置，请先执行 ForgeCLR 快速设置。");

            if (ForgeCLRRuntimeSettingsEditorUtility.TryGetYooAssetCollectorSetting(out _) == false)
                throw new BuildFailedException("未找到 YooAssets Collector 设置，请先执行 ForgeCLR 快速设置或在 YooAssets 中创建配置。");
        }

        /// <summary>
        /// 构建软件包前显示关键配置摘要，避免误点后直接触发长时间构建。
        /// </summary>
        /// <param name="options">Unity Build Settings 解析出的构建参数。</param>
        /// <returns>用户确认继续时返回 true。</returns>
        private static bool ConfirmBuildPlayer(BuildPlayerOptions options)
        {
            var scenes = options.scenes == null || options.scenes.Length == 0
                ? "无启用场景"
                : string.Join("\n", options.scenes.Select(scene => $"  - {scene}"));
            var message = new StringBuilder();
            // 这里刻意展示 Unity 最终解析出的 BuildPlayerOptions，而不是 ForgeCLR 自己缓存的配置。
            // 用户看到的摘要应与 Unity 实际构建参数一致。
            message.AppendLine("即将执行 ForgeCLR 软件包构建：");
            message.AppendLine();
            message.AppendLine($"平台：{options.target}");
            message.AppendLine($"输出：{options.locationPathName}");
            message.AppendLine($"Development Build：{(options.options & BuildOptions.Development) != 0}");
            message.AppendLine($"Build Options：{options.options}");
            message.AppendLine();
            message.AppendLine("场景：");
            message.AppendLine(scenes);
            message.AppendLine();
            message.AppendLine("继续后会先执行 HybridCLR/Generate/All，然后调用 Unity 默认构建流程。");

            return EditorUtility.DisplayDialog("ForgeCLR 构建软件包", message.ToString(), "开始构建", "取消");
        }

        /// <summary>
        /// 按 YooAssets 自己的 Collector 和 Builder 配置构建所有资源包。
        /// </summary>
        /// <param name="target">目标构建平台。</param>
        /// <returns>YooAssets 构建结果集合。</returns>
        private static List<YooAsset.Editor.BuildResult> BuildYooAssetPackages(BuildTarget target)
        {
            var packages = AssetBundleCollectorSettingData.Setting.Packages;
            if (packages == null || packages.Count == 0)
                throw new BuildFailedException("YooAssets Collector 中没有配置任何 Package。");

            var results = new List<YooAsset.Editor.BuildResult>(packages.Count);
            foreach (var package in packages)
            {
                if (string.IsNullOrWhiteSpace(package.PackageName))
                    continue;

                var result = BuildYooAssetPackage(package.PackageName, target);
                results.Add(result);

                if (result.Success == false)
                    break;
            }

            return results;
        }

        /// <summary>
        /// 按 YooAssets Builder 中记录的构建管线构建单个资源包。
        /// </summary>
        /// <param name="packageName">YooAssets 资源包名称。</param>
        /// <param name="target">目标构建平台。</param>
        /// <returns>YooAssets 构建结果。</returns>
        private static YooAsset.Editor.BuildResult BuildYooAssetPackage(string packageName, BuildTarget target)
        {
            var pipelineName = AssetBundleBuilderSetting.GetPackageBuildPipeline(packageName);
            if (Enum.TryParse(pipelineName, out EBuildPipeline pipeline) == false)
                throw new BuildFailedException($"YooAssets 构建管线无效：{packageName} -> {pipelineName}");

            return pipeline switch
            {
                EBuildPipeline.BuiltinBuildPipeline => BuildBuiltinPackage(packageName, target, pipelineName),
                EBuildPipeline.ScriptableBuildPipeline => BuildScriptablePackage(packageName, target, pipelineName),
                EBuildPipeline.RawFileBuildPipeline => BuildRawFilePackage(packageName, target, pipelineName),
                EBuildPipeline.EditorSimulateBuildPipeline => BuildEditorSimulatePackage(packageName, target, pipelineName),
                _ => throw new BuildFailedException($"暂不支持 YooAssets 构建管线：{pipelineName}")
            };
        }

        /// <summary>
        /// 使用 YooAssets ScriptableBuildPipeline 构建资源包。
        /// </summary>
        /// <param name="packageName">YooAssets 资源包名称。</param>
        /// <param name="target">目标构建平台。</param>
        /// <param name="pipelineName">YooAssets 构建管线名称。</param>
        /// <returns>YooAssets 构建结果。</returns>
        private static YooAsset.Editor.BuildResult BuildScriptablePackage(string packageName, BuildTarget target, string pipelineName)
        {
            var uniqueBundleName = AssetBundleCollectorSettingData.Setting.UniqueBundleName;
            var buildParameters = new ScriptableBuildParameters
            {
                BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = pipelineName,
                BuildBundleType = (int)EBuildBundleType.AssetBundle,
                BuildTarget = target,
                PackageName = packageName,
                PackageVersion = CreateDefaultPackageVersion(),
                EnableSharePackRule = true,
                VerifyBuildingResult = true,
                FileNameStyle = AssetBundleBuilderSetting.GetPackageFileNameStyle(packageName, pipelineName),
                BuildinFileCopyOption = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(packageName, pipelineName),
                BuildinFileCopyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(packageName, pipelineName),
                CompressOption = AssetBundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName),
                ClearBuildCacheFiles = AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName),
                UseAssetDependencyDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName),
                EncryptionServices = CreateEncryptionServicesInstance(packageName, pipelineName),
                ManifestProcessServices = CreateManifestProcessServicesInstance(packageName, pipelineName),
                ManifestRestoreServices = CreateManifestRestoreServicesInstance(packageName, pipelineName),
                BuiltinShadersBundleName = DefaultPackRule.CreateShadersPackRuleResult().GetBundleName(packageName, uniqueBundleName),
                MonoScriptsBundleName = DefaultPackRule.CreateMonosPackRuleResult().GetBundleName(packageName, uniqueBundleName)
            };

            var pipeline = new ScriptableBuildPipeline();
            return pipeline.Run(buildParameters, true);
        }

        /// <summary>
        /// 使用 YooAssets BuiltinBuildPipeline 构建资源包。
        /// </summary>
        /// <param name="packageName">YooAssets 资源包名称。</param>
        /// <param name="target">目标构建平台。</param>
        /// <param name="pipelineName">YooAssets 构建管线名称。</param>
        /// <returns>YooAssets 构建结果。</returns>
        private static YooAsset.Editor.BuildResult BuildBuiltinPackage(string packageName, BuildTarget target, string pipelineName)
        {
            var buildParameters = new BuiltinBuildParameters
            {
                BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = pipelineName,
                BuildBundleType = (int)EBuildBundleType.AssetBundle,
                BuildTarget = target,
                PackageName = packageName,
                PackageVersion = CreateDefaultPackageVersion(),
                EnableSharePackRule = true,
                VerifyBuildingResult = true,
                FileNameStyle = AssetBundleBuilderSetting.GetPackageFileNameStyle(packageName, pipelineName),
                BuildinFileCopyOption = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(packageName, pipelineName),
                BuildinFileCopyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(packageName, pipelineName),
                CompressOption = AssetBundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName),
                ClearBuildCacheFiles = AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName),
                UseAssetDependencyDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName),
                EncryptionServices = CreateEncryptionServicesInstance(packageName, pipelineName),
                ManifestProcessServices = CreateManifestProcessServicesInstance(packageName, pipelineName),
                ManifestRestoreServices = CreateManifestRestoreServicesInstance(packageName, pipelineName)
            };

            var pipeline = new BuiltinBuildPipeline();
            return pipeline.Run(buildParameters, true);
        }

        /// <summary>
        /// 使用 YooAssets RawFileBuildPipeline 构建资源包。
        /// </summary>
        /// <param name="packageName">YooAssets 资源包名称。</param>
        /// <param name="target">目标构建平台。</param>
        /// <param name="pipelineName">YooAssets 构建管线名称。</param>
        /// <returns>YooAssets 构建结果。</returns>
        private static YooAsset.Editor.BuildResult BuildRawFilePackage(string packageName, BuildTarget target, string pipelineName)
        {
            var buildParameters = new RawFileBuildParameters
            {
                BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = pipelineName,
                BuildBundleType = (int)EBuildBundleType.RawBundle,
                BuildTarget = target,
                PackageName = packageName,
                PackageVersion = CreateDefaultPackageVersion(),
                VerifyBuildingResult = true,
                FileNameStyle = AssetBundleBuilderSetting.GetPackageFileNameStyle(packageName, pipelineName),
                BuildinFileCopyOption = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(packageName, pipelineName),
                BuildinFileCopyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(packageName, pipelineName),
                ClearBuildCacheFiles = AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName),
                UseAssetDependencyDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName),
                EncryptionServices = CreateEncryptionServicesInstance(packageName, pipelineName),
                ManifestProcessServices = CreateManifestProcessServicesInstance(packageName, pipelineName),
                ManifestRestoreServices = CreateManifestRestoreServicesInstance(packageName, pipelineName)
            };

            var pipeline = new RawFileBuildPipeline();
            return pipeline.Run(buildParameters, true);
        }

        /// <summary>
        /// 使用 YooAssets EditorSimulateBuildPipeline 构建模拟清单。
        /// </summary>
        /// <param name="packageName">YooAssets 资源包名称。</param>
        /// <param name="target">目标构建平台。</param>
        /// <param name="pipelineName">YooAssets 构建管线名称。</param>
        /// <returns>YooAssets 构建结果。</returns>
        private static YooAsset.Editor.BuildResult BuildEditorSimulatePackage(string packageName, BuildTarget target, string pipelineName)
        {
            var buildParameters = new EditorSimulateBuildParameters
            {
                BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = pipelineName,
                BuildBundleType = (int)EBuildBundleType.VirtualBundle,
                BuildTarget = target,
                PackageName = packageName,
                PackageVersion = "Simulate",
                VerifyBuildingResult = true,
                FileNameStyle = AssetBundleBuilderSetting.GetPackageFileNameStyle(packageName, pipelineName),
                BuildinFileCopyOption = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(packageName, pipelineName),
                BuildinFileCopyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(packageName, pipelineName),
                ClearBuildCacheFiles = AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName),
                UseAssetDependencyDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName),
                EncryptionServices = CreateEncryptionServicesInstance(packageName, pipelineName),
                ManifestProcessServices = CreateManifestProcessServicesInstance(packageName, pipelineName),
                ManifestRestoreServices = CreateManifestRestoreServicesInstance(packageName, pipelineName)
            };

            var pipeline = new EditorSimulateBuildPipeline();
            return pipeline.Run(buildParameters, true);
        }

        /// <summary>
        /// 创建资源包加密服务实例，配置来源为 YooAssets Builder。
        /// </summary>
        /// <param name="packageName">YooAssets 资源包名称。</param>
        /// <param name="pipelineName">YooAssets 构建管线名称。</param>
        /// <returns>资源包加密服务实例。</returns>
        private static IEncryptionServices CreateEncryptionServicesInstance(string packageName, string pipelineName)
        {
            return CreateYooAssetService<IEncryptionServices>(
                AssetBundleBuilderSetting.GetPackageEncyptionServicesClassName(packageName, pipelineName));
        }

        /// <summary>
        /// 创建资源清单处理服务实例，配置来源为 YooAssets Builder。
        /// </summary>
        /// <param name="packageName">YooAssets 资源包名称。</param>
        /// <param name="pipelineName">YooAssets 构建管线名称。</param>
        /// <returns>资源清单处理服务实例。</returns>
        private static IManifestProcessServices CreateManifestProcessServicesInstance(string packageName, string pipelineName)
        {
            return CreateYooAssetService<IManifestProcessServices>(
                AssetBundleBuilderSetting.GetPackageManifestProcessServicesClassName(packageName, pipelineName));
        }

        /// <summary>
        /// 创建资源清单还原服务实例，配置来源为 YooAssets Builder。
        /// </summary>
        /// <param name="packageName">YooAssets 资源包名称。</param>
        /// <param name="pipelineName">YooAssets 构建管线名称。</param>
        /// <returns>资源清单还原服务实例。</returns>
        private static IManifestRestoreServices CreateManifestRestoreServicesInstance(string packageName, string pipelineName)
        {
            return CreateYooAssetService<IManifestRestoreServices>(
                AssetBundleBuilderSetting.GetPackageManifestRestoreServicesClassName(packageName, pipelineName));
        }

        /// <summary>
        /// 创建 YooAssets 构建服务实例。
        /// </summary>
        /// <typeparam name="TService">服务接口类型。</typeparam>
        /// <param name="className">服务实现类型完整名称。</param>
        /// <returns>服务实例。</returns>
        private static TService CreateYooAssetService<TService>(string className) where TService : class
        {
            var classType = EditorTools.GetAssignableTypes(typeof(TService))
                .Find(type => type.FullName == className);
            return classType != null ? Activator.CreateInstance(classType) as TService : null;
        }

        /// <summary>
        /// 创建和 YooAssets Builder 默认规则一致的资源版本号。
        /// </summary>
        /// <returns>资源版本号。</returns>
        private static string CreateDefaultPackageVersion()
        {
            var totalMinutes = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
            return $"{DateTime.Now:yyyy-MM-dd}-{totalMinutes}";
        }

    }
}
