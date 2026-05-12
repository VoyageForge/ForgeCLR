using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using HybridCLR;
using UnityEngine;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// HybridCLR 热更新启动器，负责加载 AOT 元数据、加载热更新程序集并调用入口方法。
    /// </summary>
    public static class HotUpdateBootstrap
    {
        /// <summary>
        /// 启动热更新程序集入口。
        /// </summary>
        /// <param name="package">已经完成初始化和补丁更新的 YooAssets 资源包。</param>
        /// <param name="loadAotMetadata">是否加载 AOT 补充元数据。</param>
        /// <param name="aotMetadataDllLocations">AOT 元数据 DLL 的 YooAssets 地址集合。</param>
        /// <param name="hotUpdateDllLocations">热更新程序集 DLL 的 YooAssets 地址集合。</param>
      
        public static async UniTask StartAsync(
            ResourcePackage package,
            bool loadAotMetadata,
            string[] aotMetadataDllLocations,
            string[] hotUpdateDllLocations)
        {
            if (loadAotMetadata)
                await LoadAotMetadataAsync(package, aotMetadataDllLocations);

            await LoadHotUpdateAssembliesAsync(package, hotUpdateDllLocations);
        }

        /// <summary>
        /// 从 YooAssets 原生文件中读取 AOT 元数据并交给 HybridCLR 注册。
        /// </summary>
        /// <param name="package">YooAssets 资源包。</param>
        /// <param name="locations">AOT 元数据 DLL 的 YooAssets 地址集合。</param>
        private static async UniTask LoadAotMetadataAsync(ResourcePackage package, string[] locations)
        {
#if UNITY_EDITOR
            Debug.Log("[ForgeCLR] Editor 模式跳过 AOT 元数据加载。");
            await UniTask.Yield();
#else
            foreach (var location in locations ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(location))
                    continue;

// 禁止使用LoadRawFileAsync ，会从 android 内置包去加载资源，导致加载失败
                using var handle = package.LoadAssetAsync<TextAsset>(location);
                await handle.ToUniTask();

                if (handle.Status != EOperationStatus.Succeed)
                {
                    Debug.LogError($"[ForgeCLR] 加载 AOT 元数据失败：{location}，{handle.LastError}");
                    continue;
                }
                
                var errorCode = RuntimeApi.LoadMetadataForAOTAssembly(
                    handle.GetAssetObject<TextAsset>().bytes ,
                    HomologousImageMode.SuperSet);

                if (errorCode == LoadImageErrorCode.OK)
                    Debug.Log($"[ForgeCLR] AOT 元数据加载成功：{location}");
                else
                    Debug.LogWarning($"[ForgeCLR] AOT 元数据加载结果：{location}，{errorCode}");
            }
#endif
        }

        /// <summary>
        /// 从 YooAssets 原生文件中加载热更新程序集。
        /// </summary>
        /// <param name="package">YooAssets 资源包。</param>
        /// <param name="locations">热更新程序集 DLL 的 YooAssets 地址集合。</param>
        /// <returns>当前 AppDomain 中已加载的程序集集合。</returns>
        private static async UniTask<Assembly[]> LoadHotUpdateAssembliesAsync(ResourcePackage package,
            string[] locations)
        {
            var loadedAssemblies = new List<Assembly>();

            foreach (var location in locations ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(location))
                    continue;

                var assemblyName = GetAssemblyName(location);

#if UNITY_EDITOR
                var editorAssembly =
                    loadedAssemblies.FirstOrDefault(assembly => assembly.GetName().Name == assemblyName);
                if (editorAssembly != null)
                    continue;
#endif

                using var handle = package.LoadAssetAsync<TextAsset>(location);
                await handle.ToUniTask();

                if (handle.Status != EOperationStatus.Succeed)
                {
                    Debug.LogWarning($"[ForgeCLR] 热更新程序集未加载：{location}，{handle.LastError}");
                    continue;
                }

                var assembly = Assembly.Load(handle.GetAssetObject<TextAsset>().bytes);
                loadedAssemblies.Add(assembly);
                Debug.Log($"[ForgeCLR] 热更新程序集加载成功：{assembly.GetName().Name}");
            }

            return loadedAssemblies.ToArray();
        }


        /// <summary>
        /// 从 DLL 文件地址推导程序集名称。
        /// </summary>
        /// <param name="location">DLL 文件的 YooAssets 地址。</param>
        /// <returns>程序集名称。</returns>
        private static string GetAssemblyName(string location)
        {
            var fileName = Path.GetFileNameWithoutExtension(location);
            return fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - 4)
                : fileName;
        }
    }
}