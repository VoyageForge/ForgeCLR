using System.IO;
using System.Linq;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 StreamingAssets 中是否包含 YooAsset 启动的关键文件。
    /// YooAsset 初始化流程：先读取内置 catalog（json + hash），
    /// 解析后得知所有资源包清单和远程下载地址，之后按需拉取 .bundle。
    /// 因此只检查 catalog 文件是否存在即可，不要求所有 .bundle 都在本地。
    /// 此检查不支持自动修复，需通过 YooAsset 构建内置资源流程生成。
    /// </summary>
    public sealed class StreamingAssetsYooAssetFilesCheck : IForgeCLRValidationCheck
    {
        public string Title => "StreamingAssets YooAssets 文件";
        public bool CanRepair => false;

        private const string StreamingAssetsPath = "Assets/StreamingAssets";

        /// <summary>
        /// 扫描 StreamingAssets 目录，检测是否包含 YooAsset catalog 启动文件。
        /// 有 json + hash 即可通过，仅有其中之一或都没有则告警。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            if (!Directory.Exists(StreamingAssetsPath))
            {
                return new ForgeCLRValidationItem(Title,
                    "StreamingAssets 目录不存在，构建内置资源后会自动生成",
                    ForgeCLRValidationStatus.Warning);
            }

            var allFiles = Directory.GetFiles(StreamingAssetsPath, "*", SearchOption.TopDirectoryOnly);
            var jsonFiles = allFiles.Where(f => Path.GetExtension(f).ToLowerInvariant() == ".json").ToList();
            var hashFiles = allFiles.Where(f => Path.GetExtension(f).ToLowerInvariant() == ".hash").ToList();

            if (jsonFiles.Count == 0 && hashFiles.Count == 0)
            {
                return new ForgeCLRValidationItem(Title,
                    "StreamingAssets 中未找到 catalog 文件（.json / .hash），YooAsset 无法初始化内置资源包",
                    context.StrictMode ? ForgeCLRValidationStatus.Failed : ForgeCLRValidationStatus.Warning);
            }

            if (jsonFiles.Count == 0)
            {
                return new ForgeCLRValidationItem(Title,
                    "StreamingAssets 中缺少 catalog.json，YooAsset 无法解析资源清单",
                    context.StrictMode ? ForgeCLRValidationStatus.Failed : ForgeCLRValidationStatus.Warning);
            }

            if (hashFiles.Count == 0)
            {
                return new ForgeCLRValidationItem(Title,
                    "StreamingAssets 中缺少 catalog.hash，YooAsset 无法校验资源清单完整性",
                    context.StrictMode ? ForgeCLRValidationStatus.Failed : ForgeCLRValidationStatus.Warning);
            }

            var subFiles = Directory.GetFiles(StreamingAssetsPath, "*", SearchOption.AllDirectories);
            return new ForgeCLRValidationItem(Title,
                $"StreamingAssets 中包含 YooAsset catalog 启动文件，共 {subFiles.Length} 个文件",
                ForgeCLRValidationStatus.Passed);
        }

        /// <summary>
        /// 不支持自动修复。请通过 YooAsset 构建内置资源。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context) { }
    }
}
