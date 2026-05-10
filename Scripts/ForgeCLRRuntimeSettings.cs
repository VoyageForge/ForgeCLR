using System;
using System.Linq;
using UnityEngine;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// ForgeCLR 运行时配置，保存启动资源包和热更新入口相关参数。
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
        private const string DefaultResourcesPath = "VoyageForge/Config/" + AssetName;

        /// <summary>
        /// YooAssets 资源包名称，需要和 YooAssets Collector 中的 PackageName 保持一致。
        /// </summary>
        [Header("YooAssets")]
        [SerializeField] private string packageName = "DefaultPackage";

        /// <summary>
        /// YooAssets 运行模式，编辑器调试通常使用 EditorSimulateMode。
        /// </summary>
        [SerializeField] private EPlayMode playMode = EPlayMode.EditorSimulateMode;

        /// <summary>
        /// 是否在资源补丁完成后加载 HybridCLR AOT 补充元数据。
        /// </summary>
        [Header("HybridCLR")]
        [SerializeField] private bool loadAotMetadata = true;

        /// <summary>
        /// AOT 补充元数据 DLL 的 YooAssets 地址，文件通常以 .dll.bytes 形式被打入 AB。
        /// </summary>
        [SerializeField] private string[] aotMetadataDllLocations =
        {
            "mscorlib.dll.bytes",
            "System.dll.bytes",
            "System.Core.dll.bytes"
        };

        /// <summary>
        /// 热更新程序集 DLL 的 YooAssets 地址，文件通常以 .dll.bytes 形式被打入 AB。
        /// </summary>
        [SerializeField] private string[] hotUpdateDllLocations =
        {
            "HotUpdateAssembly.dll.bytes"
        };

        /// <summary>
        /// 热更新入口类型的完整名称。
        /// </summary>
        [SerializeField] private string hotUpdateEntryTypeName = "HotUpdate.HotUpdateEntry";

        /// <summary>
        /// 热更新入口静态方法名称。
        /// </summary>
        [SerializeField] private string hotUpdateEntryMethodName = "Start";

        /// <summary>
        /// 是否在热更新入口启动后自动加载第一个业务场景。
        /// </summary>
        [Header("Startup Scene")]
        [SerializeField] private bool loadStartupScene = true;

        /// <summary>
        /// 第一个业务场景的 YooAssets 地址；没有打入资源包时也可以填写 Build Settings 中的场景名。
        /// </summary>
        [SerializeField] private string startupSceneLocation = "Main";

        /// <summary>
        /// YooAssets 资源包名称。
        /// </summary>
        public string PackageName => packageName;

        /// <summary>
        /// YooAssets 运行模式。
        /// </summary>
        public EPlayMode PlayMode => playMode;

        /// <summary>
        /// 是否加载 AOT 补充元数据。
        /// </summary>
        public bool LoadAotMetadata => loadAotMetadata;

        /// <summary>
        /// AOT 补充元数据 DLL 地址集合。
        /// </summary>
        public string[] AotMetadataDllLocations => aotMetadataDllLocations ?? Array.Empty<string>();

        /// <summary>
        /// 热更新程序集 DLL 地址集合。
        /// </summary>
        public string[] HotUpdateDllLocations => hotUpdateDllLocations ?? Array.Empty<string>();

        /// <summary>
        /// 热更新入口类型完整名称。
        /// </summary>
        public string HotUpdateEntryTypeName => hotUpdateEntryTypeName;

        /// <summary>
        /// 热更新入口静态方法名称。
        /// </summary>
        public string HotUpdateEntryMethodName => hotUpdateEntryMethodName;

        /// <summary>
        /// 是否在热更新启动完成后加载第一个业务场景。
        /// </summary>
        public bool LoadStartupScene => loadStartupScene;

        /// <summary>
        /// 第一个业务场景的 YooAssets 地址或 Unity 场景名。
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
