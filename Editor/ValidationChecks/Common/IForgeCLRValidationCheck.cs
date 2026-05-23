namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// ForgeCLR 单项环境检测接口。每个检测项实现此接口，提供验证和修复能力。
    /// </summary>
    public interface IForgeCLRValidationCheck
    {
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
}
