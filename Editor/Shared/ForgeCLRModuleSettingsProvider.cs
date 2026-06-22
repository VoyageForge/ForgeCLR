using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// FCLR 子模块 Provider 抽象基类。
    /// 操作目标为 ForgeCLRRuntimeSettings（Resources 中的运行时配置）。
    /// 子 SO 作为 RuntimeSettings 的子物体存储，List 中存在即启用。
    /// </summary>
    public abstract class ForgeCLRModuleSettingsProvider
    {
        public abstract string ModuleId { get; }
        public abstract string DisplayName { get; }
        public virtual string[] Dependencies => Array.Empty<string>();

        public abstract Type RuntimeConfigType { get; }

        public abstract VisualElement CreateModuleUI(
            ForgeCLR.Runtime.ForgeCLRRuntimeSettings runtimeSettings,
            ForgeCLRSettings editorSettings);

        public abstract IForgeCLRValidationCheck[] CreateValidationChecks(
            ForgeCLR.Runtime.ForgeCLRRuntimeSettings runtimeSettings);

        public abstract void ExecuteQuickSetup(
            ForgeCLR.Runtime.ForgeCLRRuntimeSettings runtimeSettings,
            ForgeCLRSettings editorSettings);

        // ===== 通用实现 =====

        public bool IsInstalled(ForgeCLR.Runtime.ForgeCLRRuntimeSettings rs)
        {
            var method = typeof(ForgeCLR.Runtime.ForgeCLRRuntimeSettings)
                .GetMethod("GetModuleConfig")!
                .MakeGenericMethod(RuntimeConfigType);
            return method.Invoke(rs, null) != null;
        }

        /// <summary>已安装且 enabled 字段为 true。</summary>
        public bool IsEnabled(ForgeCLR.Runtime.ForgeCLRRuntimeSettings rs)
        {
            var method = typeof(ForgeCLR.Runtime.ForgeCLRRuntimeSettings)
                .GetMethod("GetModuleConfig")!
                .MakeGenericMethod(RuntimeConfigType);
            var config = method.Invoke(rs, null) as ScriptableObject;
            if (config == null) return false;

            // 动态读取 .Enabled 属性
            var enabledProp = RuntimeConfigType.GetProperty("Enabled");
            if (enabledProp != null)
                return (bool)enabledProp.GetValue(config);
            return true; // 无 Enabled 属性时默认启用
        }

        /// <summary>
        /// 启用模块：创建子 SO 作为 RuntimeSettings 的子物体，加入 List。
        /// </summary>
        public virtual void Enable(
            ForgeCLR.Runtime.ForgeCLRRuntimeSettings runtimeSettings,
            ForgeCLRSettings editorSettings)
        {
            if (IsEnabled(runtimeSettings)) return;

            var config = (ScriptableObject)ScriptableObject.CreateInstance(RuntimeConfigType);
            config.name = RuntimeConfigType.Name;

            var rsPath = AssetDatabase.GetAssetPath(runtimeSettings);
            if (!string.IsNullOrEmpty(rsPath))
                AssetDatabase.AddObjectToAsset(config, rsPath);

            // 通过反射调用 SetModuleConfig<T>
            var method = typeof(ForgeCLR.Runtime.ForgeCLRRuntimeSettings)
                .GetMethod("SetModuleConfig")!
                .MakeGenericMethod(RuntimeConfigType);
            method.Invoke(runtimeSettings, new object[] { config });

            EditorUtility.SetDirty(runtimeSettings);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 禁用模块：从 List 移除并从 RuntimeSettings 删除子物体。
        /// </summary>
        public virtual void Disable(
            ForgeCLR.Runtime.ForgeCLRRuntimeSettings runtimeSettings,
            ForgeCLRSettings editorSettings)
        {
            // 通过反射调用 SetModuleConfig<T>(null)
            var method = typeof(ForgeCLR.Runtime.ForgeCLRRuntimeSettings)
                .GetMethod("SetModuleConfig")!
                .MakeGenericMethod(RuntimeConfigType);

            // 先通过反射取出引用再清掉 list
            var getMethod = typeof(ForgeCLR.Runtime.ForgeCLRRuntimeSettings)
                .GetMethod("GetModuleConfig")!
                .MakeGenericMethod(RuntimeConfigType);
            var existing = getMethod.Invoke(runtimeSettings, null) as ScriptableObject;

            method.Invoke(runtimeSettings, new object[] { null });

            if (existing != null)
            {
                AssetDatabase.RemoveObjectFromAsset(existing);
                Object.DestroyImmediate(existing, true);
            }

            EditorUtility.SetDirty(runtimeSettings);
            AssetDatabase.SaveAssets();
        }

        /// <summary>创建与 UXML 一致的 section card。</summary>
        protected static VisualElement CreateSectionCard(string title)
        {
            var card = new VisualElement();
            card.AddToClassList("fclr-section-card");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("fclr-section-title");
            card.Add(titleLabel);
            return card;
        }

        // ===== 构建回调 =====

        public virtual void OnPreBuildResource(BuildTarget target) { }
        public virtual void OnPostBuildResource(BuildTarget target) { }
        public virtual void OnPreBuildPlayer(BuildTarget target) { }
    }

    /// <summary>
    /// 泛型版本：自动推导 RuntimeConfigType。
    /// </summary>
    public abstract class ForgeCLRModuleSettingsProvider<T> : ForgeCLRModuleSettingsProvider
        where T : ScriptableObject
    {
        public override Type RuntimeConfigType => typeof(T);
    }
}
