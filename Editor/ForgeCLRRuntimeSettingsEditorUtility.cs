using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VoyageForge.ForgeCLR.Runtime;
using YooAsset.Editor;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// ForgeCLR 运行时配置资产的编辑器工具。
    /// </summary>
    public static class ForgeCLRRuntimeSettingsEditorUtility
    {
        /// <summary>
        /// 默认运行时配置资产目录。
        /// </summary>
        private const string RuntimeSettingsDirectory = "Assets/Resources/VoyageForge/Config";

        /// <summary>
        /// 默认运行时配置资产路径。
        /// </summary>
        private const string RuntimeSettingsPath = RuntimeSettingsDirectory + "/ForgeCLRRuntimeSettings.asset";

        /// <summary>
        /// 模板默认首场景路径。
        /// </summary>
        public const string DefaultStartupScenePath = "Assets/Scenes/Main.unity";

        /// <summary>
        /// 模板默认 YooAssets 资源包名称。
        /// </summary>
        public const string DefaultPackageName = "DefaultPackage";

        /// <summary>
        /// YooAssets Collector 配置资产路径（仅编辑器使用，放在 Assets 下避免打包进 Resources）。
        /// </summary>
        private const string YooAssetCollectorSettingPath = "Assets/AssetBundleCollectorSetting.asset";

        /// <summary>
        /// YooAssets Settings 配置资产路径（运行时需从 Resources 加载）。
        /// </summary>
        private const string YooAssetSettingsPath = "Assets/Resources/YooAssetSettings.asset";

        /// <summary>
        /// YooAssets Settings 类型完整名称。
        /// </summary>
        private const string YooAssetSettingsTypeName = "YooAsset.YooAssetSettings, YooAsset";

        /// <summary>
        /// 获取 Project Settings 中配置的运行时配置资产；未配置时创建默认资产并写回配置。
        /// </summary>
        /// <returns>运行时配置资产。</returns>
        public static ForgeCLRRuntimeSettings EnsureRuntimeSettingsAsset()
        {
            // 直接从磁盘查找，不经过 ForgeCLRSettings.RuntimeSettings（避免循环调用）
            var runtimeSettings = FindRuntimeSettingsAsset();
            if (runtimeSettings != null)
                return runtimeSettings;

            if (Directory.Exists(RuntimeSettingsDirectory) == false)
            {
                Directory.CreateDirectory(RuntimeSettingsDirectory);
                AssetDatabase.Refresh();
            }

            runtimeSettings = ScriptableObject.CreateInstance<ForgeCLRRuntimeSettings>();
            AssetDatabase.CreateAsset(runtimeSettings, RuntimeSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ForgeCLR] 已创建运行时配置：{RuntimeSettingsPath}");
            return runtimeSettings;
        }

        /// <summary>
        /// 全项目搜索已有 ForgeCLR 运行时配置资产。
        /// </summary>
        /// <returns>找到的运行时配置资产；不存在时返回 null。</returns>
        private static ForgeCLRRuntimeSettings FindRuntimeSettingsAsset()
        {
            var runtimeSettings = AssetDatabase.LoadAssetAtPath<ForgeCLRRuntimeSettings>(RuntimeSettingsPath);
            if (runtimeSettings != null)
                return runtimeSettings;

            string[] guids = AssetDatabase.FindAssets($"t:{nameof(ForgeCLRRuntimeSettings)}");
            if (guids == null || guids.Length == 0)
                return null;

            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<ForgeCLRRuntimeSettings>(assetPath);
        }

        /// <summary>
        /// 从当前项目配置和 DLL 拷贝结果自动填充运行时配置资产。
        /// </summary>
        /// <returns>被填充的运行时配置资产。</returns>
        public static ForgeCLRRuntimeSettings AutoFillRuntimeSettings(
            CopyHotUpdateDllToFolder.CopyResult copyResult = null,
            string startupSceneLocation = null)
        {
            var runtimeSettings = EnsureRuntimeSettingsAsset();
            var editorSettings = ForgeCLRSettings.instance;
            var serializedObject = new SerializedObject(runtimeSettings);

            var packageNameProperty = serializedObject.FindProperty("packageName");
            packageNameProperty.stringValue = ResolvePackageName(packageNameProperty.stringValue);

            FillStringArray(
                serializedObject.FindProperty("hotUpdateDllLocations"),
                CollectDllLocations(editorSettings.HotUpdateDllCopyDirectory, copyResult));

            FillStringArray(
                serializedObject.FindProperty("aotMetadataDllLocations"),
                CollectDllLocations(editorSettings.MetadataDllCopyDirectory, copyResult));

            var startupSceneProperty = serializedObject.FindProperty("startupSceneLocation");
            startupSceneProperty.stringValue = ResolveStartupSceneLocation(startupSceneLocation ?? startupSceneProperty.stringValue);

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runtimeSettings);
            AssetDatabase.SaveAssets();

            Debug.Log("[ForgeCLR] 已自动填充 ForgeCLRRuntimeSettings。");
            return runtimeSettings;
        }

        /// <summary>
        /// 从 YooAssets Collector 推断运行时使用的资源包名称。
        /// </summary>
        /// <param name="currentPackageName">当前配置中的资源包名称。</param>
        /// <returns>推断后的资源包名称。</returns>
        private static string ResolvePackageName(string currentPackageName)
        {
            var packageNames = GetYooAssetPackageNames();
            if (packageNames.Length == 0)
            {
                return string.IsNullOrWhiteSpace(currentPackageName) ? DefaultPackageName : currentPackageName;
            }

            if (string.IsNullOrWhiteSpace(currentPackageName) == false &&
                packageNames.Contains(currentPackageName))
            {
                return currentPackageName;
            }

            return packageNames[0];
        }

        /// <summary>
        /// 从项目中查找 YooAssets Collector 配置资产。
        /// 优先检查默认路径，其次全局搜索类型（仅限 Assets 下，排除 Resources）。
        /// </summary>
        /// <param name="setting">找到的配置资产。</param>
        /// <returns>找到配置资产时返回 true。</returns>
        public static bool TryGetYooAssetCollectorSetting(out AssetBundleCollectorSetting setting)
        {
            setting = AssetDatabase.LoadAssetAtPath<AssetBundleCollectorSetting>(YooAssetCollectorSettingPath);
            if (setting != null)
                return true;

            var guids = AssetDatabase.FindAssets("t:AssetBundleCollectorSetting");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/") && path.Contains("/Resources/") == false)
                {
                    setting = AssetDatabase.LoadAssetAtPath<AssetBundleCollectorSetting>(path);
                    if (setting != null)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 确保项目中存在 YooAssets Collector 配置资产；一键配置会调用该方法创建默认资产。
        /// </summary>
        /// <returns>YooAssets Collector 配置资产。</returns>
        public static AssetBundleCollectorSetting EnsureYooAssetCollectorSetting()
        {
            if (TryGetYooAssetCollectorSetting(out var setting))
            {
                return setting;
            }

            var directory = Path.GetDirectoryName(YooAssetCollectorSettingPath);
            if (Directory.Exists(directory) == false)
                Directory.CreateDirectory(directory);

            setting = ScriptableObject.CreateInstance<AssetBundleCollectorSetting>();
            AssetDatabase.CreateAsset(setting, YooAssetCollectorSettingPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ForgeCLR] 已创建 YooAssets Collector 配置：{YooAssetCollectorSettingPath}");
            return setting;
        }

        /// <summary>
        /// 从项目中查找 YooAssetSettings 配置资产。
        /// 优先检查默认路径，其次全局搜索类型（必须在 Resources 下，运行时从 Resources 加载）。
        /// </summary>
        /// <param name="setting">找到的配置资产。</param>
        /// <returns>找到配置资产时返回 true。</returns>
        public static bool TryGetYooAssetSettings(out ScriptableObject setting)
        {
            setting = AssetDatabase.LoadAssetAtPath<ScriptableObject>(YooAssetSettingsPath);
            if (setting != null && setting.GetType().FullName == "YooAsset.YooAssetSettings")
                return true;

            var guids = AssetDatabase.FindAssets("t:ScriptableObject");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/Resources/"))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (obj != null && obj.GetType().FullName == "YooAsset.YooAssetSettings")
                    {
                        setting = obj;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 确保项目中存在 YooAssetSettings 配置资产；资源运行时会从 Resources 中加载该配置。
        /// </summary>
        /// <returns>YooAssetSettings 配置资产。</returns>
        public static ScriptableObject EnsureYooAssetSettings()
        {
            if (TryGetYooAssetSettings(out var setting))
            {
                return setting;
            }

            var settingsType = Type.GetType(YooAssetSettingsTypeName);
            if (settingsType == null)
            {
                throw new InvalidOperationException("未找到 YooAsset.YooAssetSettings 类型，请确认 YooAssets 已安装。");
            }

            EnsureResourcesDirectory();
            setting = ScriptableObject.CreateInstance(settingsType);
            AssetDatabase.CreateAsset(setting, YooAssetSettingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ForgeCLR] 已创建 YooAssetSettings 配置：{YooAssetSettingsPath}");
            return setting;
        }

        /// <summary>
        /// 从 YooAssets Collector 配置中读取所有 Package 名称。
        /// </summary>
        /// <returns>Package 名称集合。</returns>
        public static string[] GetYooAssetPackageNames()
        {
            if (TryGetYooAssetCollectorSetting(out var setting) == false)
            {
                return Array.Empty<string>();
            }

            var packages = setting.Packages;
            if (packages == null || packages.Count == 0)
            {
                return Array.Empty<string>();
            }

            return packages
                .Select(package => package.PackageName)
                .Where(packageName => string.IsNullOrWhiteSpace(packageName) == false)
                .Distinct()
                .ToArray();
        }

        /// <summary>
        /// 扫描 DLL 拷贝目录中的 .dll.bytes 文件，并转换为 YooAssets 加载地址。
        /// </summary>
        /// <param name="assetDirectory">Assets 下的 DLL 拷贝目录。</param>
        /// <param name="copyResult">本次 DLL 拷贝结果；存在时优先使用本次拷贝出的资源路径。</param>
        /// <returns>DLL 加载地址集合。</returns>
        private static string[] CollectDllLocations(string assetDirectory, CopyHotUpdateDllToFolder.CopyResult copyResult)
        {
            if (string.IsNullOrWhiteSpace(assetDirectory) || assetDirectory.StartsWith("Assets/", StringComparison.Ordinal) == false)
                return Array.Empty<string>();

            if (copyResult != null)
            {
                var normalizedDirectory = NormalizeAssetPath(assetDirectory).TrimEnd('/') + "/";
                var copiedLocations = copyResult.CopiedAssetFiles
                    .Select(NormalizeAssetPath)
                    .Where(path => path.StartsWith(normalizedDirectory, StringComparison.Ordinal))
                    .Where(path => path.EndsWith(".dll.bytes", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                if (copiedLocations.Length > 0)
                    return copiedLocations;
            }

            var absoluteDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetDirectory));
            if (Directory.Exists(absoluteDirectory) == false)
                return Array.Empty<string>();

            return Directory.GetFiles(absoluteDirectory, "*.dll.bytes", SearchOption.TopDirectoryOnly)
                .Select(path => NormalizeAssetPath(Path.Combine(assetDirectory, Path.GetFileName(path))))
                .Where(path => string.IsNullOrWhiteSpace(path) == false)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 获取可作为首场景的项目场景路径。
        /// </summary>
        /// <returns>场景资源路径集合。</returns>
        public static string[] GetAvailableStartupSceneLocations()
        {
            return AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => string.IsNullOrWhiteSpace(path) == false)
                .Where(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path == DefaultStartupScenePath ? 0 : 1)
                .ThenBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 解析首场景路径；当前配置无效时优先使用模板 Main 场景，其次使用项目中的第一个场景。
        /// </summary>
        /// <param name="currentLocation">当前场景路径。</param>
        /// <returns>有效的场景资源路径。</returns>
        private static string ResolveStartupSceneLocation(string currentLocation)
        {
            var locations = GetAvailableStartupSceneLocations();
            if (string.IsNullOrWhiteSpace(currentLocation) == false &&
                locations.Contains(currentLocation))
            {
                return currentLocation;
            }

            if (locations.Contains(DefaultStartupScenePath))
            {
                return DefaultStartupScenePath;
            }

            return locations.FirstOrDefault() ?? DefaultStartupScenePath;
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

        /// <summary>
        /// 确保 Assets/Resources 目录存在。
        /// </summary>
        private static void EnsureResourcesDirectory()
        {
            if (Directory.Exists("Assets/Resources") == false)
            {
                Directory.CreateDirectory("Assets/Resources");
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// 填充字符串数组序列化属性。
        /// </summary>
        /// <param name="property">字符串数组属性。</param>
        /// <param name="values">要写入的值集合。</param>
        private static void FillStringArray(SerializedProperty property, string[] values)
        {
            property.ClearArray();
            for (var index = 0; index < values.Length; index++)
            {
                property.InsertArrayElementAtIndex(index);
                property.GetArrayElementAtIndex(index).stringValue = values[index];
            }
        }
    }
}
