using VoyageForge.ForgeCLR.Runtime;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 ForgeCLR 运行时配置 ScriptableObject（RuntimeSettings）是否已被正确引用。
    /// 运行时配置 SO 包含资源包名称、场景路径、DLL 目录等核心热更新参数，
    /// 若未正确引用则项目设置中的配置无法被运行时读取。
    /// 可通过修复功能自动创建并引用运行时配置 SO。
    /// </summary>
    public sealed class RuntimeSettingsSOCheck : IForgeCLRValidationCheck<YooAssetsRuntimeConfigSO>
    {
        public string ModuleId => null;
        public string Title => "运行时配置 SO";
        public bool CanRepair => true;

        /// <summary>
        /// 验证 ForgeCLR Project Settings 中是否已引用运行时配置 SO。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context, YooAssetsRuntimeConfigSO config)
        {
            var exists = context.Settings.RuntimeSettings != null;
            return new ForgeCLRValidationItem(Title,
                exists ? "ForgeCLR Project Settings 已引用运行时 SO" : "ForgeCLR Project Settings 未引用运行时 SO，可执行快速设置创建",
                exists ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        ForgeCLRValidationItem IForgeCLRValidationCheck.Validate(ForgeCLRValidationContext context)
        {
            var rs = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            return Validate(context, rs.GetModuleConfig<YooAssetsRuntimeConfigSO>());
        }

        /// <summary>
        /// 自动创建运行时配置 SO 并关联到 Project Settings。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
        }
    }
}
