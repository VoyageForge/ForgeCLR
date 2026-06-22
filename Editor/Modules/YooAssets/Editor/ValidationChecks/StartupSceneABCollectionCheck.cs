using System.Linq;
using UnityEditor;
using VoyageForge.ForgeCLR.Runtime;
using YooAsset.Editor;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测启动场景是否已被加入 YooAsset 资源收集器的当前资源包中。
    /// 启动场景是应用启动后加载的第一个场景，需要被 YooAsset 正确打包才能从 AssetBundle 中加载。
    /// 如果运行时设置中关闭了自动加载启动场景，则跳过此检查。
    /// 可通过修复功能自动补齐 YooAsset 收集器配置。
    /// </summary>
    public sealed class StartupSceneABCollectionCheck : IForgeCLRValidationCheck<YooAssetsRuntimeConfigSO>
    {
        public string ModuleId => null;
        public string Title => "启动场景 AB 收集";
        public bool CanRepair => true;

        /// <summary>
        /// 验证启动场景是否在 YooAsset 收集器中有对应的收集规则，
        /// 若运行时已关闭自动加载启动场景则直接通过。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context, YooAssetsRuntimeConfigSO config)
        {
            var collected = config != null &&
                            (config.LoadStartupScene == false ||
                             HotUpdateDllABCollectionCheck.HasCollectorForPath(
                                 context.Settings.RuntimeSettings,
                                 context.CollectorSetting, config.StartupSceneLocation));
            return new ForgeCLRValidationItem(Title,
                collected ? "启动场景已加入当前 YooAssets 包，或已关闭自动加载首场景" : "启动场景尚未加入当前 YooAssets 包，可点击修复补齐",
                collected ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        ForgeCLRValidationItem IForgeCLRValidationCheck.Validate(ForgeCLRValidationContext context)
        {
            var rs = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            return Validate(context, rs.GetModuleConfig<YooAssetsRuntimeConfigSO>());
        }

        /// <summary>
        /// 自动补齐 YooAsset 收集器配置以包含启动场景。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            ForgeCLRQuickSetup.EnsureYooAssetCollectorConfiguration();
        }
    }
}
