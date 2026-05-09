using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// YooAssets 远端资源地址服务。
    /// </summary>
    public class RemoteServices : IRemoteServices
    {
        /// <summary>
        /// 主资源服务器地址。
        /// </summary>
        private readonly string _defaultHostServer;

        /// <summary>
        /// 备用资源服务器地址。
        /// </summary>
        private readonly string _fallbackHostServer;

        /// <summary>
        /// 创建远端资源地址服务。
        /// </summary>
        /// <param name="defaultHostServer">主资源服务器地址。</param>
        /// <param name="fallbackHostServer">备用资源服务器地址。</param>
        public RemoteServices(string defaultHostServer, string fallbackHostServer)
        {
            _defaultHostServer = defaultHostServer;
            _fallbackHostServer = fallbackHostServer;
        }

        /// <summary>
        /// 获取主资源下载地址。
        /// </summary>
        /// <param name="fileName">资源文件名。</param>
        /// <returns>主资源下载地址。</returns>
        string IRemoteServices.GetRemoteMainURL(string fileName)
        {
            return $"{_defaultHostServer}/{fileName}";
        }

        /// <summary>
        /// 获取备用资源下载地址。
        /// </summary>
        /// <param name="fileName">资源文件名。</param>
        /// <returns>备用资源下载地址。</returns>
        string IRemoteServices.GetRemoteFallbackURL(string fileName)
        {
            return $"{_fallbackHostServer}/{fileName}";
        }
    }
}
