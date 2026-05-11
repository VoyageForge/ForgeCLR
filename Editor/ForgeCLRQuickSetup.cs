using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HybridCLR.Editor.Installer;
using UnityEditor;
using UnityEngine;

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
            
            ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
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
            AppendCheck(items, File.Exists("Assets/AssetBundleCollectorSetting.asset"), "YooAssets Collector", "YooAssets Collector 设置已找到", "未找到 YooAssets Collector 设置");
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
            AppendCheck(items, IsValidFolderName(settings.DllCopyDirectoryName), "DLL 拷贝根目录名", $"DLL 拷贝根目录名有效：{settings.DllCopyDirectoryName}", "DLL 拷贝根目录名不能为空，也不能包含路径分隔符");
            AppendCheck(items, IsAssetPath(settings.HotUpdateDllCopyDirectory), "热更新 DLL 拷贝目录", "热更新 DLL 拷贝目录位于 Assets 下", "热更新 DLL 拷贝目录必须位于 Assets 下");
            AppendCheck(items, IsAssetPath(settings.MetadataDllCopyDirectory), "AOT 元数据 DLL 拷贝目录", "AOT 元数据 DLL 拷贝目录位于 Assets 下", "AOT 元数据 DLL 拷贝目录必须位于 Assets 下");

            AppendDirectoryWarning(items, settings.HotUpdateDllCopyDirectory, "热更新 DLL 拷贝目录状态");
            AppendDirectoryWarning(items, settings.MetadataDllCopyDirectory, "AOT 元数据 DLL 拷贝目录状态");

            return new ForgeCLRValidationReport(items);
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
    }
}
