using UnityEditor;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// ForgeCLR Project Settings 面板。
    /// </summary>
    public sealed class ForgeCLRSettingsProvider : SettingsProvider
    {
        /// <summary>
        /// Project Settings 中的页面路径。
        /// </summary>
        public const string SettingsPath = "Project/VoyageForge/00 ForgeCLR";

        /// <summary>
        /// 当前面板绑定的序列化配置对象。
        /// </summary>
        private SerializedObject settingsObject;

        /// <summary>
        /// 创建 ForgeCLR 设置面板。
        /// </summary>
        public ForgeCLRSettingsProvider() : base(SettingsPath, SettingsScope.Project)
        {
            label = "ForgeCLR";
        }

        /// <summary>
        /// 面板激活时创建序列化对象。
        /// </summary>
        /// <param name="searchContext">搜索上下文。</param>
        /// <param name="rootElement">根元素。</param>
        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            settingsObject = new SerializedObject(ForgeCLRSettings.instance);
        }

        /// <summary>
        /// 面板关闭时保存配置。
        /// </summary>
        public override void OnDeactivate()
        {
            settingsObject?.ApplyModifiedProperties();
            ForgeCLRSettings.instance.SaveSettings();
            settingsObject?.Dispose();
            settingsObject = null;
        }

        /// <summary>
        /// 绘制 ForgeCLR Project Settings 面板。
        /// </summary>
        /// <param name="searchContext">搜索上下文。</param>
        public override void OnGUI(string searchContext)
        {
            if (settingsObject == null)
                settingsObject = new SerializedObject(ForgeCLRSettings.instance);

            settingsObject.Update();

            EditorGUILayout.LabelField("DLL 拷贝", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(settingsObject.FindProperty("runtimeSettings"), new GUIContent("运行时配置 SO"));
            EditorGUILayout.PropertyField(settingsObject.FindProperty("hotUpdateDllCopyDirectory"), new GUIContent("热更新 DLL 拷贝目录"));
            EditorGUILayout.PropertyField(settingsObject.FindProperty("metadataDllCopyDirectory"), new GUIContent("AOT 元数据 DLL 拷贝目录"));

            settingsObject.ApplyModifiedProperties();
            ForgeCLRSettings.instance.SaveSettings();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("验证环境"))
                    ForgeCLRQuickSetup.ValidateConfiguration();
                if (GUILayout.Button("拷贝 DLL"))
                    CopyHotUpdateDllToFolder.Execute();
                if (GUILayout.Button("构建资源包"))
                    ForgeCLRBuildPipeline.BuildResourcePackage();
                if (GUILayout.Button("打开 Unity Build 面板"))
                    ForgeCLRBuildPipeline.OpenUnityBuildPanel();
            }
        }

        /// <summary>
        /// 创建 ForgeCLR Project Settings Provider。
        /// </summary>
        /// <returns>ForgeCLR 设置面板实例。</returns>
        [SettingsProvider]
        public static SettingsProvider CreateForgeCLRSettingsProvider()
        {
            return new ForgeCLRSettingsProvider
            {
                keywords = GetSearchKeywordsFromGUIContentProperties<ForgeCLRSettings>()
            };
        }
    }
}
