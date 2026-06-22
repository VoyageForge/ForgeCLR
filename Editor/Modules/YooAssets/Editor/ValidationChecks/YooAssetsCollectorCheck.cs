using VoyageForge.ForgeCLR.Runtime;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 YooAsset 资源收集器配置（AssetBundleCollectorSetting）是否存在。
    /// 资源收集器定义了哪些资源需要被打包以及如何打包，是 YooAsset 资源构建的核心配置。
    /// 可通过修复功能自动创建该配置。
    /// </summary>
    public sealed class YooAssetsCollectorCheck : IForgeCLRValidationCheck<YooAssetsRuntimeConfigSO>
    {
        public string ModuleId => null;
        public string Title => "YooAssets Collector";
        public bool CanRepair => true;

        /// <summary>
        /// 验证 YooAsset 资源收集器配置是否存在。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context, YooAssetsRuntimeConfigSO config)
        {
            var exists = ForgeCLRRuntimeSettingsEditorUtility.TryGetYooAssetCollectorSetting(out _);
            return new ForgeCLRValidationItem(Title,
                exists ? "Assets/AssetBundleCollectorSetting.asset 已找到" : "未找到 Assets/AssetBundleCollectorSetting.asset，可执行快速设置创建",
                exists ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        ForgeCLRValidationItem IForgeCLRValidationCheck.Validate(ForgeCLRValidationContext context)
        {
            var rs = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            return Validate(context, rs.GetModuleConfig<YooAssetsRuntimeConfigSO>());
        }

        /// <summary>
        /// 自动创建 YooAsset 资源收集器配置。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            ForgeCLRRuntimeSettingsEditorUtility.EnsureYooAssetCollectorSetting();
        }
    }
}
