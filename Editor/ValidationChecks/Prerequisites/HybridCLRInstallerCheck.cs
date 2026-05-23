using System;
using HybridCLR.Editor.Installer;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测 HybridCLR 是否已通过安装器完成安装（包括 il2cpp 裁剪等必要步骤）。
    /// HybridCLR 的安装流程包含将必要的 C# 运行时文件复制到 Unity 项目中的关键步骤，
    /// 若未完成安装则热更新功能无法正常工作。
    /// 此检查不支持自动修复，缺失时请通过 HybridCLR 安装器完成安装。
    /// </summary>
    public sealed class HybridCLRInstallerCheck : IForgeCLRValidationCheck
    {
        public string Title => "HybridCLR Installer";
        public bool CanRepair => false;

        /// <summary>
        /// 通过 InstallerController 检测 HybridCLR 是否已完成安装。
        /// </summary>
        public ForgeCLRValidationItem Validate(ForgeCLRValidationContext context)
        {
            try
            {
                var installer = new InstallerController();
                var installed = installer.HasInstalledHybridCLR();
                return new ForgeCLRValidationItem(Title,
                    installed ? "HybridCLR Installer 已完成" : "HybridCLR Installer 尚未完成",
                    installed ? ForgeCLRValidationStatus.Passed : ForgeCLRValidationStatus.Failed);
            }
            catch (Exception e)
            {
                return new ForgeCLRValidationItem(Title, $"HybridCLR Installer 检测失败：{e.Message}",
                    ForgeCLRValidationStatus.Failed);
            }
        }

        /// <summary>
        /// 不支持自动修复。
        /// </summary>
        public void Repair(ForgeCLRValidationContext context) { }
    }
}
