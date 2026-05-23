namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 YooAsset 全局设置文件（Resources/YooAssetSettings.asset）是否存在。
    /// YooAsset 是项目使用的资源管理框架，该设置文件为其核心配置，若缺失则资源加载功能无法工作。
    /// 可通过修复功能自动创建该设置文件。
    /// </summary>
    public sealed class YooAssetSettingsCheck : IForgeCLRValidationCheck
    {
        public string Title => "YooAsset Settings";
        public bool CanRepair => true;

        /// <summary>
        /// 验证 YooAsset 全局设置文件是否存在。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var exists = context.HasYooAssetSettings;
            return new ForgeCLRValidationItem(Title,
                exists ? "YooAssetSettings.asset 已在 Resources 下找到" : "未找到 Resources/YooAssetSettings.asset，可执行快速设置创建",
                exists ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        /// <summary>
        /// 自动创建 YooAsset 全局设置文件。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            ForgeCLRRuntimeSettingsEditorUtility.EnsureYooAssetSettings();
        }
    }
}
