using UnityEngine.Events;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using System;

[CustomEditor(typeof(IVSceneStateMachine))]
public class IVStateMachineInspector : Editor
{
    private const string EVENT_VARIABLE_NAME = "_onRaisedEvent";

    private IVSceneStateMachine _stateMachine;
    private IVGameEventHandler[] _eventHandlers;
    private SerializedObject[] _eventSerializedObjects;

    private IVSceneState _initState;
    private readonly Dictionary<IVSceneState, IVSceneState> _nextStates = new Dictionary<IVSceneState, IVSceneState>();
    private readonly Dictionary<IVSceneState, List<IVSceneState>> _prevStates = new Dictionary<IVSceneState, List<IVSceneState>>();
    private readonly Dictionary<IVSceneEventState, List<IVGameEventHandler>> _eventStates = new Dictionary<IVSceneEventState, List<IVGameEventHandler>>();
    private readonly Dictionary<IVSceneBranchState, Dictionary<IVSceneState, List<IVGameEventHandler>>> _branchStates = new Dictionary<IVSceneBranchState, Dictionary<IVSceneState, List<IVGameEventHandler>>>();
    private readonly Dictionary<IVSceneBranchState, List<IVGameEventHandler>> _branchNones = new Dictionary<IVSceneBranchState, List<IVGameEventHandler>>();

    private readonly List<IVSceneState> _inaccessibleStates = new List<IVSceneState>();

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GUILayout.Space(20f);
        if (GUILayout.Button("Draw"))
        {
            _stateMachine = (IVSceneStateMachine)target;

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
                _eventHandlers = prefabStage.prefabContentsRoot.GetComponentsInChildren<IVGameEventHandler>();
            else
                _eventHandlers = FindObjectsByType<IVGameEventHandler>(FindObjectsSortMode.None);
            _eventSerializedObjects = new SerializedObject[_eventHandlers.Length];
            for (int i = 0; i < _eventSerializedObjects.Length; i++)
                _eventSerializedObjects[i] = new SerializedObject(_eventHandlers[i]);

            _nextStates.Clear();
            _prevStates.Clear();
            _eventStates.Clear();
            _branchStates.Clear();
            _branchNones.Clear();

            _inaccessibleStates.Clear();

            FindStateFlow();
            FindInaccessibleStates();

            IVStateFlowDrawer.ShowWindow(_stateMachine, _initState,
                _nextStates, _prevStates, _eventStates, _branchStates, _branchNones, 
                _inaccessibleStates);
        }
    }

    private void FindStateFlow()
    {
        _initState = (IVSceneState)FindField(typeof(IVSceneStateMachine), "_initState", _stateMachine);
        if (_initState == null)
            return;

        // Check Event, Branch
        CheckAdditionalState(_initState);

        // Find Next
        FindNextState(_initState);

        // Check Branch None
        foreach (IVSceneBranchState branchState in _branchStates.Keys)
        {
            if (_branchStates[branchState].Count == 0 && !_branchNones.ContainsKey(branchState))
                _branchNones.Add(branchState, new List<IVGameEventHandler>());
        }
    }

    private void FindNextState(IVSceneState prevState)
    {
        // Check Next State
        IVSceneState nextState = (IVSceneState)FindField(typeof(IVSceneState), "_nextState", prevState);
        if (nextState == null)
        {
            _nextStates[prevState] = null;
            return;
        }

        _nextStates[prevState] = nextState;

        // Set PrevStates
        if (!_prevStates.ContainsKey(nextState))
            _prevStates.Add(nextState, new List<IVSceneState>());
        _prevStates[nextState].Add(prevState);

        // Check Already Checked
        if (_nextStates.ContainsKey(nextState))
            return;

        // Check Event, Branch
        CheckAdditionalState(nextState);        

        // Find Next
        FindNextState(nextState);
    }

    private void FindNextStateEvents(IVSceneEventState eventState)
    {
        // Check Duplication
        if (_eventStates.ContainsKey(eventState))
            return;
            
        _eventStates.Add(eventState, new List<IVGameEventHandler>());

        // Check Event
        for (int i = 0; i < _eventHandlers.Length; i++)
        {
            UnityEvent unityEvent = (UnityEvent)FindField(typeof(IVGameEventHandler), EVENT_VARIABLE_NAME, _eventHandlers[i]);
            int length = unityEvent.GetPersistentEventCount();

            for (int j = 0; j < length; j++)
            {
                if (eventState != unityEvent.GetPersistentTarget(j)
                    || "MoveNextState" != unityEvent.GetPersistentMethodName(j))
                    continue;

                _eventStates[eventState].Add(_eventHandlers[i]);
            }
        }
    }

    private void FindBranchStates(IVSceneBranchState branchState)
    {
        // Check Duplication
        if (_branchStates.ContainsKey(branchState))
            return;

        _branchStates.Add(branchState, new Dictionary<IVSceneState, List<IVGameEventHandler>>());

        // Check Event
        for (int i = 0; i < _eventHandlers.Length; i++)
        {
            UnityEvent unityEvent = (UnityEvent)FindField(typeof(IVGameEventHandler), EVENT_VARIABLE_NAME, _eventHandlers[i]);
            SerializedProperty eventProp = _eventSerializedObjects[i].FindProperty(EVENT_VARIABLE_NAME);
            SerializedProperty calls = eventProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            int length = unityEvent.GetPersistentEventCount();

            for (int j = 0; j < length; j++)
            {
                if (branchState != unityEvent.GetPersistentTarget(j)
                    || "MoveBranchState" != unityEvent.GetPersistentMethodName(j))
                    continue;

                SerializedProperty method = calls.GetArrayElementAtIndex(j);
                SerializedProperty args = method.FindPropertyRelative("m_Arguments");
                SerializedProperty objArg = args.FindPropertyRelative("m_ObjectArgument");
                UnityEngine.Object obj = objArg.objectReferenceValue;

                if (obj != null)
                {
                    IVSceneState state = (IVSceneState)obj;
                    if (!_branchStates[branchState].ContainsKey(state))
                        _branchStates[branchState].Add(state, new List<IVGameEventHandler>());

                    _branchStates[branchState][state].Add(_eventHandlers[i]);
                }
                else
                { 
                    if(!_branchNones.ContainsKey(branchState))
                        _branchNones.Add(branchState, new List<IVGameEventHandler>());

                    _branchNones[branchState].Add(_eventHandlers[i]);
                }
            }
        }

        // Find Next
        foreach (IVSceneState state in _branchStates[branchState].Keys)
        {
            if (!_nextStates.ContainsKey(state))
                CheckAdditionalState(state);
            FindNextState(state);
        }
    }

    private void CheckAdditionalState(IVSceneState state)
    {
        if (state is IVSceneEventState eventState)
        {
            FindNextStateEvents(eventState);

            if (eventState is IVSceneBranchState branchState)
                FindBranchStates(branchState);
        }
    }

    private object FindField(Type type, string fieldName, object instance)
    {
        return type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance);
    }

    private void FindInaccessibleStates()
    {
        HashSet<IVSceneState> states = new HashSet<IVSceneState>(GameObject.FindObjectsByType<IVSceneState>(FindObjectsSortMode.None));

        foreach (IVSceneState nextState in _nextStates.Keys)
            states.Remove(nextState);
        
        foreach(IVSceneState state in states)
            _inaccessibleStates.Add(state);
    }
}