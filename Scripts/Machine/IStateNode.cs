namespace VoyageForge.ForgeCLR.Runtime
{
    /// <summary>
    /// 补丁流程状态节点接口。
    /// </summary>
    public interface IStateNode
    {
        /// <summary>
        /// 创建状态节点时调用。
        /// </summary>
        /// <param name="machine">拥有该节点的状态机。</param>
        void OnCreate(StateMachine machine);

        /// <summary>
        /// 进入状态节点时调用。
        /// </summary>
        void OnEnter();

        /// <summary>
        /// 状态节点更新时调用。
        /// </summary>
        void OnUpdate();

        /// <summary>
        /// 离开状态节点时调用。
        /// </summary>
        void OnExit();
    }
}
