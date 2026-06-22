using System;
using System.Collections.Generic;
using System.Linq;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 通过反射发现所有 ForgeCLRModuleSettingsProvider 子类。
    /// </summary>
    public static class ForgeCLRModuleDiscovery
    {
        private static List<ForgeCLRModuleSettingsProvider> _cached;

        /// <summary>
        /// 反射扫描所有非抽象 ForgeCLRModuleSettingsProvider 子类，返回实例列表。
        /// </summary>
        public static List<ForgeCLRModuleSettingsProvider> DiscoverAll()
        {
            if (_cached != null)
                return _cached;

            _cached = new List<ForgeCLRModuleSettingsProvider>();
            var baseType = typeof(ForgeCLRModuleSettingsProvider);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsAbstract || !baseType.IsAssignableFrom(type))
                            continue;

                        if (Activator.CreateInstance(type) is ForgeCLRModuleSettingsProvider instance)
                            _cached.Add(instance);
                    }
                }
                catch (System.Reflection.ReflectionTypeLoadException)
                {
                    // 某些程序集无法加载所有类型（如缺失依赖），跳过即可
                }
            }

            return _cached;
        }

        /// <summary>
        /// 获取已启用的模块列表。
        /// </summary>
        public static List<ForgeCLRModuleSettingsProvider> GetEnabled(
            ForgeCLR.Runtime.ForgeCLRRuntimeSettings runtimeSettings)
            => DiscoverAll().Where(m => m.IsEnabled(runtimeSettings)).ToList();
    }
}
