using UnityEngine;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// YooAssets 补丁流程操作，按状态机顺序完成初始化、版本请求、清单更新、下载和缓存清理。
    /// </summary>
    public class PatchOperation : GameAsyncOperation
    {
        /// <summary>
        /// 补丁流程内部步骤。
        /// </summary>
        private enum ESteps
        {
            /// <summary>
            /// 未开始。
            /// </summary>
            None,

            /// <summary>
            /// 正在更新。
            /// </summary>
            Update,

            /// <summary>
            /// 已结束。
            /// </summary>
            Done,
        }

        /// <summary>
        /// 驱动补丁流程的状态机。
        /// </summary>
        private readonly StateMachine _machine;

        /// <summary>
        /// 当前处理的 YooAssets 资源包名称。
        /// </summary>
        private readonly string _packageName;

        /// <summary>
        /// 当前补丁流程步骤。
        /// </summary>
        private ESteps _steps = ESteps.None;

        /// <summary>
        /// 创建补丁流程操作。
        /// </summary>
        /// <param name="packageName">YooAssets 资源包名称。</param>
        /// <param name="playMode">YooAssets 运行模式。</param>
        public PatchOperation(string packageName, EPlayMode playMode)
        {
            _packageName = packageName;

            _machine = new StateMachine(this);
            _machine.AddNode<FsmInitializePackage>();
            _machine.AddNode<FsmRequestPackageVersion>();
            _machine.AddNode<FsmUpdatePackageManifest>();
            _machine.AddNode<FsmCreateDownloader>();
            _machine.AddNode<FsmDownloadPackageFiles>();
            _machine.AddNode<FsmDownloadPackageOver>();
            _machine.AddNode<FsmClearCacheBundle>();
            _machine.AddNode<FsmStartGame>();

            _machine.SetBlackboardValue("PackageName", packageName);
            _machine.SetBlackboardValue("PlayMode", playMode);
        }

        /// <summary>
        /// YooAssets 操作开始回调。
        /// </summary>
        protected override void OnStart()
        {
            _steps = ESteps.Update;
            _machine.Run<FsmInitializePackage>();
        }

        /// <summary>
        /// YooAssets 操作逐帧更新回调。
        /// </summary>
        protected override void OnUpdate()
        {
            if (_steps == ESteps.None || _steps == ESteps.Done)
                return;

            if (_steps == ESteps.Update)
                _machine.Update();
        }

        /// <summary>
        /// YooAssets 操作中断回调。
        /// </summary>
        protected override void OnAbort()
        {
        }

        /// <summary>
        /// 将补丁流程标记为成功完成。
        /// </summary>
        public void SetFinish()
        {
            _steps = ESteps.Done;
            Status = EOperationStatus.Succeed;
            Debug.Log($"Package {_packageName} patch done !");
        }

        /// <summary>
        /// 将补丁流程标记为失败。
        /// </summary>
        /// <param name="error">失败原因。</param>
        public void SetError(string error)
        {
            _steps = ESteps.Done;
            Error = error;
            Status = EOperationStatus.Failed;
        }
    }
}
