using System.IO;
using System.Linq;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 StreamingAssets 中文件名是否仅包含跨平台安全字符。
    /// Android aapt2 打包时要求资源文件名仅包含 ASCII 字母、数字、下划线、连字符和点号，
    /// 中文、全角、空格及其它特殊字符会导致打包失败或运行时资源加载异常。
    /// 严格模式下返回 Failed（阻断），非严格模式返回 Warning（提醒但允许继续）。
    /// 此检查不支持自动修复，需要用户手动重命名问题文件。
    /// </summary>
    public sealed class StreamingAssetsFileNameCheck : IForgeCLRValidationCheck
    {
        public string Title => "StreamingAssets 文件名";
        public bool CanRepair => false;

        private const string StreamingAssetsPath = "Assets/StreamingAssets";

        /// <summary>
        /// 扫描 StreamingAssets 目录，检测文件名是否仅包含跨平台安全 ASCII 字符。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            if (!Directory.Exists(StreamingAssetsPath))
            {
                return new ForgeCLRValidationItem(Title, "StreamingAssets 目录不存在，无需检查",
                    ForgeCLRValidationStatus.Passed);
            }

            var allFiles = Directory.GetFiles(StreamingAssetsPath, "*", SearchOption.AllDirectories);
            var problemFiles = allFiles
                .Select(Path.GetFileName)
                .Where(name => IsSafeFileName(name) == false)
                .ToList();

            if (problemFiles.Count == 0)
            {
                return new ForgeCLRValidationItem(Title, "StreamingAssets 中所有文件名合规",
                    ForgeCLRValidationStatus.Passed);
            }

            var status = context.StrictMode ? ForgeCLRValidationStatus.Failed : ForgeCLRValidationStatus.Warning;
            var modeLabel = context.StrictMode ? "严格模式" : "非严格模式";
            var sample = string.Join(", ", problemFiles.Take(5));
            var extra = problemFiles.Count > 5 ? $" 等 {problemFiles.Count} 个文件" : "";
            return new ForgeCLRValidationItem(Title,
                $"[{modeLabel}] StreamingAssets 中存在 {problemFiles.Count} 个非安全字符文件名：{sample}{extra}",
                status);
        }

        /// <summary>
        /// 不支持自动修复。请手动重命名问题文件。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context) { }

        /// <summary>
        /// 判断文件名是否仅包含跨平台安全字符。
        /// 允许：a-z A-Z 0-9 _ - .
        /// 这些字符在所有平台的文件系统和 Android aapt2 打包工具中均安全。
        /// </summary>
        private static bool IsSafeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            foreach (char c in fileName)
            {
                if (c >= 'a' && c <= 'z') continue;
                if (c >= 'A' && c <= 'Z') continue;
                if (c >= '0' && c <= '9') continue;
                if (c == '_' || c == '-' || c == '.') continue;
                return false;
            }

            return true;
        }
    }
}
