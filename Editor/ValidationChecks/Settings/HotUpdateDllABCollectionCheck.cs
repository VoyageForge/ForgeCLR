using System.Linq;
using YooAsset.Editor;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测热更新 DLL 目录是否已被加入 YooAsset 资源收集器的当前资源包中。
    /// 若未加入，热更新 DLL 将不会被 YooAsset 打包，导致热更新功能在运行时无法工作。
    /// 可通过修复功能自动补齐 YooAsset 收集器配置。
    /// </summary>
    public sealed class HotUpdateDllABCollectionCheck : IForgeCLRValidationCheck
    {
        public string Title => "热更新 DLL AB 收集";
        public bool CanRepair => true;

        /// <summary>
        /// 验证热更新 DLL 目录是否在 YooAsset 收集器中有对应的收集规则。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var settings = context.Settings;
            var exists = HasCollectorForPath(settings.RuntimeSettings, context.CollectorSetting,
                settings.HotUpdateDllCopyDirectory);
            return new ForgeCLRValidationItem(Title,
                exists ? "热更新 DLL 目录已加入当前 YooAssets 包" : "热更新 DLL 目录尚未加入当前 YooAssets 包，可点击修复补齐",
                exists ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        /// <summary>
        /// 自动补齐 YooAsset 收集器配置以包含热更新 DLL 目录。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            ForgeCLRQuickSetup.EnsureYooAssetCollectorConfiguration();
        }

        /// <summary>
        /// 检查指定目录是否已在 YooAsset 收集器配置中有对应的收集规则。
        /// </summary>
        internal static bool HasCollectorForPath(
            ForgeCLR.Runtime.ForgeCLRRuntimeSettings runtimeSettings,
            AssetBundleCollectorSetting collectorSetting,
            string collectPath)
        {
            if (runtimeSettings == null || collectorSetting == null || string.IsNullOrWhiteSpace(collectPath))
                return false;
            var package = collectorSetting.Packages?.FirstOrDefault(item => item.PackageName == runtimeSettings.PackageName);
            if (package == null)
                return false;
            var normalizedPath = ForgeCLRValidationHelper.NormalizeAssetPath(collectPath);
            return package.Groups.Any(group =>
                group.Collectors.Any(collector =>
                    ForgeCLRValidationHelper.NormalizeAssetPath(collector.CollectPath) == normalizedPath));
        }
    }
}
