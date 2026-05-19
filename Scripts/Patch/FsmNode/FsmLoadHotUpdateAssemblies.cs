using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// 加载热更新程序集状态节点。
    /// </summary>
    public class FsmLoadHotUpdateAssemblies : IStateNode
    {
        private StateMachine _machine;

        void IStateNode.OnCreate(StateMachine machine)
        {
            _machine = machine;
        }

        void IStateNode.OnEnter()
        {
            var config = (ForgeCLRRuntimeSettings)_machine.GetBlackboardValue("ForgeCLRRuntimeConfig");
            var gamePackage = YooAssets.GetPackage(config.PackageName);

            LoadHotUpdateAssembliesAsync(gamePackage, config.HotUpdateDllLocations).Forget();
        }

        void IStateNode.OnUpdate()
        {
        }

        void IStateNode.OnExit()
        {
        }

        /// <summary>
        /// 从 YooAssets 原生文件中加载热更新程序集。
        /// </summary>
        /// <param name="gamePackage"></param>
        /// <param name="locations">热更新程序集 DLL 的 YooAssets 地址集合。</param>
        /// <returns>当前 AppDomain 中已加载的程序集集合。</returns>
        private async UniTaskVoid LoadHotUpdateAssembliesAsync(ResourcePackage gamePackage, string[] locations)
        {
            var loadedAssemblies = new List<Assembly>();

            foreach (var location in locations ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(location))
                    continue;

                var assemblyName = GetAssemblyName(location);

#if UNITY_EDITOR
                var editorAssembly =
                    AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(assembly => assembly.GetName().Name == assemblyName);
                if (editorAssembly != null)
                {
                    Debug.Log($"[ForgeCLR] 编辑器跳过加载热更新程序集：{assemblyName}");
                    continue;
                }

#endif

                using var handle = gamePackage.LoadAssetAsync<TextAsset>(location);

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


            _machine.SetBlackboardValue("LoadedAssemblies", loadedAssemblies);

            _machine.ChangeState<FsmRuntimeInitializeInvoker>();
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