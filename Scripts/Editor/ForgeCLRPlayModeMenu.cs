using System;
using UnityEditor;
using UnityEngine;
using VoyageForge.ForgeCLR.Runtime;
using YooAsset;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// ForgeCLR PlayMode 快速切换菜单。
    /// </summary>
    public static class ForgeCLRPlayModeMenu
    {
        /// <summary>
        /// PlayMode 快速切换菜单路径。
        /// </summary>
        private const string PlayModeMenuPath = "VoyageForge/ForgeCLR/PlayMode/选择当前模式";

        /// <summary>
        /// 打开 PlayMode 快速切换窗口。
        /// </summary>
        [MenuItem(PlayModeMenuPath, false, 101)]
        private static void ShowPlayModeMenu()
        {
            var settings = ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            if (settings == null)
            {
                Debug.LogError("未找到或无法创建 ForgeCLRRuntimeSettings 配置资源。请先打开 ForgeCLR Project Settings 检查配置。");
                return;
            }

            ForgeCLRPlayModeQuickSwitchWindow.Open(settings);
        }
    }

    /// <summary>
    /// ForgeCLR PlayMode 快速切换窗口。
    /// </summary>
    internal sealed class ForgeCLRPlayModeQuickSwitchWindow : EditorWindow
    {
        /// <summary>
        /// 当前编辑的运行时配置资产。
        /// </summary>
        private ForgeCLRRuntimeSettings settings;

        /// <summary>
        /// 打开窗口并绑定运行时配置资产。
        /// </summary>
        /// <param name="settings">运行时配置资产。</param>
        public static void Open(ForgeCLRRuntimeSettings settings)
        {
            var window = CreateInstance<ForgeCLRPlayModeQuickSwitchWindow>();
            window.settings = settings;
            window.titleContent = new GUIContent("快速切换 PlayMode");
            window.minSize = new Vector2(280f, 140f);
            window.maxSize = new Vector2(380f, 360f);
            window.ShowUtility();
        }

        /// <summary>
        /// 绘制窗口内容。
        /// </summary>
        private void OnGUI()
        {
            if (settings == null)
            {
                EditorGUILayout.HelpBox("未找到 ForgeCLRRuntimeSettings 配置资源。", MessageType.Warning);
                if (GUILayout.Button("打开配置"))
                {
                    ForgeCLRQuickSetup.OpenConfigurationWindow();
                    Close();
                }

                return;
            }

            var serializedObject = new SerializedObject(settings);
            var playModeProperty = serializedObject.FindProperty("playMode");
            var currentPlayMode = (EPlayMode)playModeProperty.intValue;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("快速切换 PlayMode", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("点击下面的模式名称即可立即写入运行时配置 SO。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(8f);

            foreach (EPlayMode playMode in Enum.GetValues(typeof(EPlayMode)))
            {
                using (new EditorGUI.DisabledScope(currentPlayMode == playMode))
                {
                    string buttonText = currentPlayMode == playMode ? $"当前模式：{playMode}" : playMode.ToString();
                    if (GUILayout.Button(buttonText, GUILayout.Height(28f)))
                    {
                        SetPlayMode(serializedObject, playModeProperty, playMode);
                        Close();
                    }
                }
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("打开配置面板"))
            {
                ForgeCLRQuickSetup.OpenConfigurationWindow();
                Close();
            }
        }

        /// <summary>
        /// 写入新的 YooAssets PlayMode。
        /// </summary>
        /// <param name="serializedObject">运行时配置序列化对象。</param>
        /// <param name="playModeProperty">PlayMode 序列化属性。</param>
        /// <param name="playMode">新的 PlayMode。</param>
        private void SetPlayMode(SerializedObject serializedObject, SerializedProperty playModeProperty, EPlayMode playMode)
        {
            serializedObject.Update();
            playModeProperty.intValue = (int)playMode;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ForgeCLR] 当前 PlayMode 已切换为：{playMode}");
        }
    }
}
