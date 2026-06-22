using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VoyageForge.ForgeCLR.Runtime;

namespace VoyageForge.ForgeCLR.Editor
{
    public class YooAssetsModuleProvider : ForgeCLRModuleSettingsProvider<YooAssetsRuntimeConfigSO>
    {
        public override string ModuleId => "com.voyageforge.forgeclr.yooassets";
        public override string DisplayName => "YooAssets 资源管理";
        public override string[] Dependencies => Array.Empty<string>();

        public override VisualElement CreateModuleUI(ForgeCLRRuntimeSettings rs, ForgeCLRSettings editorSettings)
        {
            var root = new VisualElement();
            var config = rs.GetModuleConfig<YooAssetsRuntimeConfigSO>();
            if (config == null)
            {
                root.Add(new HelpBox("YooAssets 配置未找到。", HelpBoxMessageType.Warning));
                return root;
            }

            // ---- 资源包 section ----
            var pkgCard = CreateSectionCard("资源包");
            pkgCard.Add(CreatePackageDropdown(config, rs));
            pkgCard.Add(CreatePlayModeDropdown(config, rs));
            pkgCard.Add(CreateOfflineFallbackToggle(config, rs));
            root.Add(pkgCard);

            // ---- 场景 section ----
            var sceneCard = CreateSectionCard("场景");
            sceneCard.Add(CreateLauncherSceneDropdown(config, rs, editorSettings));
            sceneCard.Add(CreateLoadStartupSceneToggle(config, rs));
            sceneCard.Add(CreateStartupSceneDropdown(config, rs));
            root.Add(sceneCard);

            return root;
        }

        private static VisualElement CreatePackageDropdown(YooAssetsRuntimeConfigSO config, ForgeCLRRuntimeSettings rs)
        {
            var c = new VisualElement();
            if (!ForgeCLRRuntimeSettingsEditorUtility.TryGetYooAssetCollectorSetting(out _))
            {
                c.Add(new HelpBox("未找到 YooAssets Collector 配置。", HelpBoxMessageType.Warning));
                return c;
            }

            var choices = ForgeCLRRuntimeSettingsEditorUtility.GetYooAssetPackageNames().ToList();
            if (choices.Count == 0)
            {
                c.Add(new HelpBox("YooAssets Collector 中没有 Package。", HelpBoxMessageType.Warning));
                return c;
            }

            var v = config.PackageName;
            if (!string.IsNullOrWhiteSpace(v) && !choices.Contains(v)) choices.Insert(0, v);
            if (!choices.Contains(v)) v = choices[0];
            config.SetPackageName(v);

            var dd = new PopupField<string>("资源包名称", choices, v);
            dd.AddToClassList("fclr-settings-field");
            dd.RegisterValueChangedCallback(evt =>
            {
                config.SetPackageName(evt.newValue);
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(rs);
                AssetDatabase.SaveAssets();
            });
            c.Add(dd);
            return c;
        }

        private static VisualElement CreatePlayModeDropdown(YooAssetsRuntimeConfigSO config, ForgeCLRRuntimeSettings rs)
        {
            var c = new VisualElement();
            var choices = new System.Collections.Generic.List<string>
                { "Editor Simulate", "Offline", "Host" };
            var values = new[] { YooAsset.EPlayMode.EditorSimulateMode, YooAsset.EPlayMode.OfflinePlayMode, YooAsset.EPlayMode.HostPlayMode };
            var idx = Array.IndexOf(values, config.PlayMode);
            if (idx < 0) idx = 0;

            var dd = new PopupField<string>("运行模式", choices, choices[idx]);
            dd.AddToClassList("fclr-settings-field");
            dd.RegisterValueChangedCallback(evt =>
            {
                var i = choices.IndexOf(evt.newValue);
                if (i >= 0) config.SetPlayMode(values[i]);
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(rs);
                AssetDatabase.SaveAssets();
            });
            c.Add(dd);
            return c;
        }

        private static VisualElement CreateOfflineFallbackToggle(YooAssetsRuntimeConfigSO config, ForgeCLRRuntimeSettings rs)
        {
            var c = new VisualElement();
            var t = new Toggle("网络失败时自动使用离线模式") { value = config.EnableAutoOfflineFallback };
            t.AddToClassList("fclr-settings-field");
            t.RegisterValueChangedCallback(evt =>
            {
                config.SetEnableAutoOfflineFallback(evt.newValue);
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(rs);
                AssetDatabase.SaveAssets();
            });
            c.Add(t);
            return c;
        }

        private static VisualElement CreateLoadStartupSceneToggle(
            YooAssetsRuntimeConfigSO config, ForgeCLRRuntimeSettings rs)
        {
            var c = new VisualElement();
            var t = new Toggle("启动后加载首场景") { value = config.LoadStartupScene };
            t.AddToClassList("fclr-settings-field");
            t.RegisterValueChangedCallback(evt =>
            {
                config.SetLoadStartupScene(evt.newValue);
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(rs);
                AssetDatabase.SaveAssets();
            });
            c.Add(t);
            return c;
        }

        private static VisualElement CreateStartupSceneDropdown(
            YooAssetsRuntimeConfigSO config, ForgeCLRRuntimeSettings rs)
        {
            var c = new VisualElement();
            var choices = ForgeCLRRuntimeSettingsEditorUtility.GetAvailableStartupSceneLocations().ToList();
            if (choices.Count == 0)
            {
                c.Add(new HelpBox("项目中未找到任何场景文件，请先创建启动场景。", HelpBoxMessageType.Warning));
                return c;
            }

            var v = config.StartupSceneLocation;
            if (!string.IsNullOrWhiteSpace(v) && !choices.Contains(v)) choices.Insert(0, v);
            if (!choices.Contains(v)) v = choices[0];
            config.SetStartupSceneLocation(v);

            var dd = new PopupField<string>("启动场景地址", choices, v);
            dd.AddToClassList("fclr-settings-field");
            dd.RegisterValueChangedCallback(evt =>
            {
                config.SetStartupSceneLocation(evt.newValue);
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(rs);
                AssetDatabase.SaveAssets();
            });
            c.Add(dd);
            return c;
        }

        private static VisualElement CreateLauncherSceneDropdown(
            YooAssetsRuntimeConfigSO config, ForgeCLRRuntimeSettings rs, ForgeCLRSettings editorSettings)
        {
            var c = new VisualElement();
            var choices = ForgeCLRRuntimeSettingsEditorUtility.GetAvailableStartupSceneLocations().ToList();
            if (choices.Count == 0)
                choices.Add(ForgeCLRSettings.DefaultLauncherScenePath);

            var v = config.LauncherSceneLocation;
            if (!string.IsNullOrWhiteSpace(v) && !choices.Contains(v)) choices.Insert(0, v);
            if (!choices.Contains(v)) v = choices[0];
            config.SetLauncherSceneLocation(v);

            var dd = new PopupField<string>("Launcher 场景", choices, v);
            dd.AddToClassList("fclr-settings-field");
            dd.RegisterValueChangedCallback(evt =>
            {
                config.SetLauncherSceneLocation(evt.newValue);
                editorSettings.SetLauncherSceneLocation(evt.newValue);
                ForgeCLRValidationUtility.EnsureLauncherSceneInBuildSettings();
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(rs);
                AssetDatabase.SaveAssets();
            });
            c.Add(dd);
            return c;
        }

        public override IForgeCLRValidationCheck[] CreateValidationChecks(ForgeCLRRuntimeSettings rs)
        {
            return new IForgeCLRValidationCheck[]
            {
                new YooAssetsRuntimeCheck(),
                new YooAssetSettingsCheck(),
                new YooAssetsCollectorCheck(),
                new YooAssetsPackageCheck(),
                new LauncherSceneCheck(),
                new LauncherBuildSettingsCheck(),
                new RuntimeSettingsSOCheck(),
                new StartupSceneABCollectionCheck(),
            };
        }

        public override void ExecuteQuickSetup(ForgeCLRRuntimeSettings rs, ForgeCLRSettings editorSettings)
        {
            ForgeCLRRuntimeSettingsEditorUtility.EnsureYooAssetSettings();
            ForgeCLRQuickSetup.EnsureYooAssetCollectorConfiguration();
            ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            ForgeCLRValidationUtility.EnsureLauncherSceneInBuildSettings();
        }

        public override void OnPostBuildResource(UnityEditor.BuildTarget target)
        {
            ForgeCLRRuntimeSettingsEditorUtility.AutoFillRuntimeSettings();
            ForgeCLRQuickSetup.EnsureYooAssetCollectorConfiguration();
        }
    }
}
