using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// ForgeCLR 主运行时配置，位于 Resources 中。
    /// 通过 List 统一管理所有子模块运行时配置 SO。
    /// 子 SO 作为此资产的子物体存储，List 中存在即模块已启用。
    /// </summary>
    [CreateAssetMenu(fileName = AssetName, menuName = "VoyageForge/ForgeCLR Runtime Settings")]
    public sealed class ForgeCLRRuntimeSettings : ScriptableObject
    {
        public const string AssetName = "ForgeCLRRuntimeSettings";
        public const string DefaultResourcesPath = "VoyageForge/Config/" + AssetName;

        [SerializeField]
        private List<ScriptableObject> moduleConfigs = new List<ScriptableObject>();

        // ===== 泛型读写（编辑器用） =====

        public T GetModuleConfig<T>() where T : ScriptableObject
            => moduleConfigs.OfType<T>().FirstOrDefault();

        public void SetModuleConfig<T>(T config) where T : ScriptableObject
        {
            moduleConfigs.RemoveAll(c => c is T);
            if (config != null) moduleConfigs.Add(config);
        }

        // ===== 便捷属性 =====

        public YooAssetsRuntimeConfigSO YooAssetsConfig
            => GetModuleConfig<YooAssetsRuntimeConfigSO>();

        public HCLRRuntimeConfigSO HCLRConfig
            => GetModuleConfig<HCLRRuntimeConfigSO>();

        /// <summary>模块是否已安装且启用。</summary>
        public bool IsYooAssetsEnabled => YooAssetsConfig != null && YooAssetsConfig.Enabled;
        public bool IsHCLRInstalled => HCLRConfig != null;
        public bool IsYooAssetsInstalled => YooAssetsConfig != null;
        public bool IsHCLREnabled => HCLRConfig != null;

        // ===== 向后兼容属性（委托到子 SO，运行时代码不报错） =====

        public string PackageName => YooAssetsConfig?.PackageName ?? "DefaultPackage";
        public EPlayMode PlayMode => YooAssetsConfig?.PlayMode ?? EPlayMode.EditorSimulateMode;
        public bool EnableAutoOfflineFallback => YooAssetsConfig?.EnableAutoOfflineFallback ?? true;
        public bool LoadStartupScene => YooAssetsConfig?.LoadStartupScene ?? true;
        public string StartupSceneLocation => YooAssetsConfig?.StartupSceneLocation ?? "Assets/Scenes/Main.unity";
        public string[] AotMetadataDllLocations => HCLRConfig?.AotMetadataDllLocations ?? Array.Empty<string>();
        public string[] HotUpdateDllLocations => HCLRConfig?.HotUpdateDllLocations ?? Array.Empty<string>();

        // Obsolete wrappers (old serialized property names)
        [HideInInspector, Obsolete] public string packageName { get => PackageName; }
        [HideInInspector, Obsolete] public EPlayMode playMode { get => PlayMode; }
        [HideInInspector, Obsolete] public bool enableAutoOfflineFallback { get => EnableAutoOfflineFallback; }
        [HideInInspector, Obsolete] public string[] aotMetadataDllLocations { get => AotMetadataDllLocations; }
        [HideInInspector, Obsolete] public string[] hotUpdateDllLocations { get => HotUpdateDllLocations; }

        // ===== 加载 =====

        public static ForgeCLRRuntimeSettings LoadDefault()
        {
            var s = Resources.Load<ForgeCLRRuntimeSettings>(DefaultResourcesPath);
            if (s) return s;
            s = Resources.Load<ForgeCLRRuntimeSettings>(AssetName);
            if (s) return s;
            s = Resources.LoadAll<ForgeCLRRuntimeSettings>(string.Empty).FirstOrDefault();
            return s ? s : CreateInstance<ForgeCLRRuntimeSettings>();
        }
    }
}
