using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    public class FsmUpdatePackageManifest : IStateNode
    {
        private StateMachine _machine;
        private PatchOperation _owner;

        void IStateNode.OnCreate(StateMachine machine)
        {
            _machine = machine;
            _owner = machine.Owner as PatchOperation;
        }

        void IStateNode.OnEnter()
        {
            Debug.Log("[ForgeCLR] 更新资源清单");
            UpdateManifest().Forget();
        }

        void IStateNode.OnUpdate()
        {
        }

        void IStateNode.OnExit()
        {
        }

        private async UniTaskVoid UpdateManifest()
        {
            var packageName = (string)_machine.GetBlackboardValue("PackageName");
            var packageVersion = (string)_machine.GetBlackboardValue("PackageVersion");
            var package = YooAssets.GetPackage(packageName);
            var operation = package.UpdatePackageManifestAsync(packageVersion);
            await operation.ToUniTask();

            if (operation.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"[ForgeCLR] Package manifest update failed: {operation.Error}");
                _owner.SetError(operation.Error);
                return;
            }

            _machine.ChangeState<FsmCreateDownloader>();
        }
    }
}
