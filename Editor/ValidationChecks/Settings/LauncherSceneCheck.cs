using UnityEditor;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 Launcher 场景文件是否存在。
    /// Launcher 场景是 ForgeCLR 框架入口场景，负责初始化热更新环境并加载后续游戏内容，
    /// 若场景文件不存在则应用无法启动。此检查不支持自动修复，需用户手动指定有效场景。
    /// </summary>
    public sealed class LauncherSceneCheck : IForgeCLRValidationCheck
    {
        public string Title => "Launcher 场景";
        public bool CanRepair => false;

        /// <summary>
        /// 通过 AssetDatabase 加载验证 Launcher 场景文件是否存在。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var location = context.Settings.LauncherSceneLocation;
            var exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(location) != null;
            return new ForgeCLRValidationItem(Title,
                exists ? $"Launcher 场景存在：{location}" : "Launcher 场景不存在，请在 Project Settings 中选择有效场景",
                exists ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        /// <summary>
        /// 不支持自动修复。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context) { }
    }
}
