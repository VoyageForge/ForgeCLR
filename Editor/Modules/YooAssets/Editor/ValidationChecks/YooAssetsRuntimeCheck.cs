using System;
using VoyageForge.ForgeCLR.Runtime;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 YooAsset 运行时库（YooAsset.YooAssets 类型）是否已安装。
    /// 通过反射检查 YooAsset 运行时程序集是否可被加载，确保 YooAsset 包已被正确引入项目。
    /// 此检查不支持自动修复，缺失时请通过 Package Manager 安装 YooAsset 包。
    /// </summary>
    public sealed class YooAssetsRuntimeCheck : IForgeCLRValidationCheck<YooAssetsRuntimeConfigSO>
    {
        public string ModuleId => null;
        public string Title => "YooAssets Runtime";
        public bool CanRepair => false;

        /// <summary>
        /// 通过反射检测 YooAsset 运行时程序集是否可用。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context, YooAssetsRuntimeConfigSO config)
        {
            var installed = Type.GetType("YooAsset.YooAssets, YooAsset") != null;
            return new ForgeCLRValidationItem(Title,
                installed ? "YooAssets Runtime 已安装" : "未检测到 YooAssets Runtime",
                installed ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        ForgeCLRValidationItem IForgeCLRValidationCheck.Validate(ForgeCLRValidationContext context)
        {
            var rs = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            return Validate(context, rs.GetModuleConfig<YooAssetsRuntimeConfigSO>());
        }

        /// <summary>
        /// 不支持自动修复。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context) { }
    }
}
