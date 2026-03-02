using System;
using System.Collections;
using UnityEngine;

public class IVSceneStateMachine : MonoBehaviour
{
    [SerializeField] private IVSceneState _initState;

    public IVSceneState CurrentState { get; private set; }

    private void Start()
    {
        CurrentState = _initState;
        CurrentState.gameObject.SetActive(true);
    }

    private void OnDestroy()
    {
        Resources.UnloadUnusedAssets();
        GC.Collect();
    }

    public void TransitionTo(IVSceneState nextState)
    {
        if (CurrentState == nextState)
            return; // 중복 전이 방지

        StartCoroutine(TransitionCoroutine(nextState));              
    }    

    private IEnumerator TransitionCoroutine(IVSceneState nextState)
    {
        CurrentState.gameObject.SetActive(false);
        yield return null;          // 상태 변환 사이에 한 프레임 쉬고 싶음
        CurrentState = nextState;
        CurrentState.gameObject.SetActive(true);
    }
}