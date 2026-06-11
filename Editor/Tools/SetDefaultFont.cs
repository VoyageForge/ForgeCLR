using UnityEngine;
using UnityEngine.UI;
using UnityEditor;


namespace VoyageForge.ForgeCLR.Editor.Tools
{
    /// <summary>
    /// 设置默认字体
    /// </summary>
    [InitializeOnLoad]
    public static class SetDefaultFont
    {
        // 静态构造函数，在编辑器启动时自动调用
        static SetDefaultFont()
        {
            // 订阅 ObjectFactory 的 componentWasAdded 事件
            ObjectFactory.componentWasAdded += OnComponentWasAdded;
        }

        // 当任何组件被添加到 GameObject 时触发
        private static void OnComponentWasAdded(Component component)
        {
            // 检查添加的是否为 Text 组件
            if (component is Text textComponent)
            {
                // 设置默认字体
                // 注意：这里需要将 "Fonts/YourDefaultFont" 替换为你的字体在 Resources 文件夹下的路径
                Font defaultFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/SourceHanSansSC-Regular.otf");
                if (defaultFont != null)
                {
                    textComponent.font = defaultFont;
                    // 标记组件为已修改状态，以便 Unity 保存更改
                    EditorUtility.SetDirty(textComponent);
                    Debug.Log($"为新创建的Text组件 '{textComponent.name}' 设置了默认字体。");
                }
                else
                {
                    Debug.LogError("未找到默认字体资源，请检查 字体文件是否存在。");
                }
            }
        }
    }
}