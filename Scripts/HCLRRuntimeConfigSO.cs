using System;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// HCLR（HybridCLR）模块的运行时配置 SO，位于 Resources 中。
    /// 被 ForgeCLRRuntimeSettings 引用即表示模块已启用。
    /// </summary>
    public class HCLRRuntimeConfigSO : ScriptableObject
    {
        [SerializeField]
        private bool enabled = true;

        [SerializeField]
        private string dllCopyDirectoryName = "HotUpdateDll";

        [SerializeField]
        private string[] aotMetadataDllLocations = Array.Empty<string>();

        [SerializeField]
        private string[] hotUpdateDllLocations = Array.Empty<string>();

        public bool Enabled { get => enabled; set => enabled = value; }

        public string DllCopyDirectoryName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(dllCopyDirectoryName))
                    return "HotUpdateDll";
                var n = dllCopyDirectoryName.Trim().Replace("\\", "/").Trim('/');
                var i = n.LastIndexOf('/');
                if (i >= 0) n = n[(i + 1)..];
                return string.IsNullOrWhiteSpace(n) ? "HotUpdateDll" : n;
            }
        }

        public string HotUpdateDllCopyDirectory => $"Assets/{DllCopyDirectoryName}/HotUpdateDll";
        public string MetadataDllCopyDirectory => $"Assets/{DllCopyDirectoryName}/MetadataDll";

        public string[] AotMetadataDllLocations => aotMetadataDllLocations ?? Array.Empty<string>();
        public string[] HotUpdateDllLocations => hotUpdateDllLocations ?? Array.Empty<string>();

        public void SetEnabled(bool v) { enabled = v; }
        public void SetDllCopyDirectoryName(string name) { dllCopyDirectoryName = name; }
        public void SetAotMetadataDllLocations(string[] v) { aotMetadataDllLocations = v ?? Array.Empty<string>(); }
        public void SetHotUpdateDllLocations(string[] v) { hotUpdateDllLocations = v ?? Array.Empty<string>(); }
    }
}
