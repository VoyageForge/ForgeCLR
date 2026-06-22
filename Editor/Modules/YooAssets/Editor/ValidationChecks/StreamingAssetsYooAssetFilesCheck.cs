using System.IO;
using UnityEditor;
using UnityEngine;
using VoyageForge.ForgeCLR.Runtime;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 YooAssets 内置资源目录中是否包含启动必须的 BuildinCatalog 文件。
    /// YooAssets 初始化顺序：先读取内置 BuildinCatalog（bytes + json），
    /// 解析后得知所有资源包清单和远程下载地址，之后按需拉取 .bundle。
    /// 内置文件路径：Assets/StreamingAssets/{YooFolderName}/{PackageName}/
    /// 其中 YooFolderName 来自 YooAssetSettings.DefaultYooFolderName（默认 "yoo"），
    /// PackageName 来自 ForgeCLRRuntimeSettings。
    /// 此检查不支持自动修复，需通过 YooAsset 构建内置资源。
    /// </summary>
    public sealed class StreamingAssetsYooAssetFilesCheck : IForgeCLRValidationCheck<YooAssetsRuntimeConfigSO>
    {
        public string ModuleId => null;
        public string Title => "StreamingAssets YooAssets 文件";
        public bool CanRepair => false;

        private const string YooAssetSettingsPath = "Assets/Resources/YooAssetSettings.asset";

        /// <summary>
        /// 从 YooAssetSettings.asset 读取 DefaultYooFolderName，默认 "yoo"。
        /// </summary>
        private static string GetYooFolderName()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(YooAssetSettingsPath);
            if (settings == null)
                return "yoo";

            var so = new SerializedObject(settings);
            var prop = so.FindProperty("DefaultYooFolderName");
            return string.IsNullOrWhiteSpace(prop?.stringValue) == false ? prop.stringValue : "yoo";
        }

        /// <summary>
        /// 拼装 YooAssets 内置资源在 StreamingAssets 下的完整路径。
        /// </summary>
        private static string GetBuiltinPackagePath(YooAssetsRuntimeConfigSO config)
        {
            var packageName = config != null ? config.PackageName : "DefaultPackage";
            var yooFolder = GetYooFolderName();
            return $"Assets/StreamingAssets/{yooFolder}/{packageName}";
        }

        /// <summary>
        /// 检查 BuildinCatalog.bytes 和 BuildinCatalog.json 是否都存在。
        /// 路径已知，直接 File.Exists 检查。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context, YooAssetsRuntimeConfigSO config)
        {
            var packagePath = GetBuiltinPackagePath(config);

            if (!Directory.Exists(packagePath))
            {
                return new ForgeCLRValidationItem(Title,
                    $"YooAssets 内置资源目录不存在：{packagePath}，构建内置资源后会自动生成",
                    context.StrictMode ? ForgeCLRValidationStatus.Failed : ForgeCLRValidationStatus.Warning);
            }

            var hasBytes = File.Exists(Path.Combine(packagePath, "BuildinCatalog.bytes"));
            var hasJson = File.Exists(Path.Combine(packagePath, "BuildinCatalog.json"));

            if (!hasBytes && !hasJson)
            {
                return new ForgeCLRValidationItem(Title,
                    $"未找到 BuildinCatalog.bytes / BuildinCatalog.json（目录：{packagePath}），YooAsset 无法初始化内置资源包",
                    context.StrictMode ? ForgeCLRValidationStatus.Failed : ForgeCLRValidationStatus.Warning);
            }

            if (!hasBytes)
            {
                return new ForgeCLRValidationItem(Title,
                    $"缺少 BuildinCatalog.bytes（目录：{packagePath}），YooAsset 无法加载内置资源清单",
                    context.StrictMode ? ForgeCLRValidationStatus.Failed : ForgeCLRValidationStatus.Warning);
            }

            if (!hasJson)
            {
                return new ForgeCLRValidationItem(Title,
                    $"缺少 BuildinCatalog.json（目录：{packagePath}），YooAsset 无法解析资源清单",
                    context.StrictMode ? ForgeCLRValidationStatus.Failed : ForgeCLRValidationStatus.Warning);
            }

            return new ForgeCLRValidationItem(Title,
                $"YooAssets 内置资源目录完整（{packagePath}）",
                ForgeCLRValidationStatus.Passed);
        }

        ForgeCLRValidationItem IForgeCLRValidationCheck.Validate(ForgeCLRValidationContext context)
        {
            var rs = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            return Validate(context, rs.GetModuleConfig<YooAssetsRuntimeConfigSO>());
        }

        public void Repair(ForgeCLRValidationContext context) { }
    }
}
