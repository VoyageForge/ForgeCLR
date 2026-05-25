using System;
using System.Linq;
using UnityEngine;
using VoyageForge.Depot.Runtime.Attributes;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// ForgeCLR 运行时配置，保存启动资源包、热更新程序集和首场景相关参数。
    /// </summary>
    [CreateAssetMenu(fileName = AssetName, menuName = "VoyageForge/ForgeCLR Runtime Settings")]
    public sealed class ForgeCLRRuntimeSettings : ScriptableObject
    {
        /// <summary>
        /// 默认资源加载名称，放在 Resources 目录下时可以被 Launcher 自动加载。
        /// </summary>
        public const string AssetName = "ForgeCLRRuntimeSettings";

        /// <summary>
        /// 默认配置在 Resources 下的子路径。
        /// </summary>
        public const string DefaultResourcesPath = "VoyageForge/Config/" + AssetName;

       
        
        
        /// <summary>
        /// YooAssets 资源包名称，需要和 YooAssets Collector 中的 PackageName 保持一致。
        /// </summary>
        [Header("YooAssets")]
        [ReadOnly]
        [SerializeField] private string packageName = "DefaultPackage";

        /// <summary>
        /// YooAssets 运行模式，编辑器调试通常使用 EditorSimulateMode。
        /// </summary>
        [SerializeField] private EPlayMode playMode = EPlayMode.EditorSimulateMode;

        

        /// <summary>
        /// AOT 补充元数据 DLL 的 YooAssets 完整资源路径，由 ForgeCLR 构建流程自动写入。
        /// </summary>
        [HideInInspector]
        [SerializeField] private string[] aotMetadataDllLocations =
        {
            "Assets/HotUpdateDll/MetadataDll/mscorlib.dll.bytes",
            "Assets/HotUpdateDll/MetadataDll/System.dll.bytes",
            "Assets/HotUpdateDll/MetadataDll/System.Core.dll.bytes"
        };

        /// <summary>
        /// 热更新程序集 DLL 的 YooAssets 完整资源路径，由 ForgeCLR 构建流程自动写入。
        /// </summary>
        [HideInInspector]
        [SerializeField] private string[] hotUpdateDllLocations =
        {
            "Assets/HotUpdateDll/HotUpdateDll/HotUpdateAssembly.dll.bytes"
        };

        /// <summary>
        /// HostPlayMode 下网络请求失败时，是否自动回退到内置离线资源继续启动。
        /// </summary>
        [Header("Offline Fallback")]
        [SerializeField] private bool enableAutoOfflineFallback = true;

        /// <summary>
        /// 是否在热更新程序集加载完成后自动加载第一个业务场景。
        /// </summary>
        [Header("Startup Scene")]
        [SerializeField] private bool loadStartupScene = true;

        /// <summary>
        /// 第一个业务场景的完整资源路径；没有打入资源包时也可以作为 Unity 场景路径回退加载。
        /// </summary>
        [ReadOnly]
        [SerializeField] private string startupSceneLocation = "Assets/Scenes/Main.unity";

        /// <summary>
        /// YooAssets 资源包名称。
        /// </summary>
        public string PackageName => packageName;

        /// <summary>
        /// YooAssets 运行模式。
        /// </summary>
        public EPlayMode PlayMode => playMode;


        /// <summary>
        /// AOT 补充元数据 DLL 地址集合。
        /// </summary>
        public string[] AotMetadataDllLocations => aotMetadataDllLocations ?? Array.Empty<string>();

        /// <summary>
        /// 热更新程序集 DLL 地址集合。
        /// </summary>
        public string[] HotUpdateDllLocations => hotUpdateDllLocations ?? Array.Empty<string>();

        /// <summary>
        /// HostPlayMode 下网络请求失败时是否自动回退到内置离线资源。
        /// </summary>
        public bool EnableAutoOfflineFallback => enableAutoOfflineFallback;

        /// <summary>
        /// 是否在热更新启动完成后加载第一个业务场景。
        /// </summary>
        public bool LoadStartupScene => loadStartupScene;

        /// <summary>
        /// 第一个业务场景的完整资源路径。
        /// </summary>
        public string StartupSceneLocation => startupSceneLocation;

        /// <summary>
        /// 从 Resources 加载默认运行时配置；不存在时返回内存默认配置。
        /// </summary>
        /// <returns>运行时配置实例。</returns>
        public static ForgeCLRRuntimeSettings LoadDefault()
        {
            var settings = Resources.Load<ForgeCLRRuntimeSettings>(DefaultResourcesPath);
            if (settings != null)
            {
                return settings;
            }

            settings = Resources.Load<ForgeCLRRuntimeSettings>(AssetName);
            if (settings != null)
            {
                return settings;
            }

            settings = Resources.LoadAll<ForgeCLRRuntimeSettings>(string.Empty)
                .FirstOrDefault();
            return settings != null ? settings : CreateInstance<ForgeCLRRuntimeSettings>();
        }
    }
}
