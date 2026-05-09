using System.IO;
using System.Text;
using HybridCLR.Editor.Installer;
using UnityEditor;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
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

        /// <summary>
        /// 执行 ForgeCLR 快速设置。
        /// </summary>
        [MenuItem(SetupMenuPath)]
        public static void Execute()
        {
            var settings = ForgeCLRSettings.instance;
            CreateDirectory(settings.HotUpdateDllCopyDirectory);
            CreateDirectory(settings.MetadataDllCopyDirectory);
            ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            settings.SaveSettings();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("ForgeCLR 快速设置", "目录和 Project Settings 配置已准备完成。", "确定");
            Debug.Log("[ForgeCLR] 快速设置完成。");
        }

        /// <summary>
        /// 验证当前 ForgeCLR 构建环境。
        /// </summary>
        [MenuItem(ValidateMenuPath)]
        public static void ValidateConfiguration()
        {
            var report = new StringBuilder();
            var errors = 0;

            AppendCheck(report, File.Exists("Packages/manifest.json"), "Packages/manifest.json 已找到", "未找到 Packages/manifest.json", ref errors);
            AppendCheck(report, File.Exists("ProjectSettings/HybridCLRSettings.asset"), "HybridCLRSettings.asset 已找到", "未找到 HybridCLRSettings.asset", ref errors);
            AppendCheck(report, File.Exists("Assets/AssetBundleCollectorSetting.asset"), "YooAssets Collector 设置已找到", "未找到 YooAssets Collector 设置", ref errors);

            try
            {
                var installer = new InstallerController();
                AppendCheck(report, installer.HasInstalledHybridCLR(), "HybridCLR Installer 已完成", "HybridCLR Installer 尚未完成", ref errors);
            }
            catch (System.Exception e)
            {
                report.AppendLine($"HybridCLR Installer 检测失败：{e.Message}");
                errors++;
            }

            var settings = ForgeCLRSettings.instance;
            AppendCheck(report, settings.RuntimeSettings != null, "ForgeCLR Project Settings 已引用运行时 SO", "ForgeCLR Project Settings 未引用运行时 SO，可执行快速设置创建", ref errors);
            AppendCheck(report, IsAssetPath(settings.HotUpdateDllCopyDirectory), "热更新 DLL 拷贝目录位于 Assets 下", "热更新 DLL 拷贝目录必须位于 Assets 下", ref errors);
            AppendCheck(report, IsAssetPath(settings.MetadataDllCopyDirectory), "AOT 元数据 DLL 拷贝目录位于 Assets 下", "AOT 元数据 DLL 拷贝目录必须位于 Assets 下", ref errors);

            var message = $"ForgeCLR 环境验证\n\n{report}\n错误数量：{errors}";
            if (errors > 0)
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
        /// 追加一条验证结果。
        /// </summary>
        /// <param name="report">验证报告。</param>
        /// <param name="success">验证是否通过。</param>
        /// <param name="successMessage">成功消息。</param>
        /// <param name="errorMessage">失败消息。</param>
        /// <param name="errors">错误数量。</param>
        private static void AppendCheck(StringBuilder report, bool success, string successMessage, string errorMessage, ref int errors)
        {
            if (success)
            {
                report.AppendLine($"通过：{successMessage}");
                return;
            }

            report.AppendLine($"错误：{errorMessage}");
            errors++;
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
    }
}
