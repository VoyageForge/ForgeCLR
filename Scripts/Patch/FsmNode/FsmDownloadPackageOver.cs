using UnityEngine;

namespace VoyageForge.ForgeCLR.Runtime
{
    internal class FsmDownloadPackageOver : IStateNode
    {
        private StateMachine _machine;

        void IStateNode.OnCreate(StateMachine machine)
        {
            _machine = machine;
        }

        void IStateNode.OnEnter()
        {
            LauncherStatus.Instance.Log("[ForgeCLR] 资源文件下载完毕");
            _machine.ChangeState<FsmClearCacheBundle>();
        }

        void IStateNode.OnUpdate()
        {
        }

        void IStateNode.OnExit()
        {
        }
    }
}
