using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// VoyageForge 局域网文件服务器窗口。
    /// 窗口使用 UI Toolkit，配置统一读写 ForgeCLR Project Settings。
    /// </summary>
    public sealed class VoyageForgeFileServerWindow : EditorWindow
    {
        /// <summary>
        /// 文件服务器窗口 UXML 布局路径。
        /// </summary>
        private const string UxmlPath = "Assets/ForgeCLR/Editor/FileServer/VoyageForgeFileServerWindow.uxml";

        /// <summary>
        /// 文件服务器窗口 USS 样式路径。
        /// </summary>
        private const string UssPath = "Assets/ForgeCLR/Editor/FileServer/VoyageForgeFileServerWindow.uss";

        /// <summary>
        /// 当前共享的文件服务器实例。
        /// </summary>
        private VoyageForgeFileServer Server => VoyageForgeFileServerSingleton.Server;

        /// <summary>
        /// 当前编辑中的根目录。
        /// </summary>
        private string rootDirectory;

        /// <summary>
        /// 当前编辑中的端口。
        /// </summary>
        private int port;

        /// <summary>
        /// 当前编辑中的绑定 IP；空字符串代表监听所有网卡。
        /// </summary>
        private string bindIPAddress;

        /// <summary>
        /// 本机可选 IPv4 列表。
        /// </summary>
        private List<VoyageForgeIPAddressInfo> ipList = new();

        /// <summary>
        /// 下拉框显示文本到实际 IP 的映射。
        /// </summary>
        private readonly List<KeyValuePair<string, string>> ipOptions = new();

        /// <summary>
        /// 窗口日志内容。
        /// </summary>
        private readonly StringBuilder logBuilder = new();

        private TextField rootDirectoryField;
        private IntegerField portField;
        private PopupField<string> bindIpPopup;
        private Toggle autoRestartToggle;
        private Label statusBadge;
        private Label serverUrlLabel;
        private Label rootStatusLabel;
        private Label bindAddressLabel;
        private Label healthBadge;
        private Label healthMessageLabel;
        private Label ipHelpLabel;
        private TextField logField;

        /// <summary>
        /// 启动按钮；服务器运行时禁用，避免重复 Start。
        /// </summary>
        private Button startButton;

        /// <summary>
        /// 停止按钮；服务器未运行时禁用，避免误导用户。
        /// </summary>
        private Button stopButton;

        /// <summary>
        /// 复制当前访问地址按钮。
        /// 未运行时复制“预计访问地址”，运行时复制服务器真实地址。
        /// </summary>
        private Button copyUrlButton;

        /// <summary>
        /// 浏览器打开当前访问地址按钮。
        /// </summary>
        private Button openUrlButton;

        /// <summary>
        /// 打开 VoyageForge 文件服务器窗口。
        /// </summary>
        [MenuItem("VoyageForge/ForgeCLR/File Server")]
        public static void Open()
        {
            var window = GetWindow<VoyageForgeFileServerWindow>();
            window.titleContent = new GUIContent("VoyageForge File Server");
            window.minSize = new Vector2(720, 620);
            window.Show();
        }

        /// <summary>
        /// 窗口启用时读取 Project Settings 并订阅服务器日志。
        /// </summary>
        private void OnEnable()
        {
            LoadSettings();

            if (Server != null)
            {
                Server.OnLog += AppendLog;
            }
        }

        /// <summary>
        /// 窗口关闭或脚本重载前保存配置并取消日志订阅。
        /// </summary>
        private void OnDisable()
        {
            SaveSettings();

            if (Server != null)
            {
                Server.OnLog -= AppendLog;
            }
        }

        /// <summary>
        /// 创建 UI Toolkit 界面。
        /// </summary>
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            LoadVisualTree();
            QueryElements();
            RefreshIPList();
            BindInitialValues();
            RegisterCallbacks();
            RefreshStatus();
            AppendLog("VoyageForge File Server Window 已打开。");
        }

        /// <summary>
        /// 加载 UXML 和 USS。
        /// </summary>
        private void LoadVisualTree()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                rootVisualElement.Add(new Label($"未找到文件服务器窗口 UXML：{UxmlPath}"));
                return;
            }

            visualTree.CloneTree(rootVisualElement);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
        }

        /// <summary>
        /// 查询 UXML 中需要绑定的控件。
        /// </summary>
        private void QueryElements()
        {
            rootDirectoryField = rootVisualElement.Q<TextField>("RootDirectoryField");
            portField = rootVisualElement.Q<IntegerField>("PortField");
            autoRestartToggle = rootVisualElement.Q<Toggle>("AutoRestartToggle");
            statusBadge = rootVisualElement.Q<Label>("StatusBadge");
            serverUrlLabel = rootVisualElement.Q<Label>("ServerUrlLabel");
            rootStatusLabel = rootVisualElement.Q<Label>("RootStatusLabel");
            bindAddressLabel = rootVisualElement.Q<Label>("BindAddressLabel");
            healthBadge = rootVisualElement.Q<Label>("HealthBadge");
            healthMessageLabel = rootVisualElement.Q<Label>("HealthMessageLabel");
            ipHelpLabel = rootVisualElement.Q<Label>("IpHelpLabel");
            logField = rootVisualElement.Q<TextField>("LogField");
            startButton = rootVisualElement.Q<Button>("StartButton");
            stopButton = rootVisualElement.Q<Button>("StopButton");
            copyUrlButton = rootVisualElement.Q<Button>("CopyUrlButton");
            openUrlButton = rootVisualElement.Q<Button>("OpenUrlButton");
        }

        /// <summary>
        /// 把 Project Settings 中的值写入控件。
        /// </summary>
        private void BindInitialValues()
        {
            if (rootDirectoryField != null)
            {
                rootDirectoryField.value = rootDirectory;
            }

            if (portField != null)
            {
                portField.value = port;
            }

            if (autoRestartToggle != null)
            {
                autoRestartToggle.value = VoyageForgeFileServerSingleton.AutoRestart;
            }

            if (logField != null)
            {
                logField.isReadOnly = true;
                logField.value = logBuilder.ToString();
            }
        }

        /// <summary>
        /// 注册 UI 事件。
        /// </summary>
        private void RegisterCallbacks()
        {
            rootDirectoryField?.RegisterValueChangedCallback(evt =>
            {
                // TextField 修改立即写入 Project Settings，避免窗口关闭或域重载时丢失配置。
                rootDirectory = evt.newValue;
                SaveSettings();
                RefreshStatus();
            });

            portField?.RegisterValueChangedCallback(evt =>
            {
                // UI 输入层先限制端口范围，真正启动前仍会再做一次校验。
                port = Mathf.Clamp(evt.newValue, 1, 65535);
                SaveSettings();
                RefreshStatus();
            });

            autoRestartToggle?.RegisterValueChangedCallback(evt =>
            {
                VoyageForgeFileServerSingleton.AutoRestart = evt.newValue;
                AppendLog($"自动恢复服务器：{(evt.newValue ? "开启" : "关闭")}");
            });

            rootVisualElement.Q<Button>("ChooseRootButton")?.RegisterCallback<ClickEvent>(_ => ChooseRootDirectory());
            rootVisualElement.Q<Button>("OpenRootButton")?.RegisterCallback<ClickEvent>(_ => OpenRootDirectory());
            rootVisualElement.Q<Button>("CheckPortButton")?.RegisterCallback<ClickEvent>(_ => CheckPort());
            rootVisualElement.Q<Button>("AutoPortButton")?.RegisterCallback<ClickEvent>(_ => AutoFindPort());
            rootVisualElement.Q<Button>("RefreshIpButton")?.RegisterCallback<ClickEvent>(_ =>
            {
                RefreshIPList();
                RefreshStatus();
            });

            startButton?.RegisterCallback<ClickEvent>(_ => StartServer());
            stopButton?.RegisterCallback<ClickEvent>(_ => StopServer());
            copyUrlButton?.RegisterCallback<ClickEvent>(_ => CopyServerUrl());
            openUrlButton?.RegisterCallback<ClickEvent>(_ => OpenServerUrl());
            rootVisualElement.Q<Button>("ClearLogButton")?.RegisterCallback<ClickEvent>(_ => ClearLog());
        }

        /// <summary>
        /// 从 Project Settings 读取窗口配置。
        /// </summary>
        private void LoadSettings()
        {
            var settings = ForgeCLRSettings.instance;
            rootDirectory = settings.FileServerRootDirectory;
            port = settings.FileServerPort;
            bindIPAddress = settings.FileServerBindIPAddress;
        }

        /// <summary>
        /// 保存窗口配置到 Project Settings。
        /// </summary>
        private void SaveSettings()
        {
            ForgeCLRSettings.instance.SetFileServerConfig(rootDirectory, port, bindIPAddress);
        }

        /// <summary>
        /// 刷新 IP 下拉框。
        /// </summary>
        private void RefreshIPList()
        {
            ipList = VoyageForgeFileServer.GetAllLocalIPv4();
            ipOptions.Clear();
            ipOptions.Add(new KeyValuePair<string, string>("0.0.0.0 - 监听所有网卡，推荐", string.Empty));

            foreach (var info in ipList)
            {
                var tag = info.IsVirtualLike ? "疑似虚拟网卡" : "推荐";
                ipOptions.Add(new KeyValuePair<string, string>(
                    $"{info.Address} - {info.Name} - {info.NetworkType} - {tag}",
                    info.Address));
            }

            RebuildBindIpPopup();
        }

        /// <summary>
        /// 根据最新 IP 列表重建绑定 IP 下拉框。
        /// </summary>
        private void RebuildBindIpPopup()
        {
            var container = rootVisualElement.Q<VisualElement>("BindIpFieldContainer");
            if (container == null)
            {
                return;
            }

            container.Clear();
            var choices = ipOptions.Select(option => option.Key).ToList();
            var current = ipOptions.FirstOrDefault(option => option.Value == bindIPAddress);
            var currentValue = string.IsNullOrWhiteSpace(current.Key) ? choices[0] : current.Key;

            bindIpPopup = new PopupField<string>("绑定 IP", choices, currentValue);
            bindIpPopup.AddToClassList("vffs-field");
            bindIpPopup.RegisterValueChangedCallback(evt =>
            {
                var option = ipOptions.FirstOrDefault(candidate => candidate.Key == evt.newValue);
                bindIPAddress = option.Value ?? string.Empty;
                SaveSettings();
                RefreshStatus();
            });
            container.Add(bindIpPopup);
        }

        /// <summary>
        /// 选择文件服务器根目录。
        /// </summary>
        private void ChooseRootDirectory()
        {
            var selected = EditorUtility.OpenFolderPanel("选择 VoyageForge 文件服务器根目录", rootDirectory, string.Empty);
            if (string.IsNullOrWhiteSpace(selected))
            {
                return;
            }

            rootDirectory = selected;
            rootDirectoryField?.SetValueWithoutNotify(rootDirectory);
            SaveSettings();
            RefreshStatus();
        }

        /// <summary>
        /// 在系统文件管理器中打开当前文件服务器根目录。
        /// </summary>
        private void OpenRootDirectory()
        {
            if (Directory.Exists(rootDirectory) == false)
            {
                AppendLog($"无法打开根目录，目录不存在：{rootDirectory}");
                return;
            }

            EditorUtility.RevealInFinder(rootDirectory);
        }

        /// <summary>
        /// 检测当前端口是否可用。
        /// </summary>
        private void CheckPort()
        {
            if (port <= 0 || port > 65535)
            {
                AppendLog($"端口无效：{port}");
                return;
            }

            var available = IsCurrentPortAvailable();
            AppendLog(available ? $"端口 {port} 可用。" : $"端口 {port} 已被占用。");
            RefreshStatus();
        }

        /// <summary>
        /// 从当前端口开始查找可用端口。
        /// </summary>
        private void AutoFindPort()
        {
            var newPort = VoyageForgeFileServer.FindAvailablePort(port);
            if (newPort <= 0)
            {
                AppendLog("没有找到可用端口。");
                return;
            }

            port = newPort;
            portField?.SetValueWithoutNotify(port);
            SaveSettings();
            RefreshStatus();
            AppendLog($"找到可用端口：{port}");
        }

        /// <summary>
        /// 启动局域网文件服务器。
        /// </summary>
        private void StartServer()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rootDirectory))
                {
                    AppendLog("启动失败：请选择根目录。");
                    return;
                }

                if (!Directory.Exists(rootDirectory))
                {
                    AppendLog($"启动失败：根目录不存在：{rootDirectory}");
                    return;
                }

                if (port <= 0 || port > 65535)
                {
                    AppendLog("启动失败：端口号必须在 1 - 65535 之间。");
                    return;
                }

                if (!IsCurrentPortAvailable())
                {
                    var newPort = VoyageForgeFileServer.FindAvailablePort(port + 1);
                    if (newPort <= 0)
                    {
                        AppendLog($"启动失败：端口 {port} 已被占用，且没有找到可用端口。");
                        return;
                    }

                    port = newPort;
                    portField?.SetValueWithoutNotify(port);
                    AppendLog($"端口被占用，已自动切换到：{port}");
                }

                SaveSettings();
                VoyageForgeFileServerSingleton.StartServer(rootDirectory, port, bindIPAddress);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                AppendLog($"启动失败：{exception.Message}");
            }
            finally
            {
                RefreshStatus();
            }
        }

        /// <summary>
        /// 停止局域网文件服务器。
        /// </summary>
        private void StopServer()
        {
            try
            {
                VoyageForgeFileServerSingleton.StopServer(permanent: true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                AppendLog($"停止失败：{exception.Message}");
            }
            finally
            {
                RefreshStatus();
            }
        }

        /// <summary>
        /// 复制当前访问地址到系统剪贴板。
        /// </summary>
        private void CopyServerUrl()
        {
            var url = ResolveDisplayUrl();
            EditorGUIUtility.systemCopyBuffer = url;
            AppendLog($"已复制访问地址：{url}");
        }

        /// <summary>
        /// 在默认浏览器中打开当前访问地址。
        /// </summary>
        private void OpenServerUrl()
        {
            Application.OpenURL(ResolveDisplayUrl());
        }

        /// <summary>
        /// 清空窗口日志。
        /// </summary>
        private void ClearLog()
        {
            logBuilder.Clear();
            if (logField != null)
            {
                logField.value = string.Empty;
            }
        }

        /// <summary>
        /// 刷新状态卡片、按钮可用性和提示文本。
        /// </summary>
        private void RefreshStatus()
        {
            var running = Server != null && Server.IsRunning;
            RefreshStatusBadge(running);

            if (serverUrlLabel != null)
            {
                serverUrlLabel.text = running ? Server.ServerUrl : ResolveDisplayUrl();
            }

            if (rootStatusLabel != null)
            {
                rootStatusLabel.text = Directory.Exists(rootDirectory)
                    ? $"根目录：{rootDirectory}"
                    : $"根目录不存在：{rootDirectory}";
            }

            if (bindAddressLabel != null)
            {
                bindAddressLabel.text = string.IsNullOrWhiteSpace(bindIPAddress)
                    ? "绑定：0.0.0.0，监听所有网卡"
                    : $"绑定：{bindIPAddress}";
            }

            RefreshIpHelp();
            RefreshHealth();
            RefreshButtons(running);
        }

        /// <summary>
        /// 刷新运行状态徽标。
        /// </summary>
        /// <param name="running">服务器是否运行中。</param>
        private void RefreshStatusBadge(bool running)
        {
            if (statusBadge == null)
            {
                return;
            }

            statusBadge.text = running ? "运行中" : "未启动";
            statusBadge.RemoveFromClassList("vffs-status-running");
            statusBadge.RemoveFromClassList("vffs-status-stopped");
            statusBadge.AddToClassList(running ? "vffs-status-running" : "vffs-status-stopped");
        }

        /// <summary>
        /// 刷新绑定 IP 的辅助提示。
        /// </summary>
        private void RefreshIpHelp()
        {
            if (ipHelpLabel == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(bindIPAddress))
            {
                ipHelpLabel.text = $"当前监听所有网卡。局域网设备访问时请使用：{ResolveDisplayUrl()}";
                ipHelpLabel.RemoveFromClassList("vffs-help-warning");
                return;
            }

            var info = ipList.FirstOrDefault(candidate => candidate.Address == bindIPAddress);
            if (info != null && info.IsVirtualLike)
            {
                ipHelpLabel.text = $"当前 IP 疑似虚拟网卡：{bindIPAddress}。真机访问建议选择 Wi-Fi / Ethernet，或使用 0.0.0.0。";
                ipHelpLabel.AddToClassList("vffs-help-warning");
                return;
            }

            ipHelpLabel.text = $"当前访问地址：{ResolveDisplayUrl()}";
            ipHelpLabel.RemoveFromClassList("vffs-help-warning");
        }

        /// <summary>
        /// 刷新配置自检卡片。
        /// </summary>
        private void RefreshHealth()
        {
            if (healthBadge == null || healthMessageLabel == null)
            {
                return;
            }

            var messages = new List<string>();
            var failed = false;
            var warning = false;

            if (Directory.Exists(rootDirectory))
            {
                messages.Add("根目录可访问");
            }
            else
            {
                failed = true;
                messages.Add("根目录不存在");
            }

            if (port <= 0 || port > 65535)
            {
                // 端口非法属于必须修复的问题，用失败状态提示。
                failed = true;
                messages.Add("端口超出 1 - 65535 范围");
            }
            else if (IsCurrentPortAvailable())
            {
                // 当前服务器正在使用该端口时也算可用，避免运行中被误判为端口占用。
                messages.Add($"端口 {port} 可用");
            }
            else
            {
                // 端口被其他进程占用不一定阻断配置保存，但会阻断启动，所以用提醒状态。
                warning = true;
                messages.Add($"端口 {port} 已被占用");
            }

            var selectedIp = ipList.FirstOrDefault(candidate => candidate.Address == bindIPAddress);
            if (selectedIp != null && selectedIp.IsVirtualLike)
            {
                // 虚拟网卡在本机能访问，但手机或其他设备往往访问不到，给黄色提醒。
                warning = true;
                messages.Add("绑定 IP 疑似虚拟网卡");
            }
            else if (string.IsNullOrWhiteSpace(bindIPAddress))
            {
                messages.Add("监听所有网卡");
            }
            else
            {
                messages.Add("绑定 IP 可用");
            }

            var healthClass = failed ? "vffs-health-failed" : warning ? "vffs-health-warning" : "vffs-health-passed";
            healthBadge.text = failed ? "需修复" : warning ? "有提醒" : "通过";
            healthBadge.RemoveFromClassList("vffs-health-passed");
            healthBadge.RemoveFromClassList("vffs-health-warning");
            healthBadge.RemoveFromClassList("vffs-health-failed");
            healthBadge.AddToClassList(healthClass);
            healthMessageLabel.text = string.Join("；", messages);
        }

        /// <summary>
        /// 刷新按钮可用性。
        /// </summary>
        /// <param name="running">服务器是否运行中。</param>
        private void RefreshButtons(bool running)
        {
            startButton?.SetEnabled(!running);
            stopButton?.SetEnabled(running);
            copyUrlButton?.SetEnabled(true);
            openUrlButton?.SetEnabled(true);
        }

        /// <summary>
        /// 获取当前应展示或访问的 URL。
        /// </summary>
        /// <returns>当前访问地址。</returns>
        private string ResolveDisplayUrl()
        {
            if (Server != null && Server.IsRunning)
            {
                return Server.ServerUrl;
            }

            var ip = string.IsNullOrWhiteSpace(bindIPAddress)
                ? VoyageForgeFileServer.GetRecommendedLocalIPv4()
                : bindIPAddress;
            return $"http://{ip}:{port}/";
        }

        /// <summary>
        /// 判断当前端口是否可用。
        /// </summary>
        /// <returns>端口可用或当前服务器正在使用该端口时返回 true。</returns>
        private bool IsCurrentPortAvailable()
        {
            return Server != null && Server.IsRunning && Server.Port == port ||
                VoyageForgeFileServer.IsPortAvailable(port);
        }

        /// <summary>
        /// 追加窗口日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        private void AppendLog(string message)
        {
            var time = DateTime.Now.ToString("HH:mm:ss");
            logBuilder.AppendLine($"[{time}] {message}");

            if (logField != null)
            {
                logField.value = logBuilder.ToString();
            }

            Repaint();
        }
    }
}
