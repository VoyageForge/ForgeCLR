using System;
using Cysharp.Threading.Tasks;
using HybridCLR;
using UnityEngine;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// 加载 AOT 元数据状态节点。
    /// </summary>
    public class FsmLoadAotMetadata : IStateNode
    {
        private StateMachine _machine;

        public void OnCreate(StateMachine machine)
        {
            _machine = machine;
        }

        public void OnEnter()
        {
            var config = (ForgeCLRRuntimeSettings)_machine.GetBlackboardValue("ForgeCLRRuntimeConfig");

            var gamePackage = YooAssets.GetPackage(config.PackageName);
            
            LoadAotMetadataAsync(gamePackage,config.AotMetadataDllLocations).Forget();
        }

        public void OnUpdate()
        {
        }

        public void OnExit()
        {
        }

        /// <summary>
        /// 从 YooAssets 原生文件中读取 AOT 元数据并交给 HybridCLR 注册。
        /// </summary>
        /// <param name="gamePackage"></param>
        /// <param name="locations">AOT 元数据 DLL 的 YooAssets 地址集合。</param>
        private  async UniTask LoadAotMetadataAsync(ResourcePackage gamePackage, string[] locations)
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
                using var handle = gamePackage.LoadAssetAsync<TextAsset>(location);
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
            _machine.ChangeState<FsmLoadHotUpdateAssemblies>();
        }
    }
}