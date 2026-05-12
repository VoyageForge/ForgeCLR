using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// ForgeCLR 运行时启动器，负责串联 YooAssets 补丁流程和 HybridCLR 热更新启动流程。
    /// </summary>
    public sealed class Launcher : MonoBehaviour
    {
        /// <summary>
        /// 运行时配置；为空时会从 Resources/ForgeCLRRuntimeSettings 自动加载。
        /// </summary>
        private ForgeCLRRuntimeSettings settings;

        /// <summary>
        /// Unity 启动回调，执行资源补丁、加载热更新程序集并进入首个业务场景。
        /// </summary>
        private async UniTaskVoid Start()
        {
            var ct = this.GetCancellationTokenOnDestroy();

            settings = await Resources.LoadAsync<ForgeCLRRuntimeSettings>(ForgeCLRRuntimeSettings.DefaultResourcesPath)
                .ToUniTask(cancellationToken: ct)
                .ContinueWith(request => request as ForgeCLRRuntimeSettings);

            var runtimeSettings = settings != null ? settings : ForgeCLRRuntimeSettings.LoadDefault();

            YooAssets.Initialize();

            var operation = new PatchOperation(runtimeSettings.PackageName, runtimeSettings.PlayMode);
            YooAssets.StartOperation(operation);
            await operation.ToUniTask();

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
            
            await HotUpdateBootstrap.StartAsync(
                gamePackage,
                runtimeSettings.LoadAotMetadata,
                runtimeSettings.AotMetadataDllLocations,
                runtimeSettings.HotUpdateDllLocations);

            await ForgeCLRSceneLoader.LoadStartupSceneAsync(runtimeSettings);
        }
    }
}