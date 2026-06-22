using System.IO;
using VoyageForge.ForgeCLR.Runtime;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 HybridCLR 设置文件（ProjectSettings/HybridCLRSettings.asset）是否存在。
    /// HybridCLR 是 Unity 的热更新 C# 运行时方案，该设置文件是 HybridCLR 正常运行的前提。
    /// 此检查不支持自动修复，缺失时请通过 HybridCLR 安装器生成。
    /// </summary>
    public sealed class HybridCLRSettingsCheck : IForgeCLRValidationCheck<HCLRRuntimeConfigSO>
    {
        public string ModuleId => null;
        public string Title => "HybridCLR Settings";
        public bool CanRepair => false;

        /// <summary>
        /// 验证 HybridCLRSettings.asset 文件是否存在。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context, HCLRRuntimeConfigSO config)
        {
            var exists = File.Exists("ProjectSettings/HybridCLRSettings.asset");
            return new ForgeCLRValidationItem(Title,
                exists ? "HybridCLRSettings.asset 已找到" : "未找到 HybridCLRSettings.asset",
                exists ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        ForgeCLRValidationItem IForgeCLRValidationCheck.Validate(ForgeCLRValidationContext context)
        {
            var rs = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            return Validate(context, rs.GetModuleConfig<HCLRRuntimeConfigSO>());
        }

        /// <summary>
        /// 不支持自动修复。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context) { }
    }
}
