using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using VoyageForge.Bridge.Runtime;
using VoyageForge.Depot.Runtime.Utilities;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    public class ForgeCLRSingleton : MonoSingleton<ForgeCLRSingleton>
    {
        /// <summary>
        /// 运行时配置；为空时会从 Resources/ForgeCLRRuntimeSettings 自动加载。
        /// </summary>
        private ForgeCLRRuntimeSettings settings;

        public ForgeCLRRuntimeSettings Settings => settings;

        /// <summary>
        /// 初始化任务。  
        /// </summary>
        public UniTask InitializationTask => _initCompletionSource.Task;

        private readonly UniTaskCompletionSource _initCompletionSource = new UniTaskCompletionSource();

        private void Start()
        {
            InitializationOperation().Forget();
        }
        private void OnDestroy()
        {
            // 如果还没完成就销毁了，也要结束 Task 避免泄漏
            _initCompletionSource?.TrySetCanceled();
        }
        
        private async UniTaskVoid InitializationOperation()
        {
            try
            {
                BridgeClient.Instance.Init();

                YooAssets.Initialize();
                

                var LauncherStatusPrefab = await Resources.LoadAsync<GameObject>("LauncherStatus").ToUniTask();
                
                Instantiate(LauncherStatusPrefab);

                var ct = this.GetCancellationTokenOnDestroy();

                settings = await Resources
                    .LoadAsync<ForgeCLRRuntimeSettings>(ForgeCLRRuntimeSettings.DefaultResourcesPath)
                    .ToUniTask(cancellationToken: ct)
                    .ContinueWith(request => request as ForgeCLRRuntimeSettings);

                var runtimeSettings = settings != null ? settings : ForgeCLRRuntimeSettings.LoadDefault();

                var playMode = runtimeSettings.PlayMode;
                if (playMode == EPlayMode.HostPlayMode && runtimeSettings.EnableAutoOfflineFallback)
                {
                    if (Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        Debug.LogWarning("[ForgeCLR] 无网络连接，自动切换为离线模式");
                        playMode = EPlayMode.OfflinePlayMode;
                    }
                    else
                    {
                        var hostServer = BridgeClient.Instance.GetBaseUrl("Assets").TrimEnd('/');
                        var networkOk = await IsResourceServerReachableAsync(hostServer, ct);
                        if (!networkOk)
                        {
                            Debug.LogWarning("[ForgeCLR] 资源服务器不可达，自动切换为离线模式");
                            playMode = EPlayMode.OfflinePlayMode;
                        }
                    }
                }

                var operation = new PatchOperation(runtimeSettings.PackageName, playMode);

                operation.SetBlackboardValue("ForgeCLRRuntimeConfig", runtimeSettings);
                
                YooAssets.StartOperation(operation);

                await operation.ToUniTask(cancellationToken: ct);

                if (operation.Status != EOperationStatus.Succeed)
                {
                    Debug.LogError($"[ForgeCLR] Patch failed: {operation.Error}");
                    return;
                }
                else
                {
                    Debug.Log($"[ForgeCLR] Patch succeeded: {operation.Status}");
                }

                var gamePackage = YooAssets.GetPackage(runtimeSettings.PackageName);

                Debug.Log($"[ForgeCLR] Set default package to {gamePackage.PackageName}");

                YooAssets.SetDefaultPackage(gamePackage);

                Debug.Log($"[ForgeCLR] Set streaming assets");

                // 初始化完成后销毁 LauncherStatus
                Destroy(LauncherStatus.Instance.gameObject);

                // 标记初始化成功
                _initCompletionSource.TrySetResult();
            }
            catch (Exception ex)
            {
                // 初始化失败也要通知外部，否则等待方会永远卡住
                _initCompletionSource.TrySetException(ex);
            }
        }

        /// <summary>
        /// 初始化 ForgeCLR 运行时环境。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeForgeCLR()
        {
            _ = Instance;
        }

        /// <summary>
        /// 向资源服务器发送 HEAD 请求以检测连通性。
        /// </summary>
        private static async UniTask<bool> IsResourceServerReachableAsync(string hostServer, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new UnityWebRequest(hostServer, UnityWebRequest.kHttpVerbHEAD);
                request.timeout = 3;
                var op = request.SendWebRequest();
                await op.ToUniTask(cancellationToken: cancellationToken);

                return request.result != UnityWebRequest.Result.ConnectionError;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ForgeCLR] 资源服务器连通性探测异常: {ex.Message}");
                return false;
            }
        }
    }
}