using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// ForgeCLR 单项环境检测接口。每个检测项实现此接口，提供验证和修复能力。
    /// </summary>
    public interface IForgeCLRValidationCheck
    {
        /// <summary>
        /// 所属模块 ID。null 表示 Core 通用检测。
        /// </summary>
        string ModuleId { get; }

        /// <summary>
        /// 检测项标题，用于 UI 展示和索引。
        /// </summary>
        string Title { get; }

        /// <summary>
        /// 是否支持自动修复。
        /// </summary>
        bool CanRepair { get; }

        /// <summary>
        /// 执行检测。
        /// </summary>
        /// <param name="context">检测上下文。</param>
        /// <returns>检测结果。</returns>
        ForgeCLRValidationItem Validate(ForgeCLRValidationContext context);

        /// <summary>
        /// 执行自动修复。
        /// </summary>
        /// <param name="context">检测上下文。</param>
        void Repair(ForgeCLRValidationContext context);
    }

    /// <summary>
    /// 泛型版本：声明归属的模块配置 SO 类型。
    /// TConfig 即模块运行时配置类型（如 YooAssetsRuntimeConfigSO / HCLRRuntimeConfigSO）。
    /// </summary>
    public interface IForgeCLRValidationCheck<TConfig> : IForgeCLRValidationCheck
        where TConfig : ScriptableObject
    {
        ForgeCLRValidationItem Validate(ForgeCLRValidationContext context, TConfig config);
    }
}
