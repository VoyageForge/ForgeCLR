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
            BindFileServerFields(rootElement);
            UpdateFileServerStatusLabels(rootElement);

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
            var launcherSceneContainer = rootElement.Q<VisualElement>("LauncherSceneFieldsContainer");
            var startupSceneContainer = rootElement.Q<VisualElement>("StartupSceneFieldsContainer");
            packageContainer?.Clear();
            launcherSceneContainer?.Clear();
            startupSceneContainer?.Clear();

            if (packageContainer == null && launcherSceneContainer == null && startupSceneContainer == null)
            {
                return;
            }

            runtimeSettingsObject?.Dispose();
            runtimeSettingsObject = null;

            var runtimeSettings = ForgeCLRSettings.instance.RuntimeSettings;
            if (runtimeSettings == null)
            {
                packageContainer?.Add(new HelpBox("未引用 ForgeCLRRuntimeSettings，无法选择 YooAssets 包。", HelpBoxMessageType.Warning));
                AddLauncherSceneDropdown(launcherSceneContainer);
                startupSceneContainer?.Add(new HelpBox("未引用 ForgeCLRRuntimeSettings，无法编辑启动场景配置。", HelpBoxMessageType.Warning));
                return;
            }

            runtimeSettingsObject = new SerializedObject(runtimeSettings);
            AddPackageDropdown(packageContainer);
            AddLauncherSceneDropdown(launcherSceneContainer);
            AddRuntimeSettingsField(startupSceneContainer, "loadStartupScene", "启动后加载首场景");
            AddRuntimeSettingsField(startupSceneContainer, "enableAutoOfflineFallback", "网络失败时自动使用离线模式");
            AddStartupSceneDropdown(startupSceneContainer);
            packageContainer?.Bind(runtimeSettingsObject);
            startupSceneContainer?.Bind(runtimeSettingsObject);
        }

        /// <summary>
        /// 添加软件包 Launcher 场景下拉框，用于控制 Build Settings 的第一场景。
        /// </summary>
        /// <param name="container">字段容器。</param>
        private void AddLauncherSceneDropdown(VisualElement container)
        {
            if (container == null)
            {
                return;
            }

            var choices = ForgeCLRRuntimeSettingsEditorUtility.GetAvailableStartupSceneLocations().ToList();
            if (choices.Count == 0)
            {
                container.Add(new HelpBox("项目中未找到任何场景文件，请先创建场景。", HelpBoxMessageType.Warning));
                return;
            }

            var settings = ForgeCLRSettings.instance;
            if (string.IsNullOrWhiteSpace(settings.LauncherSceneLocation) == false &&
                choices.Contains(settings.LauncherSceneLocation) == false)
            {
                choices.Insert(0, settings.LauncherSceneLocation);
            }

            var currentValue = choices.Contains(settings.LauncherSceneLocation)
                ? settings.LauncherSceneLocation
                : choices[0];

            var dropdown = new PopupField<string>("软件包首场景", choices, currentValue);
            dropdown.AddToClassList("fclr-settings-field");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                settings.SetLauncherSceneLocation(evt.newValue);
                ForgeCLRValidationUtility.EnsureLauncherSceneInBuildSettings();
                settingsObject?.Update();
            });
            container.Add(dropdown);
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
                container.Add(new HelpBox("项目中未找到任何场景文件，请先创建启动场景。", HelpBoxMessageType.Warning));
                return;
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
            rootElement.Q<Button>("BuildPlayerButton")?.RegisterCallback<ClickEvent>(_ =>
                ForgeCLRBuildPipeline.BuildPlayerPackage());
            rootElement.Q<Button>("OpenBuildPanelButton")?.RegisterCallback<ClickEvent>(_ =>
                ForgeCLRBuildPipeline.OpenUnityBuildPanel());
            rootElement.Q<Button>("OpenFileServerWindowButton")?.RegisterCallback<ClickEvent>(_ =>
                VoyageForgeFileServerWindow.Open());
            rootElement.Q<Button>("StartFileServerButton")?.RegisterCallback<ClickEvent>(_ =>
            {
                var settings = ForgeCLRSettings.instance;
                VoyageForgeFileServerSingleton.StartServer(settings.FileServerRootDirectory, settings.FileServerPort, settings.FileServerBindIPAddress);
                UpdateFileServerStatusLabels(rootElement);
                RenderValidationReport(rootElement, ForgeCLRQuickSetup.CreateValidationReport());
            });
            rootElement.Q<Button>("StopFileServerButton")?.RegisterCallback<ClickEvent>(_ =>
            {
                VoyageForgeFileServerSingleton.StopServer(permanent: true);
                UpdateFileServerStatusLabels(rootElement);
                RenderValidationReport(rootElement, ForgeCLRQuickSetup.CreateValidationReport());
            });
        }

        /// <summary>
        /// 绑定局域网文件服务器 Project Settings 字段。
        /// </summary>
        /// <param name="rootElement">根元素。</param>
        private void BindFileServerFields(VisualElement rootElement)
        {
            var container = rootElement.Q<VisualElement>("FileServerFieldsContainer");
            if (container == null)
            {
                return;
            }

            container.Clear();
            var settings = ForgeCLRSettings.instance;

            var rootRow = new VisualElement();
            rootRow.AddToClassList("fclr-inline-row");

            var rootField = new TextField("根目录") { value = settings.FileServerRootDirectory };
            rootField.AddToClassList("fclr-inline-grow");
            rootField.RegisterValueChangedCallback(evt =>
            {
                settings.SetFileServerConfig(evt.newValue, settings.FileServerPort, settings.FileServerBindIPAddress);
                settingsObject?.Update();
                RenderValidationReport(rootElement, ForgeCLRQuickSetup.CreateValidationReport());
            });
            rootRow.Add(rootField);

            var chooseButton = new Button(() =>
            {
                var selected = EditorUtility.OpenFolderPanel("选择 VoyageForge 文件服务器根目录", settings.FileServerRootDirectory, string.Empty);
                if (string.IsNullOrWhiteSpace(selected))
                {
                    return;
                }

                settings.SetFileServerConfig(selected, settings.FileServerPort, settings.FileServerBindIPAddress);
                BindFileServerFields(rootElement);
                RenderValidationReport(rootElement, ForgeCLRQuickSetup.CreateValidationReport());
            })
            {
                text = "选择"
            };
            chooseButton.AddToClassList("fclr-small-button");
            rootRow.Add(chooseButton);
            container.Add(rootRow);

            var portRow = new VisualElement();
            portRow.AddToClassList("fclr-inline-row");
            var portField = new IntegerField("端口") { value = settings.FileServerPort };
            portField.AddToClassList("fclr-inline-grow");
            portField.RegisterValueChangedCallback(evt =>
            {
                settings.SetFileServerConfig(settings.FileServerRootDirectory, evt.newValue, settings.FileServerBindIPAddress);
                settingsObject?.Update();
                RenderValidationReport(rootElement, ForgeCLRQuickSetup.CreateValidationReport());
            });
            portRow.Add(portField);

            var portButton = new Button(() =>
            {
                var port = VoyageForgeFileServer.FindAvailablePort(settings.FileServerPort);
                if (port > 0)
                {
                    settings.SetFileServerConfig(settings.FileServerRootDirectory, port, settings.FileServerBindIPAddress);
                    BindFileServerFields(rootElement);
                    RenderValidationReport(rootElement, ForgeCLRQuickSetup.CreateValidationReport());
                }
            })
            {
                text = "自动端口"
            };
            portButton.AddToClassList("fclr-small-button");
            portRow.Add(portButton);
            container.Add(portRow);

            AddFileServerIpDropdown(container, rootElement);

            var autoRestart = new Toggle("域重载后自动恢复") { value = settings.FileServerAutoRestart };
            autoRestart.AddToClassList("fclr-settings-field");
            autoRestart.RegisterValueChangedCallback(evt =>
            {
                settings.SetFileServerAutoRestart(evt.newValue);
                settingsObject?.Update();
            });
            container.Add(autoRestart);
        }

        /// <summary>
        /// 添加文件服务器绑定 IP 下拉框。
        /// </summary>
        /// <param name="container">字段容器。</param>
        /// <param name="rootElement">根元素。</param>
        private void AddFileServerIpDropdown(VisualElement container, VisualElement rootElement)
        {
            var settings = ForgeCLRSettings.instance;
            var options = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("0.0.0.0 - 监听所有网卡，推荐", string.Empty)
            };

            options.AddRange(VoyageForgeFileServer.GetAllLocalIPv4().Select(info =>
            {
                var tag = info.IsVirtualLike ? "疑似虚拟网卡" : "推荐";
                return new KeyValuePair<string, string>(
                    $"{info.Address} - {info.Name} - {info.NetworkType} - {tag}",
                    info.Address);
            }));

            var choices = options.Select(option => option.Key).ToList();
            var currentOption = options.FirstOrDefault(option => option.Value == settings.FileServerBindIPAddress);
            var currentValue = string.IsNullOrWhiteSpace(currentOption.Key) ? choices[0] : currentOption.Key;

            var dropdown = new PopupField<string>("绑定 IP", choices, currentValue);
            dropdown.AddToClassList("fclr-settings-field");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var option = options.FirstOrDefault(candidate => candidate.Key == evt.newValue);
                settings.SetFileServerConfig(settings.FileServerRootDirectory, settings.FileServerPort, option.Value ?? string.Empty);
                settingsObject?.Update();
                UpdateFileServerStatusLabels(rootElement);
            });
            container.Add(dropdown);
        }

        /// <summary>
        /// 更新文件服务器状态展示。
        /// </summary>
        /// <param name="rootElement">根元素。</param>
        private static void UpdateFileServerStatusLabels(VisualElement rootElement)
        {
            var server = VoyageForgeFileServerSingleton.Server;
            var settings = ForgeCLRSettings.instance;
            var running = server != null && server.IsRunning;
            var statusLabel = rootElement.Q<Label>("FileServerStatusLabel");
            if (statusLabel != null)
            {
                statusLabel.text = running
                    ? $"运行中：{server.RootDirectory}"
                    : $"未启动：{settings.FileServerRootDirectory}";
            }

            var urlLabel = rootElement.Q<Label>("FileServerUrlLabel");
            if (urlLabel != null)
            {
                var ip = string.IsNullOrWhiteSpace(settings.FileServerBindIPAddress)
                    ? VoyageForgeFileServer.GetRecommendedLocalIPv4()
                    : settings.FileServerBindIPAddress;
                urlLabel.text = running ? $"访问地址：{server.ServerUrl}" : $"预计访问地址：http://{ip}:{settings.FileServerPort}/";
            }
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
