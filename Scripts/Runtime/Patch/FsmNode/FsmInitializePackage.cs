using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VoyageForge.Bridge.Runtime;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// 初始化 YooAssets 资源包状态节点。
    /// HostPlayMode 下会通过 Bridge 读取资源服务器地址。
    /// </summary>
    internal class FsmInitializePackage : IStateNode
    {
        private StateMachine _machine;
        private PatchOperation _owner;

        /// <summary>
        /// Bridge 中用于 YooAssets 资源服务器的端点键。
        /// </summary>
        private const string ResourceEndpointKey = "default";

        void IStateNode.OnCreate(StateMachine machine)
        {
            _machine = machine;
            _owner = machine.Owner as PatchOperation;
        }

        void IStateNode.OnEnter()
        {
            Debug.Log("[ForgeCLR] 初始化资源包");
            InitPackage().Forget();
        }

        void IStateNode.OnUpdate()
        {
        }

        void IStateNode.OnExit()
        {
        }

        private async UniTaskVoid InitPackage()
        {
            var playMode = (EPlayMode)_machine.GetBlackboardValue("PlayMode");
            var packageName = (string)_machine.GetBlackboardValue("PackageName");

            var package = YooAssets.TryGetPackage(packageName);
            if (package == null)
                package = YooAssets.CreatePackage(packageName);

            InitializationOperation operation = null;
            if (playMode == EPlayMode.EditorSimulateMode)
            {
                var buildResult = EditorSimulateModeHelper.SimulateBuild(packageName);
                operation = package.InitializeAsync(new EditorSimulateModeParameters
                {
                    EditorFileSystemParameters =
                        FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory)
                });
            }
            else if (playMode == EPlayMode.OfflinePlayMode)
            {
                operation = package.InitializeAsync(new OfflinePlayModeParameters
                {
                    BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters()
                });
            }
            else if (playMode == EPlayMode.HostPlayMode)
            {
                string hostServer;
                try
                {
                    hostServer = GetHostServerURL();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[ForgeCLR] 获取 Bridge 资源服务器地址失败：{exception.Message}");
                    _owner.SetError(exception.Message);
                    return;
                }

                var remoteServices = new RemoteServices(hostServer, hostServer);
                operation = package.InitializeAsync(new HostPlayModeParameters
                {
                    BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(),
                    CacheFileSystemParameters = FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices)
                });
            }
            else if (playMode == EPlayMode.WebPlayMode)
            {
                operation = package.InitializeAsync(new WebPlayModeParameters
                {
                    WebServerFileSystemParameters = FileSystemParameters.CreateDefaultWebServerFileSystemParameters()
                });
            }

            if (operation == null)
            {
                _owner.SetError($"Unsupported YooAssets play mode: {playMode}");
                return;
            }

            await operation.ToUniTask();

            if (operation.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"[ForgeCLR] Package initialization failed: {operation.Error}");
                _owner.SetError(operation.Error);
                return;
            }

            _machine.ChangeState<FsmRequestPackageVersion>();
        }

        /// <summary>
        /// 从 Bridge 当前环境的默认端点读取 YooAssets 资源服务器地址。
        /// </summary>
        /// <returns>去除末尾斜杠后的资源服务器地址。</returns>
        private static string GetHostServerURL()
        {
            BridgeClient.UseDefaultConfigProviderIfMissing<ResourcesBridgeConfigProvider>();
            string hostServerUrl = BridgeClient.Config.GetBaseUrl(ResourceEndpointKey);
            return hostServerUrl.TrimEnd('/');
        }
    }
}
