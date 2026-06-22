using System.IO;
using VoyageForge.ForgeCLR.Runtime;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 AOT 元数据 DLL 拷贝目录在磁盘上是否存在。
    /// 与 MetadataDllCopyDirectoryCheck（检查路径有效性）互补，
    /// 本检查关注目录的实际存在状态。目录尚不存在时状态为 Warning 而非 Failed，
    /// 因为在执行快速设置或 DLL 拷贝时会自动创建。
    /// 可通过修复功能自动创建该目录。
    /// </summary>
    public sealed class MetadataDllDirectoryStatusCheck : IForgeCLRValidationCheck<HCLRRuntimeConfigSO>
    {
        public string ModuleId => null;
        public string Title => "AOT 元数据 DLL 拷贝目录状态";
        public bool CanRepair => true;

        /// <summary>
        /// 验证 AOT 元数据 DLL 拷贝目录在磁盘上是否实际存在。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context, HCLRRuntimeConfigSO config)
        {
            var path = config?.MetadataDllCopyDirectory ?? "";
            if (!ForgeCLRValidationHelper.IsAssetPath(path))
                return new ForgeCLRValidationItem(Title, $"目录路径无效：{path}", ForgeCLRValidationStatus.Failed);

            var exists = Directory.Exists(path);
            return new ForgeCLRValidationItem(Title,
                exists ? $"目录已存在：{path}" : $"目录尚不存在，执行快速设置或拷贝 DLL 时会创建：{path}",
                exists ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Warning);
        }

        ForgeCLRValidationItem IForgeCLRValidationCheck.Validate(ForgeCLRValidationContext context)
        {
            var rs = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            return Validate(context, rs.GetModuleConfig<HCLRRuntimeConfigSO>());
        }

        /// <summary>
        /// 自动创建 AOT 元数据 DLL 拷贝目录。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            ForgeCLRValidationHelper.CreateDirectory(context.Settings.MetadataDllCopyDirectory);
        }
    }
}
