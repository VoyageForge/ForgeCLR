using System;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    [InitializeOnLoad]
    public static class VoyageForgeFileServerSingleton
    {
        private const string PrefServerShouldRun = "VoyageForge_ForgeCLR_FileServer_ShouldRun";

        private const double SafetyNetInterval = 3.0;

        private static double _lastSafetyNetCheck;

        public static VoyageForgeFileServer Server { get; private set; }

        public static bool AutoRestart
        {
            get => ForgeCLRSettings.instance.FileServerAutoRestart;
            set => ForgeCLRSettings.instance.SetFileServerAutoRestart(value);
        }

        public static bool ServerShouldRun
        {
            get => EditorPrefs.GetBool(PrefServerShouldRun, false);
            private set => EditorPrefs.SetBool(PrefServerShouldRun, value);
        }

        static VoyageForgeFileServerSingleton()
        {
            Server = new VoyageForgeFileServer();

            EditorApplication.quitting += OnEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            EditorApplication.update += OnEditorUpdate;

            EditorApplication.delayCall += TryRestoreServerIfNeeded;
        }

        public static void StartServer(string rootDirectory, int port, string bindIPAddress)
        {
            SaveConfig(rootDirectory, port, bindIPAddress);

            if (Server == null)
                Server = new VoyageForgeFileServer();

            Server.Start(rootDirectory, port, bindIPAddress);

            ServerShouldRun = true;
        }

        public static void StopServer(bool permanent)
        {
            Server?.Stop();

            if (permanent)
                ServerShouldRun = false;
        }

        public static void SaveConfig(string rootDirectory, int port, string bindIPAddress)
        {
            ForgeCLRSettings.instance.SetFileServerConfig(rootDirectory, port, bindIPAddress);
        }

        private static void OnCompilationStarted(object obj)
        {
            // 这里只记录生命周期，不主动停止。
        }

        private static void OnBeforeAssemblyReload()
        {
            if (Server != null && Server.IsRunning)
            {
                ServerShouldRun = true;
                Server.Stop();
            }
        }

        private static void OnAfterAssemblyReload()
        {
            EditorApplication.delayCall += TryRestoreServerIfNeeded;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode ||
                state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.delayCall += TryRestoreServerIfNeeded;
            }
        }

        private static void OnEditorQuitting()
        {
            ServerShouldRun = false;
            Server?.Stop();
        }

        private static void OnEditorUpdate()
        {
            if (!AutoRestart)
                return;

            if (!ServerShouldRun)
                return;

            double now = EditorApplication.timeSinceStartup;

            if (now - _lastSafetyNetCheck < SafetyNetInterval)
                return;

            _lastSafetyNetCheck = now;

            if (Server == null || !Server.IsRunning)
            {
                TryRestoreServerIfNeeded();
            }
        }

        private static void TryRestoreServerIfNeeded()
        {
            if (!AutoRestart)
                return;

            if (!ServerShouldRun)
                return;

            if (Server == null)
                Server = new VoyageForgeFileServer();

            if (Server.IsRunning)
                return;

            var settings = ForgeCLRSettings.instance;
            string root = settings.FileServerRootDirectory;
            int port = settings.FileServerPort;
            string ip = settings.FileServerBindIPAddress;

            if (string.IsNullOrWhiteSpace(root) || !System.IO.Directory.Exists(root))
                return;

            if (!VoyageForgeFileServer.IsPortAvailable(port))
                return;

            try
            {
                Server.Start(root, port, ip);
                Debug.Log($"[VoyageForge FileServer] 自动恢复成功: {Server.ServerUrl}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VoyageForge FileServer] 自动恢复失败: {e.Message}");
            }
        }
    }
}
