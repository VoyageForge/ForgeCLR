using System.Linq;
using YooAsset.Editor;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测运行时资源包名称是否已在 YooAsset 收集器配置中存在。
    /// 确保 ForgeCLR 运行时使用的资源包名称与 YooAsset 收集器中配置的资源包对应，
    /// 若不匹配则热更新资源无法按预期加载。
    /// 可通过修复功能自动补齐 YooAsset 收集器配置和运行时设置。
    /// </summary>
    public sealed class YooAssetsPackageCheck : IForgeCLRValidationCheck
    {
        public string Title => "YooAssets Package";
        public bool CanRepair => true;

        /// <summary>
        /// 验证运行时资源包名称是否在 YooAsset 收集器中配置。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var runtimeSettings = context.Settings.RuntimeSettings;
            var collectorSetting = context.CollectorSetting;
            var exists = HasRuntimePackageInCollector(runtimeSettings, collectorSetting);
            return new ForgeCLRValidationItem(Title,
                exists ? "运行时资源包名称存在于 YooAssets Collector 配置中" : "运行时资源包名称未在 YooAssets Collector 配置中找到",
                exists ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        /// <summary>
        /// 自动补齐 YooAsset 收集器配置并完善运行时设置。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            ForgeCLRQuickSetup.EnsureYooAssetCollectorConfiguration();
            ForgeCLRRuntimeSettingsEditorUtility.AutoFillRuntimeSettings();
        }

        /// <summary>
        /// 检查运行时资源包名称是否存在于 YooAsset 收集器配置的包列表中。
        /// </summary>
        private static bool HasRuntimePackageInCollector(
            ForgeCLR.Runtime.ForgeCLRRuntimeSettings runtimeSettings,
            AssetBundleCollectorSetting collectorSetting)
        {
            if (runtimeSettings == null || collectorSetting == null || collectorSetting.Packages == null)
                return false;
            return collectorSetting.Packages.Any(package => package.PackageName == runtimeSettings.PackageName);
        }
    }
}
