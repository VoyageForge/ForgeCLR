using System.Linq;
using YooAsset.Editor;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 AOT 元数据 DLL 目录是否已被加入 YooAsset 资源收集器的当前资源包中。
    /// AOT 元数据 DLL 是 HybridCLR 运行热更新代码的补充数据，必须随包一起发布。
    /// 若未加入收集器，运行时将因缺失元数据而无法正常解释执行热更新代码。
    /// 可通过修复功能自动补齐 YooAsset 收集器配置。
    /// </summary>
    public sealed class MetadataDllABCollectionCheck : IForgeCLRValidationCheck
    {
        public string Title => "AOT 元数据 DLL AB 收集";
        public bool CanRepair => true;

        /// <summary>
        /// 验证 AOT 元数据 DLL 目录是否在 YooAsset 收集器中有对应的收集规则。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var settings = context.Settings;
            var exists = HotUpdateDllABCollectionCheck.HasCollectorForPath(settings.RuntimeSettings,
                context.CollectorSetting, settings.MetadataDllCopyDirectory);
            return new ForgeCLRValidationItem(Title,
                exists ? "AOT 元数据 DLL 目录已加入当前 YooAssets 包" : "AOT 元数据 DLL 目录尚未加入当前 YooAssets 包，可点击修复补齐",
                exists ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        /// <summary>
        /// 自动补齐 YooAsset 收集器配置以包含 AOT 元数据 DLL 目录。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            ForgeCLRQuickSetup.EnsureYooAssetCollectorConfiguration();
        }
    }
}
