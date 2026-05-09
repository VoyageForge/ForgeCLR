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
        private const string RuntimeSettingsDirectory = "Assets/ForgeCLR/Resources";

        /// <summary>
        /// 默认运行时配置资产路径。
        /// </summary>
        private const string RuntimeSettingsPath = "Assets/ForgeCLR/Resources/ForgeCLRRuntimeSettings.asset";

        /// <summary>
        /// 获取 Project Settings 中配置的运行时配置资产；未配置时创建默认资产并写回配置。
        /// </summary>
        /// <returns>运行时配置资产。</returns>
        public static ForgeCLRRuntimeSettings EnsureRuntimeSettingsAsset()
        {
            var editorSettings = ForgeCLRSettings.instance;
            if (editorSettings.RuntimeSettings != null)
                return editorSettings.RuntimeSettings;

            var runtimeSettings = AssetDatabase.LoadAssetAtPath<ForgeCLRRuntimeSettings>(RuntimeSettingsPath);
            if (runtimeSettings == null)
            {
                if (Directory.Exists(RuntimeSettingsDirectory) == false)
                    Directory.CreateDirectory(RuntimeSettingsDirectory);

                runtimeSettings = ScriptableObject.CreateInstance<ForgeCLRRuntimeSettings>();
                AssetDatabase.CreateAsset(runtimeSettings, RuntimeSettingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ForgeCLR] 已创建运行时配置：{RuntimeSettingsPath}");
            }

            editorSettings.SetRuntimeSettings(runtimeSettings);
            return runtimeSettings;
        }

        /// <summary>
        /// 从当前项目配置和 DLL 拷贝结果自动填充运行时配置资产。
        /// </summary>
        /// <returns>被填充的运行时配置资产。</returns>
        public static ForgeCLRRuntimeSettings AutoFillRuntimeSettings()
        {
            var runtimeSettings = EnsureRuntimeSettingsAsset();
            var editorSettings = ForgeCLRSettings.instance;
            var serializedObject = new SerializedObject(runtimeSettings);

            var packageNameProperty = serializedObject.FindProperty("packageName");
            packageNameProperty.stringValue = ResolvePackageName(packageNameProperty.stringValue);

            FillStringArray(
                serializedObject.FindProperty("hotUpdateDllLocations"),
                CollectDllLocations(editorSettings.HotUpdateDllCopyDirectory));

            FillStringArray(
                serializedObject.FindProperty("aotMetadataDllLocations"),
                CollectDllLocations(editorSettings.MetadataDllCopyDirectory));

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
            var packages = AssetBundleCollectorSettingData.Setting?.Packages;
            if (packages == null || packages.Count == 0)
                return currentPackageName;

            if (string.IsNullOrWhiteSpace(currentPackageName) == false &&
                packages.Any(package => package.PackageName == currentPackageName))
            {
                return currentPackageName;
            }

            var firstPackageName = packages.FirstOrDefault(package => string.IsNullOrWhiteSpace(package.PackageName) == false)?.PackageName;
            return string.IsNullOrWhiteSpace(firstPackageName) ? currentPackageName : firstPackageName;
        }

        /// <summary>
        /// 扫描 DLL 拷贝目录中的 .dll.bytes 文件，并转换为 YooAssets 加载地址。
        /// </summary>
        /// <param name="assetDirectory">Assets 下的 DLL 拷贝目录。</param>
        /// <returns>DLL 加载地址集合。</returns>
        private static string[] CollectDllLocations(string assetDirectory)
        {
            if (string.IsNullOrWhiteSpace(assetDirectory) || assetDirectory.StartsWith("Assets/", StringComparison.Ordinal) == false)
                return Array.Empty<string>();

            var absoluteDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetDirectory));
            if (Directory.Exists(absoluteDirectory) == false)
                return Array.Empty<string>();

            return Directory.GetFiles(absoluteDirectory, "*.dll.bytes", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(fileName => string.IsNullOrWhiteSpace(fileName) == false)
                .OrderBy(fileName => fileName, StringComparer.Ordinal)
                .ToArray();
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
