using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// ForgeCLR 配置检测与自动修复入口。
    /// 通过反射扫描所有程序集中实现了 IForgeCLRValidationCheck 的类型，
    /// 用户可在任意位置添加检测类而无需修改本文件。
    /// </summary>
    public static class ForgeCLRValidationUtility
    {
        private static List<IForgeCLRValidationCheck> _checks;

        /// <summary>
        /// 通过反射扫描所有已加载程序集，发现并实例化所有 IForgeCLRValidationCheck 实现。
        /// </summary>
        private static List<IForgeCLRValidationCheck> Checks
        {
            get
            {
                if (_checks != null)
                    return _checks;

                _checks = new List<IForgeCLRValidationCheck>();
                var interfaceType = typeof(IForgeCLRValidationCheck);

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.IsAbstract || type.IsInterface)
                                continue;
                            if (interfaceType.IsAssignableFrom(type) == false)
                                continue;
                            if (Activator.CreateInstance(type) is IForgeCLRValidationCheck instance)
                                _checks.Add(instance);
                        }
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        // 某些程序集无法加载所有类型（如缺失依赖），跳过即可
                    }
                }

                return _checks;
            }
        }

        /// <summary>
        /// 创建当前 ForgeCLR 环境检测报告。
        /// </summary>
        public static ForgeCLRValidationReport CreateReport()
        {
            var context = new ForgeCLRValidationContext(ForgeCLRSettings.instance);
            var items = Checks
                .Select(check => check.Validate(context))
                .Where(item => item != null)
                .OrderBy(item => item.Status == ForgeCLRValidationStatus.Failed ? 0
                    : item.Status == ForgeCLRValidationStatus.Warning ? 1
                    : 2)
                .ToList();
            return new ForgeCLRValidationReport(items);
        }

        /// <summary>
        /// 构建前验证配置；失败项会直接阻断构建。
        /// </summary>
        public static ForgeCLRValidationReport ValidateForBuild(string context)
        {
            var report = CreateReport();
            if (report.FailedCount == 0)
                return report;

            var message = string.Join("\n", report.Items
                .Where(item => item.Status == ForgeCLRValidationStatus.Failed)
                .Select(item => $"{item.Title}：{item.Message}"));
            throw new BuildFailedException($"ForgeCLR {context} 前置检查失败：\n{message}");
        }

        /// <summary>
        /// 判断指定环境检测项是否支持自动修复。
        /// </summary>
        public static bool CanRepair(string title)
        {
            return Checks.FirstOrDefault(c => c.Title == title)?.CanRepair ?? false;
        }

        /// <summary>
        /// 自动修复指定环境检测项。
        /// </summary>
        public static bool TryRepair(string title)
        {
            var check = Checks.FirstOrDefault(c => c.Title == title);
            if (check == null || check.CanRepair == false)
                return false;

            var settings = ForgeCLRSettings.instance;
            check.Repair(new ForgeCLRValidationContext(settings));

            settings.SaveSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ForgeCLR] 已尝试修复环境检测项：{title}");
            return true;
        }

        /// <summary>
        /// 确保 Launcher 场景位于 Build Settings 第一位。
        /// </summary>
        public static void EnsureLauncherSceneInBuildSettings()
        {
            var settings = ForgeCLRSettings.instance;
            var launcherScene = settings.LauncherSceneLocation;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(launcherScene) == null)
                launcherScene = ForgeCLRSettings.DefaultLauncherScenePath;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(launcherScene) == null)
                throw new BuildFailedException($"Launcher 场景不存在：{settings.LauncherSceneLocation}");

            var scenes = EditorBuildSettings.scenes
                .Where(scene => string.IsNullOrWhiteSpace(scene.path) == false)
                .Where(scene => NormalizeAssetPath(scene.path) != NormalizeAssetPath(launcherScene))
                .ToList();

            scenes.Insert(0, new EditorBuildSettingsScene(launcherScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[ForgeCLR] 已设置 Launcher 场景为 Build Settings 第一位：{launcherScene}");
        }

        private static string NormalizeAssetPath(string path)
        {
            return path?.Replace("\\", "/") ?? string.Empty;
        }
    }
}
