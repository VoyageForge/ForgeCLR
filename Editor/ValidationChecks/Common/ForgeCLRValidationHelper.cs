using System.IO;
using UnityEditor;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测工具辅助方法，提供路径规范化、目录创建等公共操作。
    /// </summary>
    internal static class ForgeCLRValidationHelper
    {
        /// <summary>
        /// 将路径中的反斜杠统一转换为正斜杠，用于 Unity 资源路径比较。
        /// </summary>
        public static string NormalizeAssetPath(string path)
        {
            return path?.Replace("\\", "/") ?? string.Empty;
        }

        /// <summary>
        /// 判断路径是否位于 Unity Assets 目录下。
        /// </summary>
        public static bool IsAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) == false && path.StartsWith("Assets/");
        }

        /// <summary>
        /// 判断目录名称是否为合法的单级目录名（不含路径分隔符）。
        /// </summary>
        public static bool IsValidFolderName(string value)
        {
            return string.IsNullOrWhiteSpace(value) == false &&
                   value.Contains("/") == false &&
                   value.Contains("\\") == false;
        }

        /// <summary>
        /// 按需创建目录，路径为空或目录已存在时跳过。
        /// </summary>
        public static void CreateDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) == false && Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
