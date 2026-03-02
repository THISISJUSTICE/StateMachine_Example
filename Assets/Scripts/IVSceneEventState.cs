using UnityEngine;
using System.Collections;

public class IVSceneEventState : IVSceneState
{
    private bool _moveReady;
    private WaitUntil _waitUntil;

    protected override void Awake()
    {
        base.Awake();

        _waitUntil = new WaitUntil(() => _moveReady);
    }

    public void MoveNextState()
    {
        if (StateMachine.CurrentState == this)
            StartCoroutine(MoveStateEvent(_nextState));
    }

    protected override IEnumerator HandleCoroutine()
    {
        _moveReady = false;

        yield return ExecuteModules();

        _raiseEvent?.RaiseEvent();

        _moveReady = true;
    }

    protected IEnumerator MoveStateEvent(IVSceneState state)
    {
        yield return null;
        yield return _waitUntil;

        StateMachine.TransitionTo(state);
    }
}