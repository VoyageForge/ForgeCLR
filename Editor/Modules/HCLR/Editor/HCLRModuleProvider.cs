using System;
using System.IO;
using System.Linq;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Installer;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UIElements;
using VoyageForge.ForgeCLR.Runtime;

namespace VoyageForge.ForgeCLR.Editor
{
    public class HCLRModuleProvider : ForgeCLRModuleSettingsProvider<HCLRRuntimeConfigSO>
    {
        public override string ModuleId => "com.voyageforge.forgeclr.hclr";
        public override string DisplayName => "HCLR 代码热更";
        public override string[] Dependencies => new[] { "com.voyageforge.forgeclr.yooassets" };

        public override void Enable(ForgeCLRRuntimeSettings rs, ForgeCLRSettings editorSettings)
        {
            if (rs.GetModuleConfig<YooAssetsRuntimeConfigSO>() == null)
                throw new InvalidOperationException("HCLR 依赖 YooAssets，请先启用 YooAssets 模块。");

            base.Enable(rs, editorSettings);

            var config = rs.GetModuleConfig<HCLRRuntimeConfigSO>();
            if (config != null)
            {
                CreateDir(config.HotUpdateDllCopyDirectory);
                CreateDir(config.MetadataDllCopyDirectory);
                AssetDatabase.Refresh();
            }
        }

        public override VisualElement CreateModuleUI(ForgeCLRRuntimeSettings rs, ForgeCLRSettings editorSettings)
        {
            var root = new VisualElement();
            var config = rs.GetModuleConfig<HCLRRuntimeConfigSO>();
            if (config == null)
            {
                root.Add(new HelpBox("HCLR 配置未找到。", HelpBoxMessageType.Warning));
                return root;
            }

            var dllCard = CreateSectionCard("DLL 配置");

            var dirField = new TextField("DLL 拷贝根目录")
            {
                value = config.DllCopyDirectoryName,
                tooltip = "DLL → Assets/{名称}/HotUpdateDll 和 Assets/{名称}/MetadataDll"
            };
            dirField.AddToClassList("fclr-settings-field");
            dirField.RegisterValueChangedCallback(evt =>
            {
                config.SetDllCopyDirectoryName(evt.newValue);
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(rs);
                AssetDatabase.SaveAssets();
            });
            dllCard.Add(dirField);

            dllCard.Add(new Label($"热更新 DLL 目录：{config.HotUpdateDllCopyDirectory}") { name = "HotUpdateDirLabel" });
            dllCard.Add(new Label($"AOT 元数据 DLL 目录：{config.MetadataDllCopyDirectory}") { name = "MetadataDirLabel" });
            dllCard.Add(new Label($"AOT 元数据 DLL 数：{config.AotMetadataDllLocations.Length}"));
            dllCard.Add(new Label($"热更新 DLL 数：{config.HotUpdateDllLocations.Length}"));

            var copyBtn = new Button(() =>
            {
                var target = EditorUserBuildSettings.activeBuildTarget;
                var result = CopyHotUpdateDllToFolder.CopyAssemblies(target, false);
                UpdateLocations(config, result);
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(rs);
                AssetDatabase.Refresh();
            })
            { text = "拷贝热更新 DLL" };
            dllCard.Add(copyBtn);

            root.Add(dllCard);
            return root;
        }

        public override IForgeCLRValidationCheck[] CreateValidationChecks(ForgeCLRRuntimeSettings rs)
        {
            return new IForgeCLRValidationCheck[]
            {
                new HybridCLRSettingsCheck(),
                new HybridCLRInstallerCheck(),
                new HotUpdateDllABCollectionCheck(),
                new MetadataDllABCollectionCheck(),
                new HotUpdateDllDirectoryStatusCheck(),
                new MetadataDllDirectoryStatusCheck(),
                new AndroidGraphicsAPICheck(),
                new HotUpdateDllCopyDirectoryCheck(),
                new MetadataDllCopyDirectoryCheck(),
            };
        }

        public override void ExecuteQuickSetup(ForgeCLRRuntimeSettings rs, ForgeCLRSettings editorSettings)
        {
            var config = rs.GetModuleConfig<HCLRRuntimeConfigSO>();
            if (config == null) return;
            CreateDir(config.HotUpdateDllCopyDirectory);
            CreateDir(config.MetadataDllCopyDirectory);
            AssetDatabase.Refresh();
        }

        public override void OnPreBuildResource(BuildTarget target)
        {
            var settings = ForgeCLRSettings.instance;
            var rs = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            if (!IsEnabled(rs)) return;

            var installer = new InstallerController();
            if (!installer.HasInstalledHybridCLR())
                throw new BuildFailedException("HybridCLR 尚未执行 Installer。");

            Debug.Log("[ForgeCLR] 编译 HybridCLR 热更新 DLL。");
            CompileDllCommand.CompileDll(target, EditorUserBuildSettings.development);

            Debug.Log("[ForgeCLR] 拷贝热更新 DLL。");
            CopyHotUpdateDllToFolder.CopyAssemblies(target, false);
            AssetDatabase.Refresh();
        }

        public override void OnPreBuildPlayer(BuildTarget target)
        {
            var rs = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            if (!IsEnabled(rs)) return;

            Debug.Log("[ForgeCLR] HybridCLR Generate/All。");
            PrebuildCommand.GenerateAll();
        }

        private static void CreateDir(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static void UpdateLocations(HCLRRuntimeConfigSO config, CopyHotUpdateDllToFolder.CopyResult result)
        {
            if (result == null) return;
            Func<string, string> n = p => (p ?? "").Replace("\\", "/");

            var hl = n(config.HotUpdateDllCopyDirectory);
            var ml = n(config.MetadataDllCopyDirectory);

            config.SetHotUpdateDllLocations(
                result.CopiedAssetFiles.Select(n)
                    .Where(p => p.StartsWith(hl, StringComparison.OrdinalIgnoreCase))
                    .Where(p => p.EndsWith(".dll.bytes", StringComparison.OrdinalIgnoreCase))
                    .ToArray());

            config.SetAotMetadataDllLocations(
                result.CopiedAssetFiles.Select(n)
                    .Where(p => p.StartsWith(ml, StringComparison.OrdinalIgnoreCase))
                    .Where(p => p.EndsWith(".dll.bytes", StringComparison.OrdinalIgnoreCase))
                    .ToArray());
        }
    }
}
