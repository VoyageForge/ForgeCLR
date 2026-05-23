using System.IO;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 Packages/manifest.json 文件是否存在。
    /// 该文件是 Unity 项目的包清单文件，若缺失则项目无法正常编译运行。
    /// 此检查不支持自动修复，缺失时需用户自行处理。
    /// </summary>
    public sealed class PackagesManifestCheck : IForgeCLRValidationCheck
    {
        public string Title => "Packages Manifest";
        public bool CanRepair => false;

        /// <summary>
        /// 验证 Packages/manifest.json 文件是否存在。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var exists = File.Exists("Packages/manifest.json");
            return new ForgeCLRValidationItem(Title,
                exists ? "Packages/manifest.json 已找到" : "未找到 Packages/manifest.json",
                exists ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        /// <summary>
        /// 不支持自动修复。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context) { }
    }
}
