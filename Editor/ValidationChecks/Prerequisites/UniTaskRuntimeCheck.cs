using System;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 UniTask 运行时库（Cysharp.Threading.Tasks.UniTask 类型）是否已安装。
    /// UniTask 是项目使用的异步编程库，通过反射检查其程序集是否可被加载。
    /// 此检查不支持自动修复，缺失时请通过 Package Manager 安装 UniTask 包。
    /// </summary>
    public sealed class UniTaskRuntimeCheck : IForgeCLRValidationCheck
    {
        public string Title => "UniTask Runtime";
        public bool CanRepair => false;

        /// <summary>
        /// 通过反射检测 UniTask 运行时程序集是否可用。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            var installed = Type.GetType("Cysharp.Threading.Tasks.UniTask, UniTask") != null;
            return new ForgeCLRValidationItem(Title,
                installed ? "UniTask Runtime 已安装" : "未检测到 UniTask Runtime",
                installed ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
        }

        /// <summary>
        /// 不支持自动修复。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context) { }
    }
}
