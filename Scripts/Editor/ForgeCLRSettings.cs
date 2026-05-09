using UnityEditor;
using UnityEngine;
using VoyageForge.ForgeCLR.Runtime;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// ForgeCLR 编辑器配置，只保存 ForgeCLR 自身需要的 DLL 拷贝参数。
    /// </summary>
    [FilePath("ProjectSettings/ForgeCLRSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class ForgeCLRSettings : ScriptableSingleton<ForgeCLRSettings>
    {
        /// <summary>
        /// 热更新程序集 DLL 拷贝目录，必须位于 Assets 下才能被 YooAssets 收集进 AB。
        /// </summary>
        [SerializeField] private string hotUpdateDllCopyDirectory = "Assets/HotUpdateDll/HotUpdateDll";

        /// <summary>
        /// AOT 补充元数据 DLL 拷贝目录，必须位于 Assets 下才能被 YooAssets 收集进 AB。
        /// </summary>
        [SerializeField] private string metadataDllCopyDirectory = "Assets/HotUpdateDll/MetadataDll";

        /// <summary>
        /// 一键构建资源包时要自动填充的 ForgeCLR 运行时配置资产。
        /// </summary>
        [SerializeField] private ForgeCLRRuntimeSettings runtimeSettings;

        /// <summary>
        /// 热更新程序集 DLL 拷贝目录。
        /// </summary>
        public string HotUpdateDllCopyDirectory => hotUpdateDllCopyDirectory;

        /// <summary>
        /// AOT 补充元数据 DLL 拷贝目录。
        /// </summary>
        public string MetadataDllCopyDirectory => metadataDllCopyDirectory;

        /// <summary>
        /// 一键构建资源包时要自动填充的 ForgeCLR 运行时配置资产。
        /// </summary>
        public ForgeCLRRuntimeSettings RuntimeSettings => runtimeSettings;

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
        /// 保存 ForgeCLR 编辑器配置到 ProjectSettings。
        /// </summary>
        public void SaveSettings()
        {
            Save(true);
        }
    }
}
