using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// 调用所有程序集中的 RuntimeInitializeOnLoadMethod 方法状态节点。
    /// </summary>
    public class FsmRuntimeInitializeInvoker : IStateNode
    {
        private StateMachine _machine;

        private const string _attributeName = "RuntimeInitializeOnLoadMethodAttribute";

        void IStateNode.OnCreate(StateMachine machine)
        {
            _machine = machine;
        }

        void IStateNode.OnEnter()
        {
            var list = (List<Assembly>)_machine.GetBlackboardValue("LoadedAssemblies");

#if !UNITY_EDITOR
            foreach (var assembly in list)
            {
                InvokeAssembly(assembly);
            }

            Debug.Log("[ForgeCLR] FsmRuntimeInitializeInvoker initialized");
#else
            Debug.Log("[ForgeCLR] 编辑器模式跳过调用 RuntimeInitializeOnLoadMethod 方法");
#endif

            _machine.ChangeState<FsmStartGame>();
        }

        void IStateNode.OnUpdate()
        {
        }

        void IStateNode.OnExit()
        {
        }

        /// <summary>
        /// 根据程序集名称调用其中所有标记了 RuntimeInitializeOnLoadMethod 的静态无参方法
        /// </summary>
        /// <param name="assemblyName">程序集名称，例如 "Assembly-CSharp"</param>
        private void InvokeByAssemblyName(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                Debug.LogWarning("程序集名称为空");
                return;
            }

            // 找到匹配名称的程序集
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);

            if (assembly == null)
            {
                Debug.LogWarning($"找不到程序集: {assemblyName}");
                return;
            }

            // 调用程序集中的所有 RuntimeInitializeOnLoadMethod（通过字符串匹配特性名称）
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public |
                                                       BindingFlags.NonPublic))
                {
                    var attrs = method.GetCustomAttributes(false)
                        .Where(a => a.GetType().Name == _attributeName)
                        .ToArray();

                    if (attrs.Length > 0 && method.GetParameters().Length == 0)
                    {
                        try
                        {
                            method.Invoke(null, null);
                            Debug.Log($"调用 RuntimeInitializeOnLoadMethod: {type.FullName}.{method.Name}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"调用 {type.FullName}.{method.Name} 失败: {ex}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 调用指定程序集里所有被 RuntimeInitializeOnLoadMethod 标注的静态无参方法（通过字符串匹配特性）
        /// </summary>
        /// <param name="assembly">目标程序集</param>
        public static void InvokeAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                Debug.LogWarning("传入的程序集为 null");
                return;
            }

            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public |
                                                       BindingFlags.NonPublic))
                {
                    var attrs = method.GetCustomAttributes(false)
                        .Where(a => a.GetType().Name == _attributeName)
                        .ToArray();

                    if (attrs.Length > 0 && method.GetParameters().Length == 0)
                    {
                        try
                        {
                            method.Invoke(null, null);
                            Debug.Log($"调用 RuntimeInitializeOnLoadMethod: {type.FullName}.{method.Name}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"调用 {type.FullName}.{method.Name} 失败: {ex}");
                        }
                    }
                }
            }
        }
    }
}