using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    public sealed class VoyageForgeFileServer
    {
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private Task _serverTask;

        public bool IsRunning { get; private set; }

        public string RootDirectory { get; private set; }

        public int Port { get; private set; }

        public string LocalIPAddress { get; private set; }

        public string BindIPAddress { get; private set; }

        public string ServerUrl => $"http://{LocalIPAddress}:{Port}/";

        public event Action<string> OnLog;

        public void Start(string rootDirectory, int port, string bindIPAddress)
        {
            if (IsRunning)
            {
                Log("服务器已经在运行。");
                return;
            }

            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("RootDirectory 不能为空。");

            if (!Directory.Exists(rootDirectory))
                throw new DirectoryNotFoundException($"目录不存在: {rootDirectory}");

            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "端口号必须在 1 - 65535 之间。");

            if (!IsPortAvailable(port))
                throw new InvalidOperationException($"端口 {port} 已被占用。");

            RootDirectory = Path.GetFullPath(rootDirectory);
            Port = port;
            BindIPAddress = bindIPAddress ?? string.Empty;

            IPAddress bindAddress = IPAddress.Any;

            if (!string.IsNullOrWhiteSpace(BindIPAddress))
            {
                if (!IPAddress.TryParse(BindIPAddress, out bindAddress))
                    throw new ArgumentException($"无效的绑定 IP: {BindIPAddress}");
            }

            LocalIPAddress = string.IsNullOrWhiteSpace(BindIPAddress)
                ? GetRecommendedLocalIPv4()
                : BindIPAddress;

            _cts = new CancellationTokenSource();

            _listener = new TcpListener(bindAddress, Port);
            _listener.Start();

            IsRunning = true;

            _serverTask = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);

            Log($"VoyageForge 文件服务器已启动: {ServerUrl}");
            Log($"绑定地址: {(string.IsNullOrWhiteSpace(BindIPAddress) ? "0.0.0.0" : BindIPAddress)}");
            Log($"根目录: {RootDirectory}");
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            try
            {
                _cts?.Cancel();
                _listener?.Stop();
            }
            catch
            {
                // ignored
            }

            IsRunning = false;

            Log("VoyageForge 文件服务器已停止。");
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = null;

                try
                {
                    client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client, token), token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (!token.IsCancellationRequested)
                        Log("监听 Socket 异常。");
                }
                catch (Exception e)
                {
                    Log($"AcceptLoop 异常: {e.Message}");
                    client?.Close();
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;

                try
                {
                    using (NetworkStream stream = client.GetStream())
                    {
                        string requestHeader = await ReadRequestHeaderAsync(stream, token);

                        if (string.IsNullOrWhiteSpace(requestHeader))
                            return;

                        string[] lines = requestHeader.Split(new[] { "\r\n" }, StringSplitOptions.None);

                        if (lines.Length == 0)
                            return;

                        string requestLine = lines[0];

                        string[] parts = requestLine.Split(' ');

                        if (parts.Length < 3)
                        {
                            await WriteTextResponseAsync(stream, 400, "Bad Request", "Bad Request", token);
                            return;
                        }

                        string method = parts[0];
                        string rawUrl = parts[1];

                        if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
                        {
                            await WriteTextResponseAsync(stream, 405, "Method Not Allowed", "Only GET / HEAD supported", token);
                            return;
                        }

                        bool isHead = string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);

                        string urlPath = rawUrl.Split('?')[0];
                        urlPath = Uri.UnescapeDataString(urlPath);

                        await HandleGetAsync(stream, urlPath, isHead, token);
                    }
                }
                catch (IOException)
                {
                    // 客户端断开，忽略
                }
                catch (ObjectDisposedException)
                {
                    // 服务器停止，忽略
                }
                catch (OperationCanceledException)
                {
                    // 服务器停止，忽略
                }
                catch (Exception e)
                {
                    Log($"处理请求异常: {e.Message}");
                }
            }
        }

        private async Task HandleGetAsync(NetworkStream stream, string urlPath, bool isHead, CancellationToken token)
        {
            string relativePath = urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);

            if (string.IsNullOrEmpty(relativePath))
                relativePath = string.Empty;

            string fullPath = Path.GetFullPath(Path.Combine(RootDirectory, relativePath));

            if (!IsSubPathOf(fullPath, RootDirectory))
            {
                await WriteTextResponseAsync(stream, 403, "Forbidden", "Forbidden", token);
                return;
            }

            if (Directory.Exists(fullPath))
            {
                await WriteDirectoryListingAsync(stream, fullPath, urlPath, isHead, token);
                return;
            }

            if (File.Exists(fullPath))
            {
                await WriteFileAsync(stream, fullPath, isHead, token);
                return;
            }

            await WriteTextResponseAsync(stream, 404, "Not Found", "File Not Found", token);
        }

        private async Task WriteDirectoryListingAsync(
            NetworkStream stream,
            string directory,
            string urlPath,
            bool isHead,
            CancellationToken token)
        {
            StringBuilder html = new StringBuilder();

            string displayPath = string.IsNullOrEmpty(urlPath) ? "/" : urlPath;

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset=\"utf-8\"/>");
            html.AppendLine("<title>VoyageForge File Server</title>");
            html.AppendLine("<style>");
            html.AppendLine("body{font-family:Arial,Microsoft YaHei,sans-serif;margin:24px;background:#f7f7f7;color:#222;}");
            html.AppendLine(".wrap{max-width:1000px;margin:0 auto;background:#fff;padding:24px;border-radius:12px;box-shadow:0 4px 16px rgba(0,0,0,.08);}");
            html.AppendLine("h2{margin-top:0;}");
            html.AppendLine("a{display:block;padding:8px 10px;color:#0366d6;text-decoration:none;border-radius:6px;}");
            html.AppendLine("a:hover{background:#f0f4ff;text-decoration:underline;}");
            html.AppendLine(".dir{font-weight:bold;}");
            html.AppendLine(".file{color:#333;}");
            html.AppendLine(".footer{margin-top:24px;color:#888;font-size:12px;}");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<div class=\"wrap\">");

            html.AppendLine("<h2>VoyageForge File Server</h2>");
            html.AppendLine($"<p>Index of {WebUtility.HtmlEncode(displayPath)}</p>");

            string normalizedUrlPath = urlPath.Replace("\\", "/");

            if (!normalizedUrlPath.EndsWith("/"))
                normalizedUrlPath += "/";

            if (normalizedUrlPath != "/")
            {
                html.AppendLine("<a class=\"dir\" href=\"../\">📁 ../</a>");
            }

            foreach (string dir in Directory.GetDirectories(directory))
            {
                string name = Path.GetFileName(dir);
                string href = Uri.EscapeDataString(name) + "/";
                html.AppendLine($"<a class=\"dir\" href=\"{href}\">📁 {WebUtility.HtmlEncode(name)}/</a>");
            }

            foreach (string file in Directory.GetFiles(directory))
            {
                string name = Path.GetFileName(file);
                long size = new FileInfo(file).Length;
                string href = Uri.EscapeDataString(name);

                html.AppendLine(
                    $"<a class=\"file\" href=\"{href}\">📄 {WebUtility.HtmlEncode(name)} ({FormatSize(size)})</a>");
            }

            html.AppendLine("<div class=\"footer\">Powered by VoyageForge</div>");
            html.AppendLine("</div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            byte[] body = Encoding.UTF8.GetBytes(html.ToString());

            string header =
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: text/html; charset=utf-8\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            byte[] headerBytes = Encoding.ASCII.GetBytes(header);

            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, token);

            if (!isHead)
                await stream.WriteAsync(body, 0, body.Length, token);
        }

        private async Task WriteFileAsync(NetworkStream stream, string filePath, bool isHead, CancellationToken token)
        {
            FileInfo fileInfo = new FileInfo(filePath);

            string fileName = fileInfo.Name;
            string contentType = GetContentType(fileInfo.Extension);
            string encodedFileName = Uri.EscapeDataString(fileName);

            string header =
                "HTTP/1.1 200 OK\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {fileInfo.Length}\r\n" +
                $"Content-Disposition: attachment; filename*=UTF-8''{encodedFileName}\r\n" +
                "Accept-Ranges: bytes\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            byte[] headerBytes = Encoding.ASCII.GetBytes(header);

            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, token);

            if (isHead)
                return;

            const int bufferSize = 1024 * 128;
            byte[] buffer = new byte[bufferSize];

            using (FileStream fs = new FileStream(
                       filePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite,
                       bufferSize,
                       useAsync: true))
            {
                int read;

                while ((read = await fs.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, read, token);
                }
            }

            Log($"下载文件: {fileName}");
        }

        private async Task WriteTextResponseAsync(
            NetworkStream stream,
            int statusCode,
            string statusText,
            string bodyText,
            CancellationToken token)
        {
            byte[] body = Encoding.UTF8.GetBytes(bodyText);

            string header =
                $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                "Content-Type: text/plain; charset=utf-8\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            byte[] headerBytes = Encoding.ASCII.GetBytes(header);

            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, token);
            await stream.WriteAsync(body, 0, body.Length, token);
        }

        private async Task<string> ReadRequestHeaderAsync(NetworkStream stream, CancellationToken token)
        {
            List<byte> buffer = new List<byte>(4096);
            byte[] temp = new byte[1024];

            while (!token.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(temp, 0, temp.Length, token);

                if (read <= 0)
                    break;

                for (int i = 0; i < read; i++)
                    buffer.Add(temp[i]);

                string text = Encoding.ASCII.GetString(buffer.ToArray());

                if (text.Contains("\r\n\r\n"))
                    return text;

                if (buffer.Count > 32 * 1024)
                    break;
            }

            return string.Empty;
        }

        public static bool IsPortAvailable(int port)
        {
            try
            {
                TcpListener testListener = new TcpListener(IPAddress.Any, port);
                testListener.Start();
                testListener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static int FindAvailablePort(int startPort, int maxTryCount = 100)
        {
            for (int i = 0; i < maxTryCount; i++)
            {
                int port = startPort + i;

                if (port > 65535)
                    break;

                if (IsPortAvailable(port))
                    return port;
            }

            return -1;
        }

        public static List<VoyageForgeIPAddressInfo> GetAllLocalIPv4()
        {
            List<VoyageForgeIPAddressInfo> result = new List<VoyageForgeIPAddressInfo>();

            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    IPInterfaceProperties properties = ni.GetIPProperties();

                    foreach (UnicastIPAddressInformation ip in properties.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;

                        string address = ip.Address.ToString();

                        if (address.StartsWith("127."))
                            continue;

                        if (address.StartsWith("169.254."))
                            continue;

                        VoyageForgeIPAddressInfo info = new VoyageForgeIPAddressInfo
                        {
                            Address = address,
                            Name = ni.Name,
                            Description = ni.Description,
                            NetworkType = ni.NetworkInterfaceType
                        };

                        info.IsVirtualLike = IsVirtualLikeNetwork(info);
                        info.Score = CalculateNetworkScore(info);

                        result.Add(info);
                    }
                }
            }
            catch
            {
                // ignored
            }

            result.Sort((a, b) => b.Score.CompareTo(a.Score));
            return result;
        }

        public static string GetRecommendedLocalIPv4()
        {
            List<VoyageForgeIPAddressInfo> list = GetAllLocalIPv4();

            foreach (VoyageForgeIPAddressInfo info in list)
            {
                if (!info.IsVirtualLike)
                    return info.Address;
            }

            if (list.Count > 0)
                return list[0].Address;

            return "127.0.0.1";
        }

        private static bool IsVirtualLikeNetwork(VoyageForgeIPAddressInfo info)
        {
            string text = $"{info.Name} {info.Description}".ToLowerInvariant();

            string[] virtualKeywords =
            {
                "vpn",
                "tap",
                "tun",
                "virtual",
                "vmware",
                "virtualbox",
                "hyper-v",
                "hyperv",
                "wsl",
                "docker",
                "npcap",
                "loopback",
                "zerotier",
                "tailscale",
                "clash",
                "mihomo",
                "sing-box",
                "singbox",
                "wireguard",
                "openvpn",
                "anyconnect",
                "fortinet",
                "forticlient",
                "hamachi",
                "nordvpn",
                "expressvpn",
                "surfshark",
                "tap-windows",
                "wan miniport",
                "bluetooth"
            };

            foreach (string keyword in virtualKeywords)
            {
                if (text.Contains(keyword))
                    return true;
            }

            return false;
        }

        private static int CalculateNetworkScore(VoyageForgeIPAddressInfo info)
        {
            int score = 0;

            if (info.NetworkType == NetworkInterfaceType.Wireless80211)
                score += 100;

            if (info.NetworkType == NetworkInterfaceType.Ethernet)
                score += 90;

            if (IsPrivateIPv4(info.Address))
                score += 50;

            if (info.Address.StartsWith("192.168."))
                score += 30;

            if (info.Address.StartsWith("10."))
                score += 20;

            if (Is172PrivateAddress(info.Address))
                score += 10;

            if (info.IsVirtualLike)
                score -= 300;

            return score;
        }

        private static bool IsPrivateIPv4(string address)
        {
            return address.StartsWith("192.168.")
                   || address.StartsWith("10.")
                   || Is172PrivateAddress(address);
        }

        private static bool Is172PrivateAddress(string address)
        {
            string[] parts = address.Split('.');

            if (parts.Length != 4)
                return false;

            if (parts[0] != "172")
                return false;

            if (!int.TryParse(parts[1], out int second))
                return false;

            return second >= 16 && second <= 31;
        }

        private static bool IsSubPathOf(string fullPath, string rootPath)
        {
            string normalizedFullPath = Path.GetFullPath(fullPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            string normalizedRootPath = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return normalizedFullPath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetContentType(string extension)
        {
            extension = extension.ToLowerInvariant();

            switch (extension)
            {
                case ".html":
                    return "text/html; charset=utf-8";
                case ".htm":
                    return "text/html; charset=utf-8";
                case ".txt":
                    return "text/plain; charset=utf-8";
                case ".json":
                    return "application/json; charset=utf-8";
                case ".xml":
                    return "application/xml; charset=utf-8";
                case ".png":
                    return "image/png";
                case ".jpg":
                    return "image/jpeg";
                case ".jpeg":
                    return "image/jpeg";
                case ".gif":
                    return "image/gif";
                case ".webp":
                    return "image/webp";
                case ".mp4":
                    return "video/mp4";
                case ".mp3":
                    return "audio/mpeg";
                case ".wav":
                    return "audio/wav";
                case ".zip":
                    return "application/zip";
                case ".apk":
                    return "application/vnd.android.package-archive";
                case ".bytes":
                    return "application/octet-stream";
                case ".bundle":
                    return "application/octet-stream";
                case ".ab":
                    return "application/octet-stream";
                default:
                    return "application/octet-stream";
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";

            double kb = bytes / 1024.0;

            if (kb < 1024)
                return $"{kb:F2} KB";

            double mb = kb / 1024.0;

            if (mb < 1024)
                return $"{mb:F2} MB";

            double gb = mb / 1024.0;

            return $"{gb:F2} GB";
        }

        private void Log(string message)
        {
            // 先在控制台打印
            Debug.Log($"[VoyageForge FileServer] {message}");

            if (OnLog != null)
            {
                // 调度到主线程执行
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        OnLog.Invoke(message);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[VoyageForge FileServer] OnLog 回调异常: {e}");
                    }
                };
            }
        }
    }

    public sealed class VoyageForgeIPAddressInfo
    {
        public string Address;
        public string Name;
        public string Description;
        public NetworkInterfaceType NetworkType;
        public int Score;
        public bool IsVirtualLike;

        public override string ToString()
        {
            string flag = IsVirtualLike ? "疑似虚拟网卡" : "推荐";
            return $"{Address} - {Name} - {NetworkType} - {flag}";
        }
    }
}