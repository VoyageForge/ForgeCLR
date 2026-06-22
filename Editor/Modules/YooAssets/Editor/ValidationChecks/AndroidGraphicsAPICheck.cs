using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VoyageForge.ForgeCLR.Runtime;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// Android 平台启动参数检测：确认 -force-gles 已配置。
    /// 在 Android 平台上，HybridCLR 需要强制使用 OpenGL ES 图形 API 以确保兼容性。
    /// 优先检测当前 Unity Editor 进程命令行，其次检测 Unity Hub 项目配置。
    /// 修复功能会通过 Unity Hub 的项目配置文件自动添加该启动参数，
    /// 添加后需重启 Editor 才能生效。
    /// 仅在 Build Target 为 Android 时执行此检查，其他平台返回 null 跳过。
    /// </summary>
    public sealed class AndroidGraphicsAPICheck : IForgeCLRValidationCheck<YooAssetsRuntimeConfigSO>
    {
        public string ModuleId => null;
        public string Title => "Android 图形 API";
        public bool CanRepair => true;

        /// <summary>
        /// 验证 Android 平台是否已配置 -force-gles 启动参数。
        /// 仅在 Build Target 为 Android 时执行，其他平台直接返回 null（跳过）。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context, YooAssetsRuntimeConfigSO config)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                return null;

            var hasInProcess = Environment.GetCommandLineArgs().Contains("-force-gles");
            if (hasInProcess)
            {
                return new ForgeCLRValidationItem(Title,
                    "Unity Editor 启动命令行已包含 -force-gles",
                    ForgeCLRValidationStatus.Passed);
            }

            var projectPath = Path.GetDirectoryName(Application.dataPath);
            var hasInHub = !string.IsNullOrEmpty(projectPath) &&
                           UnityHubArgsHelper.HasArgument(projectPath, "-force-gles");
            if (hasInHub)
            {
                return new ForgeCLRValidationItem(Title,
                    "Unity Hub 项目配置已包含 -force-gles，重启 Editor 后生效",
                    ForgeCLRValidationStatus.Warning);
            }

            return new ForgeCLRValidationItem(Title,
                "Unity Editor 启动命令行未包含 -force-gles",
                ForgeCLRValidationStatus.Failed);
        }

        ForgeCLRValidationItem IForgeCLRValidationCheck.Validate(ForgeCLRValidationContext context)
        {
            var rs = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            return Validate(context, rs.GetModuleConfig<YooAssetsRuntimeConfigSO>());
        }

        /// <summary>
        /// 通过 Unity Hub 配置文件自动添加 -force-gles 启动参数。
        /// 修改后需要重启 Editor 才能生效。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context)
        {
            var projectPath = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectPath))
            {
                Debug.LogError("[ForgeCLR] 无法获取项目根路径");
                return;
            }

            UnityHubArgsHelper.AddArgumentIfMissing(projectPath, "-force-gles");
        }
    }
}
