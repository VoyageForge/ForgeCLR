using System.IO;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测文件服务器根目录是否存在。
    /// 文件服务器用于在开发阶段提供热更新资源的本地下载服务，
    /// 若根目录不存在则资源服务器无法正常启动运行。
    /// 可通过修复功能自动创建该目录。目录不存在时状态为 Warning（非阻断）。
    /// </summary>
    public sealed class FileServerRootDirectoryCheck : IForgeCLRValidationCheck
    {
        public string Title => "文件服务器根目录";
        public bool CanRepair => true;

        /// <summary>
        /// 验证文件服务器根目录是否存在。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var rootDirectory = context.Settings.FileServerRootDirectory;
            var exists = Directory.Exists(rootDirectory);
            return new ForgeCLRValidationItem(Title,
                exists ? $"文件服务器根目录已存在：{rootDirectory}" : $"文件服务器根目录不存在，可点击修复创建：{rootDirectory}",
                exists ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Warning);
        }

        /// <summary>
        /// 自动创建文件服务器根目录。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            ForgeCLRValidationHelper.CreateDirectory(context.Settings.FileServerRootDirectory);
        }
    }
}
