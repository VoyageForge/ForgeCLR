using UnityEngine;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    internal class FsmClearCacheBundle : IStateNode
    {
        private StateMachine _machine;

        void IStateNode.OnCreate(StateMachine machine)
        {
            _machine = machine;
        }

        void IStateNode.OnEnter()
        {
            Debug.Log("[ForgeCLR] 清理未使用的缓存文件");
            var packageName = (string)_machine.GetBlackboardValue("PackageName");
            var package = YooAssets.GetPackage(packageName);
            var operation = package.ClearCacheFilesAsync(EFileClearMode.ClearUnusedBundleFiles);
            operation.Completed += Operation_Completed;
        }

        void IStateNode.OnUpdate()
        {
        }

        void IStateNode.OnExit()
        {
        }

        private void Operation_Completed(AsyncOperationBase obj)
        {
            _machine.ChangeState<FsmStartGame>();
        }
    }
}
