using System;
using UnityEditor;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor.Tools
{
    /// <summary>
    /// 自动启用 Sprite Atlas 
    /// 
    /// </summary>
    [InitializeOnLoad]
    public class SpriteAtlasAutoEnable
    {
        static SpriteAtlasAutoEnable()
        {
            // 订阅编辑器播放模式状态变化事件
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }


        private static void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            switch (obj)
            {
                case PlayModeStateChange.EnteredEditMode:
                    // 恢复之前的 Sprite Packer 模式
                    UnityEditor.EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2Build;

                    break;
                case PlayModeStateChange.ExitingEditMode:

                    // 自动启用 Sprite Atlas
                    EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
                    break;
                //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] 调用单例，会比此信号来的早
                case PlayModeStateChange.EnteredPlayMode:

                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    // 恢复之前的 Sprite Packer 模式
                    UnityEditor.EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2Build;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(obj), obj, null);
            }
        }
    }
}