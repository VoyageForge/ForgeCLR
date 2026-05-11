using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HybridCLR.Editor.Installer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset.Editor;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// ForgeCLR 环境检测状态。
    /// </summary>
    public enum ForgeCLRValidationStatus
    {
        /// <summary>
        /// 检测通过。
        /// </summary>
        Passed,

        /// <summary>
        /// 检测失败，会阻断构建或运行。
        /// </summary>
        Failed,

        /// <summary>
        /// 检测警告，可以继续但建议处理。
        /// </summary>
        Warning
    }

    /// <summary>
    /// ForgeCLR 单条环境检测结果。
    /// </summary>
    public sealed class ForgeCLRValidationItem
    {
        /// <summary>
        /// 检测项标题。
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 检测项说明。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 检测状态。
        /// </summary>
        public ForgeCLRValidationStatus Status { get; }

        /// <summary>
        /// 创建检测项。
        /// </summary>
        /// <param name="title">检测项标题。</param>
        /// <param name="message">检测项说明。</param>
        /// <param name="status">检测状态。</param>
        public ForgeCLRValidationItem(string title, string message, ForgeCLRValidationStatus status)
        {
            Title = title;
            Message = message;
            Status = status;
        }
    }

    /// <summary>
    /// ForgeCLR 环境检测报告。
    /// </summary>
    public sealed class ForgeCLRValidationReport
    {
        /// <summary>
        /// 全部检测项。
        /// </summary>
        public IReadOnlyList<ForgeCLRValidationItem> Items { get; }

        /// <summary>
        /// 失败数量。
        /// </summary>
        public int FailedCount => Items.Count(item => item.Status == ForgeCLRValidationStatus.Failed);

        /// <summary>
        /// 警告数量。
        /// </summary>
        public int WarningCount => Items.Count(item => item.Status == ForgeCLRValidationStatus.Warning);

        /// <summary>
        /// 是否全部通过且无警告。
        /// </summary>
        public bool IsClean => FailedCount == 0 && WarningCount == 0;

        /// <summary>
        /// 创建检测报告。
        /// </summary>
        /// <param name="items">全部检测项。</param>
        public ForgeCLRValidationReport(IReadOnlyList<ForgeCLRValidationItem> items)
        {
            Items = items;
        }
    }

    /// <summary>
    /// ForgeCLR 快速设置和环境验证工具。
    /// </summary>
    public static class ForgeCLRQuickSetup
    {
        /// <summary>
        /// 快速设置菜单路径。
        /// </summary>
        private const string SetupMenuPath = "VoyageForge/ForgeCLR/快速设置";

        /// <summary>
        /// 配置验证菜单路径。
        /// </summary>
        private const string ValidateMenuPath = "VoyageForge/ForgeCLR/验证环境";

        private const string HotUpdateAssemblyPath = "Assets/HotUpdateAssembly";

        /// <summary>
        /// ForgeCLR 在 YooAssets Collector 中使用的默认分组名称。
        /// </summary>
        private const string ForgeCLRCollectorGroupName = "ForgeCLR";

        /// <summary>
        /// 模板默认场景目录。
        /// </summary>
        private const string DefaultSceneDirectory = "Assets/Scenes";
        
        /// <summary>
        /// 执行 ForgeCLR 快速设置。
        /// </summary>
        [MenuItem(SetupMenuPath)]
        public static void Execute()
        {
            var settings = ForgeCLRSettings.instance;
            CreateDirectory(settings.HotUpdateDllCopyDirectory);
            CreateDirectory(settings.MetadataDllCopyDirectory);

            CheckHotUpdateAssembly();
            CheckHCLRSetting();

            var startupScenePath = EnsureDefaultMainScene();
            ForgeCLRRuntimeSettingsEditorUtility.EnsureYooAssetSettings();
            EnsureYooAssetCollectorConfiguration();
            ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            ForgeCLRRuntimeSettingsEditorUtility.AutoFillRuntimeSettings(null, startupScenePath);
            settings.SaveSettings();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("ForgeCLR 快速设置", "目录和 Project Settings 配置已准备完成。", "确定");
            Debug.Log("[ForgeCLR] 快速设置完成。");
        }

        /// <summary>
        /// 检查 HybridCLR 配置文件。
        /// </summary>
        private static void CheckHCLRSetting()
        {
            
        }

        /// <summary>
        /// 检查并创建热更新程序集目录。
        /// </summary>
        private static void CheckHotUpdateAssembly()
        {
            if (!Directory.Exists(HotUpdateAssemblyPath))
            {
                Directory.CreateDirectory(HotUpdateAssemblyPath);
            }
        }

        /// <summary>
        /// 确保模板默认 Main 场景存在。
        /// </summary>
        /// <returns>Main 场景资源路径。</returns>
        private static string EnsureDefaultMainScene()
        {
            CreateDirectory(DefaultSceneDirectory);
            var scenePath = ForgeCLRRuntimeSettingsEditorUtility.DefaultStartupScenePath;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            {
                return scenePath;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[ForgeCLR] 已创建模板首场景：{scenePath}");
            return scenePath;
        }

        /// <summary>
        /// 确保 YooAssets Collector 中包含 ForgeCLR 的 DLL 和首场景收集配置。
        /// </summary>
        public static void EnsureYooAssetCollectorConfiguration()
        {
            AssetDatabase.Refresh();

            var settings = ForgeCLRSettings.instance;
            CreateDirectory(settings.HotUpdateDllCopyDirectory);
            CreateDirectory(settings.MetadataDllCopyDirectory);
            AssetDatabase.Refresh();

            var collectorSetting = ForgeCLRRuntimeSettingsEditorUtility.EnsureYooAssetCollectorSetting();
            var package = GetOrCreatePackage(collectorSetting, ResolvePackageName());
            var group = GetOrCreateGroup(package, ForgeCLRCollectorGroupName);

            AddOrUpdateCollector(package, group, settings.HotUpdateDllCopyDirectory, nameof(PackDirectory), nameof(CollectAll));
            AddOrUpdateCollector(package, group, settings.MetadataDllCopyDirectory, nameof(PackDirectory), nameof(CollectAll));

            var startupScenePath = ResolveStartupScenePath();
            if (string.IsNullOrWhiteSpace(startupScenePath) == false)
            {
                AddOrUpdateCollector(package, group, startupScenePath, nameof(PackSeparately), nameof(CollectScene));
            }

            AssetBundleCollectorSettingData.SaveFile();
            Debug.Log("[ForgeCLR] YooAssets Collector 配置已检查并补齐。");
        }

        /// <summary>
        /// 获取或创建 YooAssets Package。
        /// </summary>
        /// <param name="setting">YooAssets Collector 设置。</param>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>资源包配置。</returns>
        private static AssetBundleCollectorPackage GetOrCreatePackage(AssetBundleCollectorSetting setting, string packageName)
        {
            var package = setting.Packages.FirstOrDefault(item => item.PackageName == packageName);
            if (package != null)
            {
                return package;
            }

            package = AssetBundleCollectorSettingData.CreatePackage(packageName);
            package.EnableAddressable = false;
            package.SupportExtensionless = false;
            package.AutoCollectShaders = true;
            Debug.Log($"[ForgeCLR] 已创建 YooAssets Package：{packageName}");
            return package;
        }

        /// <summary>
        /// 获取或创建 YooAssets Collector 分组。
        /// </summary>
        /// <param name="package">资源包配置。</param>
        /// <param name="groupName">分组名称。</param>
        /// <returns>分组配置。</returns>
        private static AssetBundleCollectorGroup GetOrCreateGroup(AssetBundleCollectorPackage package, string groupName)
        {
            var group = package.Groups.FirstOrDefault(item => item.GroupName == groupName);
            return group ?? AssetBundleCollectorSettingData.CreateGroup(package, groupName);
        }

        /// <summary>
        /// 添加或更新单条 YooAssets Collector 配置。
        /// </summary>
        /// <param name="package">资源包配置。</param>
        /// <param name="group">目标分组。</param>
        /// <param name="collectPath">收集路径。</param>
        /// <param name="packRuleName">打包规则名称。</param>
        /// <param name="filterRuleName">过滤规则名称。</param>
        private static void AddOrUpdateCollector(
            AssetBundleCollectorPackage package,
            AssetBundleCollectorGroup group,
            string collectPath,
            string packRuleName,
            string filterRuleName)
        {
            if (string.IsNullOrWhiteSpace(collectPath))
            {
                return;
            }

            collectPath = NormalizeAssetPath(collectPath);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(collectPath) == null)
            {
                Debug.LogWarning($"[ForgeCLR] YooAssets Collector 跳过不存在的路径：{collectPath}");
                return;
            }

            var collectorGroup = group;
            var collector = FindCollector(package, collectPath, out var existingGroup);
            if (collector != null)
            {
                collectorGroup = existingGroup;
            }

            if (collector == null)
            {
                collector = new AssetBundleCollector();
                AssetBundleCollectorSettingData.CreateCollector(group, collector);
            }

            collector.CollectPath = collectPath;
            collector.CollectorGUID = AssetDatabase.AssetPathToGUID(collectPath);
            collector.CollectorType = ECollectorType.MainAssetCollector;
            collector.AddressRuleName = nameof(AddressDisable);
            collector.PackRuleName = packRuleName;
            collector.FilterRuleName = filterRuleName;
            collector.AssetTags = string.Empty;
            collector.UserData = string.Empty;
            AssetBundleCollectorSettingData.ModifyCollector(collectorGroup, collector);
        }

        /// <summary>
        /// 在整个 Package 内查找指定路径的收集器，避免快速设置重复添加同一路径。
        /// </summary>
        /// <param name="package">资源包配置。</param>
        /// <param name="collectPath">收集路径。</param>
        /// <param name="group">找到的分组。</param>
        /// <returns>找到的收集器；不存在时返回 null。</returns>
        private static AssetBundleCollector FindCollector(
            AssetBundleCollectorPackage package,
            string collectPath,
            out AssetBundleCollectorGroup group)
        {
            foreach (var candidateGroup in package.Groups)
            {
                var collector = candidateGroup.Collectors.FirstOrDefault(item => NormalizeAssetPath(item.CollectPath) == collectPath);
                if (collector != null)
                {
                    group = candidateGroup;
                    return collector;
                }
            }

            group = null;
            return null;
        }

        /// <summary>
        /// 解析当前应使用的 YooAssets Package 名称。
        /// </summary>
        /// <returns>资源包名称。</returns>
        private static string ResolvePackageName()
        {
            var runtimeSettings = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            var packageNames = ForgeCLRRuntimeSettingsEditorUtility.GetYooAssetPackageNames();
            if (packageNames.Length > 0)
            {
                return packageNames.Contains(runtimeSettings.PackageName)
                    ? runtimeSettings.PackageName
                    : packageNames[0];
            }

            return ForgeCLRRuntimeSettingsEditorUtility.DefaultPackageName;
        }

        /// <summary>
        /// 解析首场景路径。
        /// </summary>
        /// <returns>首场景资源路径。</returns>
        private static string ResolveStartupScenePath()
        {
            var runtimeSettings = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(runtimeSettings.StartupSceneLocation) != null)
            {
                return runtimeSettings.StartupSceneLocation;
            }

            return AssetDatabase.LoadAssetAtPath<SceneAsset>(ForgeCLRRuntimeSettingsEditorUtility.DefaultStartupScenePath) != null
                ? ForgeCLRRuntimeSettingsEditorUtility.DefaultStartupScenePath
                : ForgeCLRRuntimeSettingsEditorUtility.GetAvailableStartupSceneLocations().FirstOrDefault();
        }

        /// <summary>
        /// 验证当前 ForgeCLR 构建环境。
        /// </summary>
        [MenuItem(ValidateMenuPath)]
        public static void ValidateConfiguration()
        {
            ShowValidationDialog(CreateValidationReport());
        }

        /// <summary>
        /// 创建当前 ForgeCLR 环境检测报告。
        /// </summary>
        /// <returns>环境检测报告。</returns>
        public static ForgeCLRValidationReport CreateValidationReport()
        {
            var report = new StringBuilder();
            var items = new List<ForgeCLRValidationItem>();

            AppendCheck(items, File.Exists("Packages/manifest.json"), "Packages Manifest", "Packages/manifest.json 已找到", "未找到 Packages/manifest.json");
            AppendCheck(items, File.Exists("ProjectSettings/HybridCLRSettings.asset"), "HybridCLR Settings", "HybridCLRSettings.asset 已找到", "未找到 HybridCLRSettings.asset");
            AppendCheck(items, ForgeCLRRuntimeSettingsEditorUtility.TryGetYooAssetSettings(out _), "YooAsset Settings", "YooAssetSettings.asset 已在 Resources 下找到", "未找到 Resources/YooAssetSettings.asset，可执行快速设置创建");
            AppendCheck(items, ForgeCLRRuntimeSettingsEditorUtility.TryGetYooAssetCollectorSetting(out var collectorSetting), "YooAssets Collector", "YooAssets Collector 设置已找到", "未找到 YooAssets Collector 设置，可执行快速设置创建");
            AppendCheck(items, Type.GetType("YooAsset.YooAssets, YooAsset") != null, "YooAssets Runtime", "YooAssets Runtime 已安装", "未检测到 YooAssets Runtime");
            AppendCheck(items, Type.GetType("Cysharp.Threading.Tasks.UniTask, UniTask") != null, "UniTask Runtime", "UniTask Runtime 已安装", "未检测到 UniTask Runtime");

            try
            {
                var installer = new InstallerController();
                AppendCheck(items, installer.HasInstalledHybridCLR(), "HybridCLR Installer", "HybridCLR Installer 已完成", "HybridCLR Installer 尚未完成");
            }
            catch (System.Exception e)
            {
                items.Add(new ForgeCLRValidationItem("HybridCLR Installer", $"HybridCLR Installer 检测失败：{e.Message}", ForgeCLRValidationStatus.Failed));
            }

            var settings = ForgeCLRSettings.instance;
            AppendCheck(items, settings.RuntimeSettings != null, "运行时配置 SO", "ForgeCLR Project Settings 已引用运行时 SO", "ForgeCLR Project Settings 未引用运行时 SO，可执行快速设置创建");
            AppendCheck(items, HasRuntimePackageInCollector(settings.RuntimeSettings, collectorSetting), "YooAssets Package", "运行时资源包名称存在于 YooAssets Collector 配置中", "运行时资源包名称未在 YooAssets Collector 配置中找到");
            AppendCheck(items, IsValidFolderName(settings.DllCopyDirectoryName), "DLL 拷贝根目录名", $"DLL 拷贝根目录名有效：{settings.DllCopyDirectoryName}", "DLL 拷贝根目录名不能为空，也不能包含路径分隔符");
            AppendCheck(items, IsAssetPath(settings.HotUpdateDllCopyDirectory), "热更新 DLL 拷贝目录", "热更新 DLL 拷贝目录位于 Assets 下", "热更新 DLL 拷贝目录必须位于 Assets 下");
            AppendCheck(items, IsAssetPath(settings.MetadataDllCopyDirectory), "AOT 元数据 DLL 拷贝目录", "AOT 元数据 DLL 拷贝目录位于 Assets 下", "AOT 元数据 DLL 拷贝目录必须位于 Assets 下");
            AppendCheck(items, HasCollectorForPath(settings.RuntimeSettings, collectorSetting, settings.HotUpdateDllCopyDirectory), "热更新 DLL AB 收集", "热更新 DLL 目录已加入当前 YooAssets 包", "热更新 DLL 目录尚未加入当前 YooAssets 包，可点击修复补齐");
            AppendCheck(items, HasCollectorForPath(settings.RuntimeSettings, collectorSetting, settings.MetadataDllCopyDirectory), "AOT 元数据 DLL AB 收集", "AOT 元数据 DLL 目录已加入当前 YooAssets 包", "AOT 元数据 DLL 目录尚未加入当前 YooAssets 包，可点击修复补齐");

            AppendDirectoryWarning(items, settings.HotUpdateDllCopyDirectory, "热更新 DLL 拷贝目录状态");
            AppendDirectoryWarning(items, settings.MetadataDllCopyDirectory, "AOT 元数据 DLL 拷贝目录状态");

            return new ForgeCLRValidationReport(items);
        }

        /// <summary>
        /// 检查运行时配置中的包名是否存在于 YooAssets Collector 配置。
        /// </summary>
        /// <param name="runtimeSettings">运行时配置。</param>
        /// <param name="collectorSetting">YooAssets Collector 配置。</param>
        /// <returns>包名存在时返回 true。</returns>
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
        /// <param name="runtimeSettings">运行时配置。</param>
        /// <param name="collectorSetting">YooAssets Collector 配置。</param>
        /// <param name="collectPath">收集路径。</param>
        /// <returns>已经被收集时返回 true。</returns>
        private static bool HasCollectorForPath(
            VoyageForge.ForgeCLR.Runtime.ForgeCLRRuntimeSettings runtimeSettings,
            AssetBundleCollectorSetting collectorSetting,
            string collectPath)
        {
            if (runtimeSettings == null || collectorSetting == null || string.IsNullOrWhiteSpace(collectPath))
            {
                return false;
            }

            var package = collectorSetting.Packages?.FirstOrDefault(item => item.PackageName == runtimeSettings.PackageName);
            if (package == null)
            {
                return false;
            }

            var normalizedPath = NormalizeAssetPath(collectPath);
            return package.Groups.Any(group =>
                group.Collectors.Any(collector => NormalizeAssetPath(collector.CollectPath) == normalizedPath));
        }

        /// <summary>
        /// 判断指定环境检测项是否支持自动修复。
        /// </summary>
        /// <param name="title">检测项标题。</param>
        /// <returns>支持自动修复时返回 true。</returns>
        public static bool CanRepairValidationItem(string title)
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
        public static bool TryRepairValidationItem(string title)
        {
            if (CanRepairValidationItem(title) == false)
            {
                return false;
            }

            var settings = ForgeCLRSettings.instance;
            switch (title)
            {
                case "YooAsset Settings":
                    ForgeCLRRuntimeSettingsEditorUtility.EnsureYooAssetSettings();
                    break;
                case "YooAssets Collector":
                    ForgeCLRRuntimeSettingsEditorUtility.EnsureYooAssetCollectorSetting();
                    break;
                case "运行时配置 SO":
                    ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
                    break;
                case "YooAssets Package":
                    EnsureYooAssetCollectorConfiguration();
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
                    EnsureYooAssetCollectorConfiguration();
                    break;
            }

            settings.SaveSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ForgeCLR] 已尝试修复环境检测项：{title}");
            return true;
        }

        /// <summary>
        /// 显示环境检测弹窗。
        /// </summary>
        /// <param name="validationReport">环境检测报告。</param>
        private static void ShowValidationDialog(ForgeCLRValidationReport validationReport)
        {
            var report = new StringBuilder();
            foreach (var item in validationReport.Items)
            {
                report.AppendLine($"{GetStatusText(item.Status)}：{item.Title} - {item.Message}");
            }

            var message = $"ForgeCLR 环境验证\n\n{report}\n错误数量：{validationReport.FailedCount}\n警告数量：{validationReport.WarningCount}";
            if (validationReport.FailedCount > 0)
            {
                EditorUtility.DisplayDialog("ForgeCLR 验证失败", message, "确定");
                Debug.LogError(message);
            }
            else
            {
                EditorUtility.DisplayDialog("ForgeCLR 验证成功", message, "确定");
                Debug.Log(message);
            }
        }

        /// <summary>
        /// 打开 ForgeCLR Project Settings 页面。
        /// </summary>
        [MenuItem("VoyageForge/ForgeCLR/打开配置")]
        public static void OpenConfigurationWindow()
        {
            SettingsService.OpenProjectSettings(ForgeCLRSettingsProvider.SettingsPath);
        }

        /// <summary>
        /// 创建目录。
        /// </summary>
        /// <param name="path">目录路径。</param>
        private static void CreateDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (Directory.Exists(path) == false)
                Directory.CreateDirectory(path);
        }

        /// <summary>
        /// 追加一条通过或失败检测结果。
        /// </summary>
        /// <param name="items">检测项列表。</param>
        /// <param name="success">检测是否通过。</param>
        /// <param name="title">检测项标题。</param>
        /// <param name="successMessage">成功消息。</param>
        /// <param name="errorMessage">失败消息。</param>
        private static void AppendCheck(List<ForgeCLRValidationItem> items, bool success, string title, string successMessage, string errorMessage)
        {
            items.Add(new ForgeCLRValidationItem(
                title,
                success ? successMessage : errorMessage,
                success ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed));
        }

        /// <summary>
        /// 追加目录存在性警告。
        /// </summary>
        /// <param name="items">检测项列表。</param>
        /// <param name="path">待检测目录路径。</param>
        /// <param name="title">检测项标题。</param>
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
        /// 获取检测状态中文文本。
        /// </summary>
        /// <param name="status">检测状态。</param>
        /// <returns>中文状态文本。</returns>
        private static string GetStatusText(ForgeCLRValidationStatus status)
        {
            return status switch
            {
                ForgeCLRValidationStatus.Passed => "通过",
                ForgeCLRValidationStatus.Warning => "警告",
                ForgeCLRValidationStatus.Failed => "错误",
                _ => "未知"
            };
        }

        /// <summary>
        /// 判断路径是否是 Assets 相对路径。
        /// </summary>
        /// <param name="path">待检测路径。</param>
        /// <returns>如果路径位于 Assets 下则返回 true。</returns>
        private static bool IsAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) == false && path.StartsWith("Assets/");
        }

        /// <summary>
        /// 判断目录名称是否是单级目录名。
        /// </summary>
        /// <param name="value">目录名称。</param>
        /// <returns>如果目录名称有效则返回 true。</returns>
        private static bool IsValidFolderName(string value)
        {
            return string.IsNullOrWhiteSpace(value) == false &&
                value.Contains("/") == false &&
                value.Contains("\\") == false;
        }

        /// <summary>
        /// 规范化 Unity 资源路径分隔符。
        /// </summary>
        /// <param name="path">路径。</param>
        /// <returns>使用正斜杠的路径。</returns>
        private static string NormalizeAssetPath(string path)
        {
            return path?.Replace("\\", "/") ?? string.Empty;
        }
    }
}
