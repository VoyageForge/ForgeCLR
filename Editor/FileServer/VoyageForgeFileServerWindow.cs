using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    public class VoyageForgeFileServerWindow : EditorWindow
    {
        private const string PrefRootDirectory = "VoyageForge_ForgeCLR_FileServer_RootDirectory";
        private const string PrefPort = "VoyageForge_ForgeCLR_FileServer_Port";
        private const string PrefBindIPAddress = "VoyageForge_ForgeCLR_FileServer_BindIPAddress";

        private static VoyageForgeFileServer _server;

        private string _rootDirectory;
        private int _port;
        private string _bindIPAddress;

        private List<VoyageForgeIPAddressInfo> _ipList = new List<VoyageForgeIPAddressInfo>();
        private string[] _ipDisplayNames = Array.Empty<string>();
        private int _selectedIPIndex;

        private Vector2 _scroll;
        private readonly StringBuilder _logBuilder = new StringBuilder();

        [MenuItem("VoyageForge/ForgeCLR/File Server")]
        public static void Open()
        {
            VoyageForgeFileServerWindow window = GetWindow<VoyageForgeFileServerWindow>();
            window.titleContent = new GUIContent("VoyageForge File Server");
            window.minSize = new Vector2(620, 520);
            window.Show();
        }

        private void OnEnable()
        {
            _rootDirectory = EditorPrefs.GetString(PrefRootDirectory, Application.dataPath);
            _port = EditorPrefs.GetInt(PrefPort, 8899);
            _bindIPAddress = EditorPrefs.GetString(PrefBindIPAddress, string.Empty);

            RefreshIPList();

            if (_server == null)
            {
                _server = new VoyageForgeFileServer();
                _server.OnLog += AppendLog;
            }

            AppendLog("VoyageForge File Server Window 已打开。");
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(PrefRootDirectory, _rootDirectory);
            EditorPrefs.SetInt(PrefPort, _port);
            EditorPrefs.SetString(PrefBindIPAddress, _bindIPAddress);
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(8);
            DrawConfig();
            EditorGUILayout.Space(8);
            DrawStatus();
            EditorGUILayout.Space(8);
            DrawButtons();
            EditorGUILayout.Space(8);
            DrawLog();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("VoyageForge 局域网文件服务器", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "启动后，同一局域网内设备可以通过 IP 地址访问并下载根目录下的文件。\n" +
                "如果你开启了 VPN，自动检测到的第一个 IP 可能是虚拟网卡，所以这里提供 IP 下拉选择。\n" +
                "如果局域网设备无法访问，请在 Windows 防火墙中允许 Unity Editor 通过专用网络。",
                MessageType.Info);
        }

        private void DrawConfig()
        {
            EditorGUILayout.LabelField("配置", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("根目录", GUILayout.Width(80));
                _rootDirectory = EditorGUILayout.TextField(_rootDirectory);

                if (GUILayout.Button("选择", GUILayout.Width(60)))
                {
                    string selected = EditorUtility.OpenFolderPanel("选择 VoyageForge 文件服务器根目录", _rootDirectory, "");

                    if (!string.IsNullOrEmpty(selected))
                    {
                        _rootDirectory = selected;
                        EditorPrefs.SetString(PrefRootDirectory, _rootDirectory);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("端口", GUILayout.Width(80));
                _port = EditorGUILayout.IntField(_port);

                if (GUILayout.Button("检测端口", GUILayout.Width(90)))
                {
                    CheckPort();
                }

                if (GUILayout.Button("自动查找", GUILayout.Width(90)))
                {
                    AutoFindPort();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("绑定 IP", GUILayout.Width(80));

                if (_ipDisplayNames == null || _ipDisplayNames.Length == 0)
                    RefreshIPList();

                int newIndex = EditorGUILayout.Popup(_selectedIPIndex, _ipDisplayNames);

                if (newIndex != _selectedIPIndex)
                {
                    _selectedIPIndex = newIndex;
                    _bindIPAddress = GetSelectedBindIP();
                    EditorPrefs.SetString(PrefBindIPAddress, _bindIPAddress);
                }

                if (GUILayout.Button("刷新", GUILayout.Width(60)))
                {
                    RefreshIPList();
                }
            }

            string selectedBindIP = GetSelectedBindIP();

            if (string.IsNullOrWhiteSpace(selectedBindIP))
            {
                string recommendIP = VoyageForgeFileServer.GetRecommendedLocalIPv4();

                EditorGUILayout.HelpBox(
                    $"当前选择：监听所有网卡 0.0.0.0\n" +
                    $"访问时不要使用 0.0.0.0，请使用推荐地址：\n" +
                    $"http://{recommendIP}:{_port}/",
                    MessageType.None);
            }
            else
            {
                VoyageForgeIPAddressInfo info = GetSelectedIPInfo();

                if (info != null && info.IsVirtualLike)
                {
                    EditorGUILayout.HelpBox(
                        $"当前选择的 IP 疑似虚拟网卡：{selectedBindIP}\n" +
                        $"如果你要让手机或局域网内其他电脑访问，建议选择 Wi-Fi / Ethernet 对应的 192.168.x.x 地址。",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"当前访问地址：\nhttp://{selectedBindIP}:{_port}/",
                        MessageType.None);
                }
            }
        }

        private void DrawStatus()
        {
            EditorGUILayout.LabelField("状态", EditorStyles.boldLabel);

            bool running = _server != null && _server.IsRunning;

            EditorGUILayout.LabelField("运行状态", running ? "运行中" : "未启动");

            if (running)
            {
                EditorGUILayout.LabelField("绑定 IP", string.IsNullOrWhiteSpace(_server.BindIPAddress) ? "0.0.0.0" : _server.BindIPAddress);
                EditorGUILayout.LabelField("访问 IP", _server.LocalIPAddress);
                EditorGUILayout.LabelField("访问地址", _server.ServerUrl);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("复制访问地址"))
                    {
                        EditorGUIUtility.systemCopyBuffer = _server.ServerUrl;
                        AppendLog($"已复制地址: {_server.ServerUrl}");
                    }

                    if (GUILayout.Button("浏览器打开"))
                    {
                        Application.OpenURL(_server.ServerUrl);
                    }
                }
            }
            else
            {
                string selectedBindIP = GetSelectedBindIP();

                if (string.IsNullOrWhiteSpace(selectedBindIP))
                {
                    string recommendIP = VoyageForgeFileServer.GetRecommendedLocalIPv4();

                    EditorGUILayout.LabelField("绑定地址", "0.0.0.0");
                    EditorGUILayout.LabelField("推荐访问地址", $"http://{recommendIP}:{_port}/");
                }
                else
                {
                    EditorGUILayout.LabelField("绑定地址", selectedBindIP);
                    EditorGUILayout.LabelField("预计访问地址", $"http://{selectedBindIP}:{_port}/");
                }
            }
        }

        private void DrawButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _server == null || !_server.IsRunning;

                if (GUILayout.Button("启动服务器", GUILayout.Height(32)))
                {
                    StartServer();
                }

                GUI.enabled = _server != null && _server.IsRunning;

                if (GUILayout.Button("停止服务器", GUILayout.Height(32)))
                {
                    StopServer();
                }

                GUI.enabled = true;
            }
        }

        private void DrawLog()
        {
            EditorGUILayout.LabelField("日志", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            EditorGUILayout.TextArea(_logBuilder.ToString(), GUILayout.ExpandHeight(true));

            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("清空日志"))
                {
                    _logBuilder.Clear();
                }
            }
        }

        private void StartServer()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_rootDirectory))
                {
                    EditorUtility.DisplayDialog("错误", "请选择根目录。", "确定");
                    return;
                }

                if (!System.IO.Directory.Exists(_rootDirectory))
                {
                    EditorUtility.DisplayDialog("错误", $"根目录不存在:\n{_rootDirectory}", "确定");
                    return;
                }

                if (_port <= 0 || _port > 65535)
                {
                    EditorUtility.DisplayDialog("错误", "端口号必须在 1 - 65535 之间。", "确定");
                    return;
                }

                if (!VoyageForgeFileServer.IsPortAvailable(_port))
                {
                    bool autoFind = EditorUtility.DisplayDialog(
                        "端口被占用",
                        $"端口 {_port} 已被占用，是否自动查找可用端口？",
                        "自动查找",
                        "取消");

                    if (!autoFind)
                        return;

                    int newPort = VoyageForgeFileServer.FindAvailablePort(_port + 1);

                    if (newPort <= 0)
                    {
                        EditorUtility.DisplayDialog("错误", "没有找到可用端口。", "确定");
                        return;
                    }

                    _port = newPort;
                    AppendLog($"自动切换到可用端口: {_port}");
                }

                _bindIPAddress = GetSelectedBindIP();

                EditorPrefs.SetString(PrefRootDirectory, _rootDirectory);
                EditorPrefs.SetInt(PrefPort, _port);
                EditorPrefs.SetString(PrefBindIPAddress, _bindIPAddress);

                _server.Start(_rootDirectory, _port, _bindIPAddress);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("启动失败", e.Message, "确定");
                AppendLog($"启动失败: {e.Message}");
            }
        }

        private void StopServer()
        {
            try
            {
                _server?.Stop();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                AppendLog($"停止失败: {e.Message}");
            }
        }

        private void CheckPort()
        {
            if (_port <= 0 || _port > 65535)
            {
                AppendLog($"端口无效: {_port}");
                EditorUtility.DisplayDialog("端口检测", "端口号必须在 1 - 65535 之间。", "确定");
                return;
            }

            bool available = VoyageForgeFileServer.IsPortAvailable(_port);

            if (available)
            {
                AppendLog($"端口 {_port} 可用。");
                EditorUtility.DisplayDialog("端口检测", $"端口 {_port} 可用。", "确定");
            }
            else
            {
                AppendLog($"端口 {_port} 已被占用。");
                EditorUtility.DisplayDialog("端口检测", $"端口 {_port} 已被占用。", "确定");
            }
        }

        private void AutoFindPort()
        {
            int newPort = VoyageForgeFileServer.FindAvailablePort(_port);

            if (newPort > 0)
            {
                _port = newPort;
                EditorPrefs.SetInt(PrefPort, _port);
                AppendLog($"找到可用端口: {_port}");
            }
            else
            {
                AppendLog("没有找到可用端口。");
            }
        }

        private void RefreshIPList()
        {
            _ipList = VoyageForgeFileServer.GetAllLocalIPv4();

            List<string> displayNames = new List<string>();

            displayNames.Add("0.0.0.0 - 监听所有网卡，推荐");

            foreach (VoyageForgeIPAddressInfo info in _ipList)
            {
                string tag = info.IsVirtualLike ? "疑似虚拟网卡" : "推荐";
                displayNames.Add($"{info.Address} - {info.Name} - {info.NetworkType} - {tag}");
            }

            _ipDisplayNames = displayNames.ToArray();

            _selectedIPIndex = 0;

            if (!string.IsNullOrWhiteSpace(_bindIPAddress))
            {
                for (int i = 0; i < _ipList.Count; i++)
                {
                    if (_ipList[i].Address == _bindIPAddress)
                    {
                        _selectedIPIndex = i + 1;
                        return;
                    }
                }
            }

            // 如果没有保存过 IP，默认仍然选择 0.0.0.0。
            // 好处是监听所有网卡，不会因为选错 IP 导致访问不了。
            _selectedIPIndex = 0;
            _bindIPAddress = string.Empty;
        }

        private string GetSelectedBindIP()
        {
            if (_selectedIPIndex <= 0)
                return string.Empty;

            int listIndex = _selectedIPIndex - 1;

            if (listIndex < 0 || listIndex >= _ipList.Count)
                return string.Empty;

            return _ipList[listIndex].Address;
        }

        private VoyageForgeIPAddressInfo GetSelectedIPInfo()
        {
            if (_selectedIPIndex <= 0)
                return null;

            int listIndex = _selectedIPIndex - 1;

            if (listIndex < 0 || listIndex >= _ipList.Count)
                return null;

            return _ipList[listIndex];
        }

        private void AppendLog(string log)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            _logBuilder.AppendLine($"[{time}] {log}");
            Repaint();
        }
    }
}