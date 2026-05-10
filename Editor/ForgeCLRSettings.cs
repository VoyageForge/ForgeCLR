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
        /// DLL 拷贝根目录名称。
        /// 实际路径固定为 Assets/{dllCopyDirectoryName}/HotUpdateDll 和 Assets/{dllCopyDirectoryName}/MetadataDll。
        /// </summary>
        [SerializeField] private string dllCopyDirectoryName = "HotUpdateDll";

        /// <summary>
        /// 一键构建资源包时要自动填充的 ForgeCLR 运行时配置资产。
        /// </summary>
        [SerializeField] private ForgeCLRRuntimeSettings runtimeSettings;

        /// <summary>
        /// DLL 拷贝根目录名称。
        /// </summary>
        public string DllCopyDirectoryName => NormalizeDllCopyDirectoryName(dllCopyDirectoryName);

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
            dllCopyDirectoryName = NormalizeDllCopyDirectoryName(dllCopyDirectoryName);
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
    }
}
