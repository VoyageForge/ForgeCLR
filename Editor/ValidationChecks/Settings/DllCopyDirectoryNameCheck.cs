namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 DLL 拷贝根目录名称是否合法。
    /// 目录名称不能为空，也不能包含路径分隔符（如 / 或 \），
    /// 否则在拷贝热更新 DLL 和 AOT 元数据 DLL 时会导致路径错误。
    /// 可通过修复功能保存设置并触发验证。
    /// </summary>
    public sealed class DllCopyDirectoryNameCheck : IForgeCLRValidationCheck
    {
        public string Title => "DLL 拷贝根目录名";
        public bool CanRepair => true;

        /// <summary>
        /// 验证 DLL 拷贝根目录名称是否为合法的文件夹名。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var settings = context.Settings;
            var valid = ForgeCLRValidationHelper.IsValidFolderName(settings.DllCopyDirectoryName);
            return new ForgeCLRValidationItem(Title,
                valid ? $"DLL 拷贝根目录名有效：{settings.DllCopyDirectoryName}" : "DLL 拷贝根目录名不能为空，也不能包含路径分隔符",
                valid ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        /// <summary>
        /// 保存设置以触发校验刷新。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            context.Settings.SaveSettings();
        }
    }
}
