using UnityEngine;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// YooAssets 模块的运行时配置 SO，位于 Resources 中。
    /// 被 ForgeCLRRuntimeSettings 引用即表示模块已启用。
    /// </summary>
    public class YooAssetsRuntimeConfigSO : ScriptableObject
    {
        /// <summary>
        /// YooAssets 资源包名称。
        /// </summary>
        [SerializeField]
        private string packageName = "DefaultPackage";

        /// <summary>
        /// YooAssets 运行模式。
        /// </summary>
        [SerializeField]
        private EPlayMode playMode = EPlayMode.EditorSimulateMode;

        /// <summary>
        /// HostPlayMode 下网络失败时自动回退到离线模式。
        /// </summary>
        [SerializeField]
        private bool enableAutoOfflineFallback = true;

        /// <summary>
        /// 是否启用此模块。安装后可单独关闭而不卸载。
        /// </summary>
        [SerializeField]
        private bool enabled = true;

        /// <summary>
        /// 是否在热更新补丁完成后自动加载首场景。
        /// </summary>
        [SerializeField]
        private bool loadStartupScene = true;

        /// <summary>
        /// 首场景完整资源路径。
        /// </summary>
        [SerializeField]
        private string startupSceneLocation = "Assets/Scenes/Main.unity";

        /// <summary>
        /// Launcher 场景路径（Build Settings 第一场景）。
        /// </summary>
        [SerializeField]
        private string launcherSceneLocation = "Assets/ForgeCLR/Scenes/Launcher.unity";

        public bool Enabled { get => enabled; set => enabled = value; }
        public string PackageName => packageName;
        public EPlayMode PlayMode => playMode;
        public bool EnableAutoOfflineFallback => enableAutoOfflineFallback;
        public bool LoadStartupScene => loadStartupScene;
        public string StartupSceneLocation => startupSceneLocation;
        public string LauncherSceneLocation => launcherSceneLocation;

        public void SetEnabled(bool v) { enabled = v; }
        public void SetPackageName(string name) { packageName = name; }
        public void SetPlayMode(EPlayMode mode) { playMode = mode; }
        public void SetEnableAutoOfflineFallback(bool v) { enableAutoOfflineFallback = v; }
        public void SetLoadStartupScene(bool v) { loadStartupScene = v; }
        public void SetStartupSceneLocation(string path) { startupSceneLocation = path; }
        public void SetLauncherSceneLocation(string path) { launcherSceneLocation = path; }
    }
}
