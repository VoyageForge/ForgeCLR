namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测热更新 DLL 拷贝目录是否位于 Assets 目录下，以及目录是否存在。
    /// 热更新 DLL 需要被拷贝到 Assets 下的指定目录才能被 Unity 识别并参与 AssetBundle 打包，
    /// 若目录不在 Assets 下则无法被 YooAsset 资源收集器正确收集。
    /// 可通过修复功能自动创建该目录。
    /// </summary>
    public sealed class HotUpdateDllCopyDirectoryCheck : IForgeCLRValidationCheck
    {
        public string Title => "热更新 DLL 拷贝目录";
        public bool CanRepair => true;

        /// <summary>
        /// 验证热更新 DLL 拷贝目录路径是否位于 Assets 下。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var path = context.Settings.HotUpdateDllCopyDirectory;
            var valid = ForgeCLRValidationHelper.IsAssetPath(path);
            return new ForgeCLRValidationItem(Title,
                valid ? "热更新 DLL 拷贝目录位于 Assets 下" : "热更新 DLL 拷贝目录必须位于 Assets 下",
                valid ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        /// <summary>
        /// 自动创建热更新 DLL 拷贝目录。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            ForgeCLRValidationHelper.CreateDirectory(context.Settings.HotUpdateDllCopyDirectory);
        }
    }
}
