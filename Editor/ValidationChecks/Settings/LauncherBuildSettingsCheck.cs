using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 Launcher 场景是否位于 Build Settings 场景列表的第一位（启用状态）。
    /// Unity 构建时会将 Build Settings 中第一个启用的场景作为入口场景，
    /// 若 Launcher 场景不在第一位，构建出的应用将无法正确启动热更新流程。
    /// 可通过修复功能自动将 Launcher 场景调整到 Build Settings 首位。
    /// </summary>
    public sealed class LauncherBuildSettingsCheck : IForgeCLRValidationCheck
    {
        public string Title => "Launcher Build Settings";
        public bool CanRepair => true;

        /// <summary>
        /// 验证 Launcher 场景是否为 Build Settings 中第一个启用的场景。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var location = context.Settings.LauncherSceneLocation;
            var isFirst = IsLauncherSceneFirstInBuildSettings(location);
            return new ForgeCLRValidationItem(Title,
                isFirst ? "Launcher 场景已位于 Build Settings 第一位" : "Launcher 场景未位于 Build Settings 第一位，可点击修复",
                isFirst ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        /// <summary>
        /// 自动将 Launcher 场景调整到 Build Settings 的首位。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            ForgeCLRValidationUtility.EnsureLauncherSceneInBuildSettings();
        }

        /// <summary>
        /// 检查 Launcher 场景是否为 Build Settings 中第一个启用的场景项。
        /// </summary>
        private static bool IsLauncherSceneFirstInBuildSettings(string launcherSceneLocation)
        {
            var firstScene = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled);
            return firstScene != null &&
                   ForgeCLRValidationHelper.NormalizeAssetPath(firstScene.path) ==
                   ForgeCLRValidationHelper.NormalizeAssetPath(launcherSceneLocation);
        }
    }
}
