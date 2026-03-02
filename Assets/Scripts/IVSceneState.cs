using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class IVSceneState : MonoBehaviour
{
    [SerializeField] protected List<IVCoroutineModule> _modules;
    [Space]
    [SerializeField] protected IVGameEventHandler _raiseEvent;
    [Space]
    [SerializeField] protected IVSceneState _nextState;

    public IVSceneStateMachine StateMachine { get; protected set; }

    public float Progress { get; protected set; } = 0f;

    private readonly List<IVCoroutineModule> _emptyModules = new List<IVCoroutineModule>();

    protected virtual void Awake()
    {
        /* StateMachine
         *   ㄴ StateA
         *   ㄴ StateB
         *   ㄴ StateC   
         */  
        StateMachine = GetComponentInParent<IVSceneStateMachine>();
        gameObject.SetActive(false);
    }

    protected virtual void OnEnable()
    {
        Debug.Log($"Enter State:: {transform.name}");
        StartCoroutine(HandleCoroutine());
    }

    // 상태 전이 시 코루틴 종료 자동 종료 됨
    protected virtual IEnumerator HandleCoroutine()
    {
        yield return ExecuteModules();

        _raiseEvent?.RaiseEvent();

        StateMachine.TransitionTo(_nextState);
    }

    protected IEnumerator ExecuteModules()
    {
        _emptyModules.Clear();

        Progress = 0f;

        float doneProgress = 1f / (float)_modules.Count;

        for (int i = 0; i < _modules.Count; i++)
        {
            IEnumerator coroutine = _modules[i].GetCoroutine();
            if (coroutine != null)
            {
                yield return StartCoroutine(coroutine);
                Progress += doneProgress;
            }
            else
                _emptyModules.Add(_modules[i]);
        }

        for (int i = 0; i < _emptyModules.Count; i++)
            _modules.Remove(_emptyModules[i]);

        Progress = 1f;
    }
}