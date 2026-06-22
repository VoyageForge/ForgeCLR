using UnityEditor;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// ForgeCLR 编辑器配置（ProjectSettings），只存 Core 字段。
    /// 模块配置由 ForgeCLRRuntimeSettings（Resources）管理。
    /// </summary>
    [FilePath("ProjectSettings/ForgeCLRSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class ForgeCLRSettings : ScriptableSingleton<ForgeCLRSettings>
    {
        public const string DefaultLauncherScenePath = "Assets/ForgeCLR/Scenes/Launcher.unity";

        [SerializeField] private string launcherSceneLocation = DefaultLauncherScenePath;

        [SerializeField] private string fileServerRootDirectory = "";
        [SerializeField] private int fileServerPort = 8899;
        [SerializeField] private string fileServerBindIPAddress = "";
        [SerializeField] private bool fileServerAutoRestart;
        [SerializeField] private bool streamingAssetsStrictMode;

        // ===== Getters =====

        public string LauncherSceneLocation
        {
            get
            {
                var rs = RuntimeSettings;
                return NormalizeAssetPath(
                    rs?.YooAssetsConfig?.LauncherSceneLocation ?? launcherSceneLocation,
                    DefaultLauncherScenePath);
            }
        }
        public string FileServerRootDirectory => NormalizeFileServerRootDirectory(fileServerRootDirectory);
        public int FileServerPort => Mathf.Clamp(fileServerPort, 1, 65535);
        public string FileServerBindIPAddress => fileServerBindIPAddress?.Trim() ?? "";
        public bool FileServerAutoRestart => fileServerAutoRestart;
        public bool StreamingAssetsStrictMode => streamingAssetsStrictMode;

        // ===== Setters =====

        public void SetLauncherSceneLocation(string path)
        {
            launcherSceneLocation = NormalizeAssetPath(path, DefaultLauncherScenePath);
            SaveSettings();
        }

        public void SetFileServerConfig(string root, int port, string bindIPAddress)
        {
            fileServerRootDirectory = root ?? "";
            fileServerPort = Mathf.Clamp(port, 1, 65535);
            fileServerBindIPAddress = bindIPAddress?.Trim() ?? "";
            SaveSettings();
        }

        public void SetStreamingAssetsStrictMode(bool enabled)
        {
            streamingAssetsStrictMode = enabled;
            SaveSettings();
        }

        public void SetFileServerAutoRestart(bool enabled)
        {
            fileServerAutoRestart = enabled;
            SaveSettings();
        }

        // ===== 向后兼容属性（委托到 RuntimeSettings 的子 SO） =====

        public ForgeCLR.Runtime.ForgeCLRRuntimeSettings RuntimeSettings
        {
            get
            {
                var rs = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
                return rs;
            }
        }

        public string DllCopyDirectoryName
        {
            get
            {
                var rs = RuntimeSettings;
                return rs?.HCLRConfig?.DllCopyDirectoryName ?? "HotUpdateDll";
            }
        }

        public string HotUpdateDllCopyDirectory
        {
            get
            {
                var rs = RuntimeSettings;
                return rs?.HCLRConfig?.HotUpdateDllCopyDirectory
                    ?? $"Assets/{DllCopyDirectoryName}/HotUpdateDll";
            }
        }

        public string MetadataDllCopyDirectory
        {
            get
            {
                var rs = RuntimeSettings;
                return rs?.HCLRConfig?.MetadataDllCopyDirectory
                    ?? $"Assets/{DllCopyDirectoryName}/MetadataDll";
            }
        }

        public void SetRuntimeSettings(ForgeCLR.Runtime.ForgeCLRRuntimeSettings settings)
        {
            // RuntimeSettings 现在通过 EnsureRuntimeSettingsAsset 管理
            // 此方法保留兼容，不做额外操作
        }

        public void SaveSettings()
        {
            launcherSceneLocation = NormalizeAssetPath(launcherSceneLocation, DefaultLauncherScenePath);
            fileServerRootDirectory = NormalizeFileServerRootDirectory(fileServerRootDirectory);
            fileServerPort = Mathf.Clamp(fileServerPort, 1, 65535);
            fileServerBindIPAddress = fileServerBindIPAddress?.Trim() ?? "";
            Save(true);
        }

        private static string NormalizeAssetPath(string path, string fallback)
        {
            if (string.IsNullOrWhiteSpace(path)) return fallback;
            return path.Trim().Replace("\\", "/");
        }

        private static string NormalizeFileServerRootDirectory(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Bundles"))
                : path.Trim();
        }
    }
}
