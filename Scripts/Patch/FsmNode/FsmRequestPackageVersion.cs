using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    internal class FsmRequestPackageVersion : IStateNode
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
            LauncherStatus.Instance.Log("[ForgeCLR] 请求资源版本");
            UpdatePackageVersion().Forget();
        }

        void IStateNode.OnUpdate()
        {
        }

        void IStateNode.OnExit()
        {
        }

        private async UniTaskVoid UpdatePackageVersion()
        {
            var packageName = (string)_machine.GetBlackboardValue("PackageName");
            var package = YooAssets.GetPackage(packageName);
            var operation = package.RequestPackageVersionAsync();
            await operation.ToUniTask();

            if (operation.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"[ForgeCLR] Package version request failed: {operation.Error}");
                _owner.SetError(operation.Error);
                return;
            }

            Debug.Log($"[ForgeCLR] Request package version: {operation.PackageVersion}");
            _machine.SetBlackboardValue("PackageVersion", operation.PackageVersion);
            _machine.ChangeState<FsmUpdatePackageManifest>();
        }
    }
}
