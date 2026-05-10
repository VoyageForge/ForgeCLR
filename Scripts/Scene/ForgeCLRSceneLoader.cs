using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// ForgeCLR 场景加载工具，负责在热更新启动完成后进入第一个业务场景。
    /// </summary>
    public static class ForgeCLRSceneLoader
    {
        /// <summary>
        /// Launcher 初始化完成后缓存的默认资源包，供热更新入口无参调用时使用。
        /// </summary>
        private static ResourcePackage defaultPackage;

        /// <summary>
        /// 设置 ForgeCLR 当前使用的默认资源包。
        /// </summary>
        /// <param name="package">已经完成初始化和补丁流程的 YooAssets 资源包。</param>
        public static void SetDefaultPackage(ResourcePackage package)
        {
            defaultPackage = package;
        }

        /// <summary>
        /// 按运行时配置加载启动场景。
        /// </summary>
        /// <param name="package">已经完成初始化和补丁流程的 YooAssets 资源包；为空时使用默认资源包。</param>
        /// <param name="settings">ForgeCLR 运行时配置；为空时从 Resources 自动加载。</param>
        /// <returns>成功发起并完成场景加载时返回 true，否则返回 false。</returns>
        public static async UniTask<bool> LoadStartupSceneAsync(
            ResourcePackage package = null,
            ForgeCLRRuntimeSettings settings = null)
        {
            settings ??= ForgeCLRRuntimeSettings.LoadDefault();
            if (settings == null || !settings.LoadStartupScene)
            {
                return false;
            }

            return await LoadSceneAsync(package, settings.StartupSceneLocation, LoadSceneMode.Single);
        }

        /// <summary>
        /// 优先通过 YooAssets 加载场景；没有资源包时回退到 Unity 原生场景加载。
        /// </summary>
        /// <param name="package">YooAssets 资源包；为空时尝试使用默认资源包。</param>
        /// <param name="sceneLocation">场景地址，通常对应 YooAssets Collector 中的 Address。</param>
        /// <param name="sceneMode">场景加载模式，默认替换当前启动场景。</param>
        /// <returns>成功加载场景时返回 true，否则返回 false。</returns>
        public static async UniTask<bool> LoadSceneAsync(
            ResourcePackage package,
            string sceneLocation,
            LoadSceneMode sceneMode = LoadSceneMode.Single)
        {
            if (string.IsNullOrWhiteSpace(sceneLocation))
            {
                Debug.LogWarning("[ForgeCLR] 启动场景地址为空，已跳过首场景加载。");
                return false;
            }

            package ??= defaultPackage;
            if (package != null)
            {
                var handle = package.LoadSceneAsync(sceneLocation, sceneMode);
                await handle.ToUniTask();

                if (handle.Status == EOperationStatus.Succeed)
                {
                    Debug.Log($"[ForgeCLR] 启动场景加载完成：{sceneLocation}");
                    return true;
                }

                Debug.LogWarning($"[ForgeCLR] YooAssets 启动场景加载失败：{sceneLocation}，{handle.LastError}");
                return false;
            }

            var operation = SceneManager.LoadSceneAsync(sceneLocation, sceneMode);
            if (operation == null)
            {
                Debug.LogWarning($"[ForgeCLR] Unity 启动场景加载失败：{sceneLocation}");
                return false;
            }

            await operation.ToUniTask();
            Debug.Log($"[ForgeCLR] Unity 启动场景加载完成：{sceneLocation}");
            return true;
        }
    }
}
