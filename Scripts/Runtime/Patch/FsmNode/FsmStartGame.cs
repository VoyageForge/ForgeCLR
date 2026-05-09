using UnityEngine;

namespace VoyageForge.ForgeCLR.Runtime
{
    internal class FsmStartGame : IStateNode
    {
        private PatchOperation _owner;

        void IStateNode.OnCreate(StateMachine machine)
        {
            _owner = machine.Owner as PatchOperation;
        }

        void IStateNode.OnEnter()
        {
            Debug.Log("[ForgeCLR] 补丁流程完成");
            _owner.SetFinish();
        }

        void IStateNode.OnUpdate()
        {
        }

        void IStateNode.OnExit()
        {
        }
    }
}
