using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCLR.Editor;
using UnityEditor;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// HybridCLR DLL 拷贝工具，将热更新程序集和 AOT 元数据程序集拷贝为 .bytes 文件。
    /// </summary>
    public static class CopyHotUpdateDllToFolder
    {
        /// <summary>
        /// DLL 拷贝菜单路径。
        /// </summary>
        private const string MenuPath = "VoyageForge/ForgeCLR/拷贝热更新 DLL";

        /// <summary>
        /// 从菜单执行 DLL 拷贝。
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Execute()
        {
            var result = CopyAssemblies(EditorUserBuildSettings.activeBuildTarget, true);
            Debug.Log($"[ForgeCLR] DLL 拷贝完成：成功 {result.CopiedCount}，缺失 {result.MissingFiles.Count}");
        }

        /// <summary>
        /// 拷贝当前构建目标的热更新 DLL 和 AOT 元数据 DLL。
        /// </summary>
        /// <param name="target">目标构建平台。</param>
        /// <param name="showDialog">是否显示完成弹窗。</param>
        /// <returns>DLL 拷贝结果。</returns>
        public static CopyResult CopyAssemblies(BuildTarget target, bool showDialog)
        {
            var settings = ForgeCLRSettings.instance;
            EnsureAssetDirectory(settings.HotUpdateDllCopyDirectory);
            EnsureAssetDirectory(settings.MetadataDllCopyDirectory);

            var hotUpdateNames = CollectHotUpdateAssemblyNames();
            var metadataNames = CollectAotMetadataAssemblyNames();
            var hotUpdateSourceDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            var aotSourceDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);

            var result = new CopyResult();

            try
            {
                AssetDatabase.StartAssetEditing();
                CopyAssemblyGroup(hotUpdateNames, hotUpdateSourceDir, settings.HotUpdateDllCopyDirectory, result);
                CopyAssemblyGroup(metadataNames, aotSourceDir, settings.MetadataDllCopyDirectory, result);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();
            }

            if (showDialog)
                ShowCopyDialog(result);

            return result;
        }

        /// <summary>
        /// 收集 HybridCLR 配置中的热更新程序集名称。
        /// </summary>
        /// <returns>热更新程序集名称集合。</returns>
        private static List<string> CollectHotUpdateAssemblyNames()
        {
            return SettingsUtil.HotUpdateAssemblyNamesExcludePreserved?
                .Where(name => string.IsNullOrWhiteSpace(name) == false)
                .Distinct()
                .ToList() ?? new List<string>();
        }

        /// <summary>
        /// 收集 AOTGenericReferences 中声明的 AOT 元数据程序集名称。
        /// </summary>
        /// <returns>AOT 元数据程序集名称集合。</returns>
        private static List<string> CollectAotMetadataAssemblyNames()
        {
            var assemblies = GetPatchedAOTAssemblyList();
            if (assemblies == null)
                return new List<string>();

            return assemblies
                .Where(name => string.IsNullOrWhiteSpace(name) == false)
                .Select(RemoveDllExtension)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// 拷贝一组程序集 DLL 到目标目录并改名为 .bytes。
        /// </summary>
        /// <param name="assemblyNames">程序集名称集合。</param>
        /// <param name="sourceDirectory">DLL 源目录。</param>
        /// <param name="targetAssetDirectory">Assets 下的目标目录。</param>
        /// <param name="result">拷贝结果。</param>
        private static void CopyAssemblyGroup(
            IReadOnlyCollection<string> assemblyNames,
            string sourceDirectory,
            string targetAssetDirectory,
            CopyResult result)
        {
            var index = 0;
            foreach (var assemblyName in assemblyNames)
            {
                index++;
                var dllName = $"{assemblyName}.dll";
                var sourceFile = Path.Combine(sourceDirectory, dllName);
                var targetFile = Path.Combine(ToAbsoluteProjectPath(targetAssetDirectory), $"{dllName}.bytes");

                EditorUtility.DisplayProgressBar(
                    "ForgeCLR 拷贝 DLL",
                    $"{dllName} ({index}/{assemblyNames.Count})",
                    assemblyNames.Count == 0 ? 1f : (float)index / assemblyNames.Count);

                if (File.Exists(sourceFile) == false)
                {
                    result.MissingFiles.Add(sourceFile);
                    Debug.LogWarning($"[ForgeCLR] 未找到 DLL：{sourceFile}");
                    continue;
                }

                File.Copy(sourceFile, targetFile, true);
                result.CopiedFiles.Add(targetFile);
                result.CopiedAssetFiles.Add(NormalizeAssetPath(Path.Combine(targetAssetDirectory, $"{dllName}.bytes")));
                Debug.Log($"[ForgeCLR] 拷贝 DLL：{sourceFile} -> {targetFile}");
            }
        }

        /// <summary>
        /// 通过反射读取 AOTGenericReferences.PatchedAOTAssemblyList。
        /// </summary>
        /// <returns>AOT 元数据程序集名称集合。</returns>
        private static List<string> GetPatchedAOTAssemblyList()
        {
            try
            {
                var type = Type.GetType("AOTGenericReferences, Assembly-CSharp") ?? Type.GetType("AOTGenericReferences");
                var field = type?.GetField("PatchedAOTAssemblyList", BindingFlags.Public | BindingFlags.Static);
                return field?.GetValue(null) as List<string>;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ForgeCLR] 读取 AOTGenericReferences 失败：{e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 确保目标目录位于 Assets 下并存在。
        /// </summary>
        /// <param name="assetDirectory">Assets 相对目录。</param>
        private static void EnsureAssetDirectory(string assetDirectory)
        {
            if (string.IsNullOrWhiteSpace(assetDirectory) || assetDirectory.StartsWith("Assets/", StringComparison.Ordinal) == false)
                throw new InvalidOperationException($"DLL 拷贝目录必须位于 Assets 下：{assetDirectory}");

            var absoluteDirectory = ToAbsoluteProjectPath(assetDirectory);
            if (Directory.Exists(absoluteDirectory) == false)
                Directory.CreateDirectory(absoluteDirectory);
        }

        /// <summary>
        /// 将 Assets 相对路径转换为项目绝对路径。
        /// </summary>
        /// <param name="assetPath">Assets 相对路径。</param>
        /// <returns>项目绝对路径。</returns>
        private static string ToAbsoluteProjectPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        /// <summary>
        /// 规范化 Unity 资源路径分隔符。
        /// </summary>
        /// <param name="assetPath">Assets 相对路径。</param>
        /// <returns>使用正斜杠的资源路径。</returns>
        private static string NormalizeAssetPath(string assetPath)
        {
            return assetPath.Replace("\\", "/");
        }

        /// <summary>
        /// 移除程序集名称末尾的 .dll 后缀。
        /// </summary>
        /// <param name="assemblyName">程序集名称或 DLL 文件名。</param>
        /// <returns>不带 .dll 后缀的程序集名称。</returns>
        private static string RemoveDllExtension(string assemblyName)
        {
            return assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? assemblyName.Substring(0, assemblyName.Length - 4)
                : assemblyName;
        }

        /// <summary>
        /// 显示 DLL 拷贝完成弹窗。
        /// </summary>
        /// <param name="result">DLL 拷贝结果。</param>
        private static void ShowCopyDialog(CopyResult result)
        {
            var message = $"拷贝完成，成功 {result.CopiedCount} 个文件。";
            if (result.MissingFiles.Count > 0)
                message += $"\n缺失 {result.MissingFiles.Count} 个文件，请查看 Console。";
            EditorUtility.DisplayDialog("ForgeCLR 拷贝 DLL", message, "确定");
        }

        /// <summary>
        /// DLL 拷贝结果。
        /// </summary>
        public sealed class CopyResult
        {
            /// <summary>
            /// 成功拷贝的文件路径集合。
            /// </summary>
            public readonly List<string> CopiedFiles = new List<string>();

            /// <summary>
            /// 成功拷贝的 Unity 资源路径集合。
            /// </summary>
            public readonly List<string> CopiedAssetFiles = new List<string>();

            /// <summary>
            /// 缺失的源文件路径集合。
            /// </summary>
            public readonly List<string> MissingFiles = new List<string>();

            /// <summary>
            /// 成功拷贝的文件数量。
            /// </summary>
            public int CopiedCount => CopiedFiles.Count;
        }
    }
}
