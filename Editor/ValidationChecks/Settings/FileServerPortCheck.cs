using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测文件服务器端口是否可用。
    /// 验证端口号是否在有效范围内（1-65535），以及端口是否可被绑定（未被其他进程占用）。
    /// 若当前文件服务器已经在目标端口上运行则直接通过。
    /// 端口不可用时状态为 Warning（非阻断），可通过修复功能自动查找并使用可用端口。
    /// </summary>
    public sealed class FileServerPortCheck : IForgeCLRValidationCheck
    {
        public string Title => "文件服务器端口";
        public bool CanRepair => true;

        /// <summary>
        /// 验证文件服务器端口是否有效且可用。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var settings = context.Settings;
            var port = settings.FileServerPort;
            var validPort = port > 0 && port <= 65535;
            var runningOnPort = VoyageForgeFileServerSingleton.Server != null &&
                                VoyageForgeFileServerSingleton.Server.IsRunning &&
                                VoyageForgeFileServerSingleton.Server.Port == port;
            var portAvailable = runningOnPort || VoyageForgeFileServer.IsPortAvailable(port);
            var ok = validPort && portAvailable;
            return new ForgeCLRValidationItem(Title,
                ok ? $"文件服务器端口可用：{port}" : $"文件服务器端口不可用：{port}",
                ok ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Warning);
        }

        /// <summary>
        /// 自动查找可用端口并更新配置。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            var settings = context.Settings;
            var port = VoyageForgeFileServer.FindAvailablePort(settings.FileServerPort);
            if (port > 0)
            {
                settings.SetFileServerConfig(settings.FileServerRootDirectory, port,
                    settings.FileServerBindIPAddress);
            }
        }
    }
}
