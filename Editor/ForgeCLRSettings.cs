using UnityEditor;
using UnityEngine;
using VoyageForge.ForgeCLR.Runtime;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// ForgeCLR 编辑器配置，保存 Project Settings 中维护的构建、启动和文件服务器参数。
    /// </summary>
    [FilePath("ProjectSettings/ForgeCLRSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class ForgeCLRSettings : ScriptableSingleton<ForgeCLRSettings>
    {
        /// <summary>
        /// 默认 Launcher 场景路径。
        /// </summary>
        public const string DefaultLauncherScenePath = "Assets/ForgeCLR/Scenes/Launcher.unity";

        /// <summary>
        /// DLL 拷贝根目录名称。
        /// 实际路径固定为 Assets/{dllCopyDirectoryName}/HotUpdateDll 和 Assets/{dllCopyDirectoryName}/MetadataDll。
        /// </summary>
        [SerializeField] private string dllCopyDirectoryName = "HotUpdateDll";

        /// <summary>
        /// 软件包首个启动场景，构建软件包前会自动放到 Build Settings 第一位。
        /// </summary>
        [SerializeField] private string launcherSceneLocation = DefaultLauncherScenePath;

        /// <summary>
        /// 一键构建资源包时要自动填充的 ForgeCLR 运行时配置资产。
        /// </summary>
        [SerializeField] private ForgeCLRRuntimeSettings runtimeSettings;

        /// <summary>
        /// 局域网文件服务器根目录。
        /// </summary>
        [SerializeField] private string fileServerRootDirectory = "";

        /// <summary>
        /// 局域网文件服务器监听端口。
        /// </summary>
        [SerializeField] private int fileServerPort = 8899;

        /// <summary>
        /// 局域网文件服务器绑定 IP；为空时监听所有网卡。
        /// </summary>
        [SerializeField] private string fileServerBindIPAddress = "";

        /// <summary>
        /// 是否在编译或 Play Mode 域重载后自动恢复文件服务器。
        /// </summary>
        [SerializeField] private bool fileServerAutoRestart;

        /// <summary>
        /// StreamingAssets 文件名检查严格模式；开启后中文/特殊字符文件名报错，否则警告。
        /// </summary>
        [SerializeField] private bool streamingAssetsStrictMode;

        /// <summary>
        /// DLL 拷贝根目录名称。
        /// </summary>
        public string DllCopyDirectoryName => NormalizeDllCopyDirectoryName(dllCopyDirectoryName);

        /// <summary>
        /// 软件包首个启动场景路径。
        /// </summary>
        public string LauncherSceneLocation => NormalizeAssetPath(launcherSceneLocation, DefaultLauncherScenePath);

        /// <summary>
        /// 热更新程序集 DLL 拷贝目录。
        /// </summary>
        public string HotUpdateDllCopyDirectory => $"Assets/{DllCopyDirectoryName}/HotUpdateDll";

        /// <summary>
        /// AOT 补充元数据 DLL 拷贝目录。
        /// </summary>
        public string MetadataDllCopyDirectory => $"Assets/{DllCopyDirectoryName}/MetadataDll";

        /// <summary>
        /// 一键构建资源包时要自动填充的 ForgeCLR 运行时配置资产。
        /// </summary>
        public ForgeCLRRuntimeSettings RuntimeSettings => runtimeSettings;

        /// <summary>
        /// 局域网文件服务器根目录。
        /// </summary>
        public string FileServerRootDirectory => NormalizeFileServerRootDirectory(fileServerRootDirectory);

        /// <summary>
        /// 局域网文件服务器监听端口。
        /// </summary>
        public int FileServerPort => Mathf.Clamp(fileServerPort, 1, 65535);

        /// <summary>
        /// 局域网文件服务器绑定 IP；空字符串代表监听所有网卡。
        /// </summary>
        public string FileServerBindIPAddress => fileServerBindIPAddress?.Trim() ?? string.Empty;

        /// <summary>
        /// 是否在域重载后自动恢复文件服务器。
        /// </summary>
        public bool FileServerAutoRestart => fileServerAutoRestart;

        /// <summary>
        /// StreamingAssets 文件名检查严格模式；开启后中文/特殊字符文件名报错，否则警告。
        /// </summary>
        public bool StreamingAssetsStrictMode => streamingAssetsStrictMode;

        /// <summary>
        /// 设置一键构建资源包时要自动填充的运行时配置资产。
        /// </summary>
        /// <param name="settings">运行时配置资产。</param>
        public void SetRuntimeSettings(ForgeCLRRuntimeSettings settings)
        {
            runtimeSettings = settings;
            SaveSettings();
        }

        /// <summary>
        /// 设置软件包首个启动场景。
        /// </summary>
        /// <param name="sceneLocation">场景资源路径。</param>
        public void SetLauncherSceneLocation(string sceneLocation)
        {
            launcherSceneLocation = NormalizeAssetPath(sceneLocation, DefaultLauncherScenePath);
            SaveSettings();
        }

        /// <summary>
        /// 设置文件服务器配置。
        /// </summary>
        /// <param name="rootDirectory">文件服务器根目录。</param>
        /// <param name="port">监听端口。</param>
        /// <param name="bindIPAddress">绑定 IP；为空时监听所有网卡。</param>
        public void SetFileServerConfig(string rootDirectory, int port, string bindIPAddress)
        {
            fileServerRootDirectory = rootDirectory ?? string.Empty;
            fileServerPort = Mathf.Clamp(port, 1, 65535);
            fileServerBindIPAddress = bindIPAddress?.Trim() ?? string.Empty;
            SaveSettings();
        }

        /// <summary>
        /// 设置 StreamingAssets 文件名检查严格模式。
        /// </summary>
        /// <param name="enabled">开启后中文/特殊字符文件名报错，否则警告。</param>
        public void SetStreamingAssetsStrictMode(bool enabled)
        {
            streamingAssetsStrictMode = enabled;
            SaveSettings();
        }

        /// <summary>
        /// 设置文件服务器自动恢复开关。
        /// </summary>
        /// <param name="enabled">是否启用自动恢复。</param>
        public void SetFileServerAutoRestart(bool enabled)
        {
            fileServerAutoRestart = enabled;
            SaveSettings();
        }

        /// <summary>
        /// 保存 ForgeCLR 编辑器配置到 ProjectSettings。
        /// </summary>
        public void SaveSettings()
        {
            dllCopyDirectoryName = NormalizeDllCopyDirectoryName(dllCopyDirectoryName);
            launcherSceneLocation = NormalizeAssetPath(launcherSceneLocation, DefaultLauncherScenePath);
            fileServerRootDirectory = NormalizeFileServerRootDirectory(fileServerRootDirectory);
            fileServerPort = Mathf.Clamp(fileServerPort, 1, 65535);
            fileServerBindIPAddress = fileServerBindIPAddress?.Trim() ?? string.Empty;
            Save(true);
        }

        /// <summary>
        /// 规范化 DLL 拷贝根目录名称。
        /// </summary>
        /// <param name="value">用户输入的目录名称。</param>
        /// <returns>可用于 Assets 下一级目录的名称。</returns>
        private static string NormalizeDllCopyDirectoryName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "HotUpdateDll";
            }

            string normalized = value.Trim().Replace("\\", "/").Trim('/');
            int slashIndex = normalized.LastIndexOf('/');
            if (slashIndex >= 0)
            {
                normalized = normalized[(slashIndex + 1)..];
            }

            return string.IsNullOrWhiteSpace(normalized) ? "HotUpdateDll" : normalized;
        }

        /// <summary>
        /// 规范化 Unity 资源路径。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="fallback">路径为空时使用的回退值。</param>
        /// <returns>使用正斜杠的资源路径。</returns>
        private static string NormalizeAssetPath(string path, string fallback)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return fallback;
            }

            return path.Trim().Replace("\\", "/");
        }

        /// <summary>
        /// 规范化文件服务器根目录。
        /// </summary>
        /// <param name="path">根目录路径。</param>
        /// <returns>可用于本机文件系统的目录路径。</returns>
        private static string NormalizeFileServerRootDirectory(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Bundles"))
                : path.Trim();
        }
    }
}
