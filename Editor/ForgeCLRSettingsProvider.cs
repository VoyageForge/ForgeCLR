using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

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
        /// ForgeCLR 设置面板 UXML 路径。
        /// </summary>
        private const string UxmlPath = "Assets/ForgeCLR/Editor/UITK/ForgeCLRSettings.uxml";


        /// <summary>
        /// 当前面板绑定的序列化配置对象。
        /// </summary>
        private SerializedObject settingsObject;

        /// <summary>
        /// 当前面板绑定的 ForgeCLR 运行时配置序列化对象。
        /// </summary>
        private SerializedObject runtimeSettingsObject;

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
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            ForgeCLRRuntimeSettingsEditorUtility.EnsureRuntimeSettingsAsset();
            settingsObject = new SerializedObject(ForgeCLRSettings.instance);
            BuildUi(rootElement);
        }

        /// <summary>
        /// 面板关闭时保存配置。
        /// </summary>
        public override void OnDeactivate()
        {
            settingsObject?.ApplyModifiedProperties();
            runtimeSettingsObject?.ApplyModifiedProperties();
            ForgeCLRSettings.instance.SaveSettings();
            runtimeSettingsObject?.Dispose();
            runtimeSettingsObject = null;
            settingsObject?.Dispose();
            settingsObject = null;
        }

        /// <summary>
        /// 构建 ForgeCLR Project Settings 的 UI Toolkit 界面。
        /// </summary>
        /// <param name="rootElement">根元素。</param>
        private void BuildUi(VisualElement rootElement)
        {
            rootElement.Clear();

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                rootElement.Add(new Label("ForgeCLR Settings UXML not found."));
                return;
            }

            visualTree.CloneTree(rootElement);
            rootElement.style.flexGrow = 1;

            var scrollView = rootElement.Q<ScrollView>("RootScrollView");
            var contentRoot = rootElement.Q<VisualElement>("Root");

            if (scrollView != null)
            {
                scrollView.style.flexGrow = 1;
            }

            if (contentRoot != null)
            {
                contentRoot.style.flexGrow = 1;
            }

            settingsObject.Update();
            rootElement.Bind(settingsObject);
            MakeRuntimeSettingsReferenceReadOnly(rootElement);
            UpdateResolvedPathLabels(rootElement);
            BindRuntimeSettingsFields(rootElement);

            RegisterSaveCallbacks(rootElement);
            BindActionButtons(rootElement);
            RenderValidationReport(rootElement, ForgeCLRQuickSetup.CreateValidationReport());
        }

        /// <summary>
        /// 将运行时配置资产引用设为只读，引用由快速设置自动创建和维护。
        /// </summary>
        /// <param name="rootElement">根元素。</param>
        private static void MakeRuntimeSettingsReferenceReadOnly(VisualElement rootElement)
        {
            rootElement.Q<PropertyField>("RuntimeSettingsField")?.SetEnabled(false);
        }

        /// <summary>
        /// 绑定运行时配置字段，方便在 ForgeCLR 面板中选择包名和首场景。
        /// </summary>
        /// <param name="rootElement">根元素。</param>
        private void BindRuntimeSettingsFields(VisualElement rootElement)
        {
            var packageContainer = rootElement.Q<VisualElement>("PackageFieldsContainer");
            var startupSceneContainer = rootElement.Q<VisualElement>("StartupSceneFieldsContainer");
            packageContainer?.Clear();
            startupSceneContainer?.Clear();

            if (packageContainer == null && startupSceneContainer == null)
            {
                return;
            }

            runtimeSettingsObject?.Dispose();
            runtimeSettingsObject = null;

            var runtimeSettings = ForgeCLRSettings.instance.RuntimeSettings;
            if (runtimeSettings == null)
            {
                packageContainer?.Add(new HelpBox("未引用 ForgeCLRRuntimeSettings，无法选择 YooAssets 包。", HelpBoxMessageType.Warning));
                startupSceneContainer?.Add(new HelpBox("未引用 ForgeCLRRuntimeSettings，无法编辑启动场景配置。", HelpBoxMessageType.Warning));
                return;
            }

            runtimeSettingsObject = new SerializedObject(runtimeSettings);
            AddPackageDropdown(packageContainer);
            AddRuntimeSettingsField(startupSceneContainer, "loadStartupScene", "启动后加载首场景");
            AddStartupSceneDropdown(startupSceneContainer);
            packageContainer?.Bind(runtimeSettingsObject);
            startupSceneContainer?.Bind(runtimeSettingsObject);
        }

        /// <summary>
        /// 向面板中添加运行时配置字段。
        /// </summary>
        /// <param name="container">字段容器。</param>
        /// <param name="propertyName">序列化字段名称。</param>
        /// <param name="label">显示标签。</param>
        private void AddRuntimeSettingsField(VisualElement container, string propertyName, string label)
        {
            if (container == null)
            {
                return;
            }

            var property = runtimeSettingsObject?.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            var field = new PropertyField(property, label);
            field.AddToClassList("fclr-settings-field");
            field.RegisterCallback<SerializedPropertyChangeEvent>(_ => SaveSettings());
            container.Add(field);
        }

        /// <summary>
        /// 添加 YooAssets 包名下拉框，包名来自项目中的 YooAssets Collector 配置。
        /// </summary>
        /// <param name="container">字段容器。</param>
        private void AddPackageDropdown(VisualElement container)
        {
            if (container == null)
            {
                return;
            }

            var property = runtimeSettingsObject?.FindProperty("packageName");
            if (property == null)
            {
                return;
            }

            if (ForgeCLRRuntimeSettingsEditorUtility.TryGetYooAssetCollectorSetting(out _) == false)
            {
                container.Add(new HelpBox("未找到 YooAssets Collector 配置，请先执行 ForgeCLR 快速设置或在 YooAssets 中创建配置。", HelpBoxMessageType.Warning));
                return;
            }

            var choices = ForgeCLRRuntimeSettingsEditorUtility.GetYooAssetPackageNames().ToList();
            if (choices.Count == 0)
            {
                container.Add(new HelpBox("YooAssets Collector 配置中没有 Package，请先执行 ForgeCLR 快速设置。", HelpBoxMessageType.Warning));
                return;
            }

            if (string.IsNullOrWhiteSpace(property.stringValue) == false && choices.Contains(property.stringValue) == false)
            {
                choices.Insert(0, property.stringValue);
            }

            var currentValue = choices.Contains(property.stringValue) ? property.stringValue : choices[0];
            property.stringValue = currentValue;

            var dropdown = new PopupField<string>("资源包名称", choices, currentValue);
            dropdown.AddToClassList("fclr-settings-field");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                property.stringValue = evt.newValue;
                SaveSettings();
            });
            container.Add(dropdown);
        }

        /// <summary>
        /// 添加首场景下拉框，避免手动填写场景地址。
        /// </summary>
        /// <param name="container">字段容器。</param>
        private void AddStartupSceneDropdown(VisualElement container)
        {
            if (container == null)
            {
                return;
            }

            var property = runtimeSettingsObject?.FindProperty("startupSceneLocation");
            if (property == null)
            {
                return;
            }

            var choices = ForgeCLRRuntimeSettingsEditorUtility.GetAvailableStartupSceneLocations().ToList();
            if (choices.Count == 0)
            {
                choices.Add(ForgeCLRRuntimeSettingsEditorUtility.DefaultStartupScenePath);
            }

            if (string.IsNullOrWhiteSpace(property.stringValue) == false && choices.Contains(property.stringValue) == false)
            {
                choices.Insert(0, property.stringValue);
            }

            var currentValue = choices.Contains(property.stringValue) ? property.stringValue : choices[0];
            property.stringValue = currentValue;

            var dropdown = new PopupField<string>("启动场景地址", choices, currentValue);
            dropdown.AddToClassList("fclr-settings-field");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                property.stringValue = evt.newValue;
                SaveSettings();
            });
            container.Add(dropdown);
        }

        /// <summary>
        /// 为所有属性字段注册保存回调。
        /// </summary>
        /// <param name="rootElement">根元素。</param>
        private void RegisterSaveCallbacks(VisualElement rootElement)
        {
            rootElement.Query<PropertyField>().ForEach(field =>
            {
                field.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
                {
                    SaveSettings();
                    UpdateResolvedPathLabels(rootElement);
                    if (field.bindingPath == "runtimeSettings")
                    {
                        rootElement.schedule.Execute(() => BindRuntimeSettingsFields(rootElement));
                    }

                    RenderValidationReport(rootElement, ForgeCLRQuickSetup.CreateValidationReport());
                });
            });
        }

        /// <summary>
        /// 绑定设置面板中的操作按钮。
        /// </summary>
        /// <param name="rootElement">根元素。</param>
        private static void BindActionButtons(VisualElement rootElement)
        {
            rootElement.Q<Button>("ValidateButton")?.RegisterCallback<ClickEvent>(_ =>
                RenderValidationReport(rootElement, ForgeCLRQuickSetup.CreateValidationReport()));
            rootElement.Q<Button>("CopyDllButton")?.RegisterCallback<ClickEvent>(_ =>
                CopyHotUpdateDllToFolder.Execute());
            rootElement.Q<Button>("BuildResourcesButton")?.RegisterCallback<ClickEvent>(_ =>
                ForgeCLRBuildPipeline.BuildResourcePackage());
            rootElement.Q<Button>("OpenBuildPanelButton")?.RegisterCallback<ClickEvent>(_ =>
                ForgeCLRBuildPipeline.OpenUnityBuildPanel());
        }

        /// <summary>
        /// 渲染 ForgeCLR 环境检测报告。
        /// </summary>
        /// <param name="rootElement">根元素。</param>
        /// <param name="report">环境检测报告。</param>
        private static void RenderValidationReport(VisualElement rootElement, ForgeCLRValidationReport report)
        {
            var summaryLabel = rootElement.Q<Label>("ValidationSummary");
            if (summaryLabel != null)
            {
                summaryLabel.text = report.FailedCount > 0
                    ? $"发现 {report.FailedCount} 个错误，{report.WarningCount} 个警告"
                    : report.WarningCount > 0
                        ? $"检测通过，但有 {report.WarningCount} 个警告"
                        : "全部检测通过";
            }

            var container = rootElement.Q<VisualElement>("ValidationCardsContainer");
            if (container == null)
            {
                return;
            }

            container.Clear();
            foreach (var item in report.Items)
            {
                container.Add(CreateValidationCard(rootElement, item));
            }
        }

        /// <summary>
        /// 创建单张环境检测卡片。
        /// </summary>
        /// <param name="rootElement">根元素，用于修复后刷新报告。</param>
        /// <param name="item">检测项。</param>
        /// <returns>检测卡片元素。</returns>
        private static VisualElement CreateValidationCard(VisualElement rootElement, ForgeCLRValidationItem item)
        {
            var card = new VisualElement();
            card.AddToClassList("fclr-validation-card");
            card.AddToClassList(GetValidationCardClass(item.Status));

            var header = new VisualElement();
            header.AddToClassList("fclr-validation-card-header");

            var title = new Label(item.Title);
            title.AddToClassList("fclr-validation-title");

            var badge = new Label(GetValidationStatusText(item.Status));
            badge.AddToClassList("fclr-validation-badge");

            header.Add(title);

            var actions = new VisualElement();
            actions.AddToClassList("fclr-validation-actions");
            actions.Add(badge);

            if (item.Status != ForgeCLRValidationStatus.Passed &&
                ForgeCLRQuickSetup.CanRepairValidationItem(item.Title))
            {
                var repairButton = new Button(() => RepairValidationItem(rootElement, item.Title))
                {
                    text = "修复"
                };
                repairButton.AddToClassList("fclr-repair-button");
                actions.Add(repairButton);
            }

            header.Add(actions);

            var message = new TextElement { text = item.Message };
            message.AddToClassList("fclr-validation-message");

            card.Add(header);
            card.Add(message);
            return card;
        }

        /// <summary>
        /// 执行单项环境修复并刷新检测卡片。
        /// </summary>
        /// <param name="rootElement">根元素。</param>
        /// <param name="title">检测项标题。</param>
        private static void RepairValidationItem(VisualElement rootElement, string title)
        {
            try
            {
                ForgeCLRQuickSetup.TryRepairValidationItem(title);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[ForgeCLR] 修复环境检测项失败：{title}\n{exception}");
            }

            RenderValidationReport(rootElement, ForgeCLRQuickSetup.CreateValidationReport());
        }

        /// <summary>
        /// 获取检测卡片样式类。
        /// </summary>
        /// <param name="status">检测状态。</param>
        /// <returns>样式类名。</returns>
        private static string GetValidationCardClass(ForgeCLRValidationStatus status)
        {
            return status switch
            {
                ForgeCLRValidationStatus.Passed => "fclr-validation-card-passed",
                ForgeCLRValidationStatus.Warning => "fclr-validation-card-warning",
                ForgeCLRValidationStatus.Failed => "fclr-validation-card-failed",
                _ => "fclr-validation-card-warning"
            };
        }

        /// <summary>
        /// 获取检测状态展示文本。
        /// </summary>
        /// <param name="status">检测状态。</param>
        /// <returns>状态文本。</returns>
        private static string GetValidationStatusText(ForgeCLRValidationStatus status)
        {
            return status switch
            {
                ForgeCLRValidationStatus.Passed => "通过",
                ForgeCLRValidationStatus.Warning => "警告",
                ForgeCLRValidationStatus.Failed => "未通过",
                _ => "未知"
            };
        }

        /// <summary>
        /// 保存当前 ForgeCLR Project Settings 配置。
        /// </summary>
        private void SaveSettings()
        {
            settingsObject?.ApplyModifiedProperties();
            if (runtimeSettingsObject != null)
            {
                runtimeSettingsObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(runtimeSettingsObject.targetObject);
                AssetDatabase.SaveAssets();
            }

            ForgeCLRSettings.instance.SaveSettings();
            settingsObject?.Update();
            runtimeSettingsObject?.Update();
        }

        /// <summary>
        /// 更新 DLL 拷贝最终路径展示。
        /// </summary>
        /// <param name="rootElement">根元素。</param>
        private static void UpdateResolvedPathLabels(VisualElement rootElement)
        {
            var settings = ForgeCLRSettings.instance;

            var hotUpdateLabel = rootElement.Q<Label>("HotUpdateDllCopyPathLabel");
            if (hotUpdateLabel != null)
            {
                hotUpdateLabel.text = $"热更新 DLL：{settings.HotUpdateDllCopyDirectory}";
            }

            var metadataLabel = rootElement.Q<Label>("MetadataDllCopyPathLabel");
            if (metadataLabel != null)
            {
                metadataLabel.text = $"AOT 元数据 DLL：{settings.MetadataDllCopyDirectory}";
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
