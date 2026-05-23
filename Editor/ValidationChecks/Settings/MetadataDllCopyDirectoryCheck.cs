namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 AOT 元数据 DLL 拷贝目录是否位于 Assets 目录下，以及目录是否存在。
    /// AOT 元数据 DLL 是 HybridCLR 解释执行热更新代码时所需的补充元数据，
    /// 需要在构建时被 YooAsset 正确收集，因此必须位于 Assets 目录下。
    /// 可通过修复功能自动创建该目录。
    /// </summary>
    public sealed class MetadataDllCopyDirectoryCheck : IForgeCLRValidationCheck
    {
        public string Title => "AOT 元数据 DLL 拷贝目录";
        public bool CanRepair => true;

        /// <summary>
        /// 验证 AOT 元数据 DLL 拷贝目录路径是否位于 Assets 下。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var path = context.Settings.MetadataDllCopyDirectory;
            var valid = ForgeCLRValidationHelper.IsAssetPath(path);
            return new ForgeCLRValidationItem(Title,
                valid ? "AOT 元数据 DLL 拷贝目录位于 Assets 下" : "AOT 元数据 DLL 拷贝目录必须位于 Assets 下",
                valid ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
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
