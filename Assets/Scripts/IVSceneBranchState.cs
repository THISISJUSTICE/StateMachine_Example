public class IVSceneBranchState : IVSceneEventState
{
    public void MoveBranchState(IVSceneState branchState)
    {
        if (StateMachine.CurrentState == this)
            StartCoroutine(MoveStateEvent(branchState));
    }
}