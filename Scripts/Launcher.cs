using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VoyageForge.Bridge.Runtime;
using VoyageForge.Depot.Runtime.Utilities;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// ForgeCLR 运行时启动器，负责串联 YooAssets 补丁流程和 HybridCLR 热更新启动流程。
    /// </summary>
    public sealed class Launcher : MonoBehaviour
    {
        /// <summary>
        /// Unity 启动回调，执行资源补丁、加载热更新程序集并进入首个业务场景。
        /// </summary>
        private async UniTaskVoid Start()
        {
            Debug.Log(" [ForgeCLR] Launcher Start");
            
            await ForgeCLRSingleton.Instance.InitializationTask;
            
            Debug.Log(" [ForgeCLR] Launcher InitializationTask");

            await ForgeCLRSceneLoader.LoadStartupSceneAsync(ForgeCLRSingleton.Instance.Settings);
        }
    }
}