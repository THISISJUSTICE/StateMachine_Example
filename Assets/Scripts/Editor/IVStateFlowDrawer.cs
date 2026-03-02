using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class IVStateFlowDrawer : EditorWindow
{
    private enum DirectionEnum
    { 
        Up, Down, Left, Right
    }

    private IVSceneStateMachine _stateMachine;
    private IVSceneState _initState;
    private Dictionary<IVSceneState, IVSceneState> _nextStates;
    private Dictionary<IVSceneState, List<IVSceneState>> _prevStates;
    private Dictionary<IVSceneEventState, List<IVGameEventHandler>> _eventStates;
    private Dictionary<IVSceneBranchState, Dictionary<IVSceneState, List<IVGameEventHandler>>> _branchStates;
    private Dictionary<IVSceneBranchState, List<IVGameEventHandler>> _branchNones;
    private List<IVSceneState> _inaccessibleStates;

    private Rect _startRect;
    private readonly Dictionary<IVSceneState, int> _mainStateIndex = new Dictionary<IVSceneState, int>();
    private readonly Dictionary<IVSceneState, Rect> _stateRects = new Dictionary<IVSceneState, Rect>();

    private (IVSceneState, IVSceneState) _turnBackState;
    private readonly Dictionary<IVSceneEventState, Vector2> _eventStateScrolls = new Dictionary<IVSceneEventState, Vector2>();

    private readonly List<IVSceneBranchState> _mainBranches = new List<IVSceneBranchState>();
    private readonly Dictionary<IVSceneBranchState, int> _branchTabIndex = new Dictionary<IVSceneBranchState, int>();
    private readonly Dictionary<IVSceneBranchState, Rect> _branchTabRects = new Dictionary<IVSceneBranchState, Rect>();
    private readonly Dictionary<IVSceneBranchState, Vector2> _branchTabScrolls = new Dictionary<IVSceneBranchState, Vector2>();
    private readonly Dictionary<IVSceneBranchState, Vector2> _branchTabEventScrolls = new Dictionary<IVSceneBranchState, Vector2>();
    private readonly Dictionary<IVSceneBranchState, List<IVSceneState>> _branchTabPaths = new Dictionary<IVSceneBranchState, List<IVSceneState>>();

    private readonly Dictionary<IVSceneState, Rect> _derivatedStateRects = new Dictionary<IVSceneState, Rect>();
    private readonly Dictionary<IVSceneState, IVSceneState> _derivatedStateParents = new Dictionary<IVSceneState, IVSceneState>();
    private readonly Dictionary<IVSceneState, IVSceneBranchState> _parentBranches = new Dictionary<IVSceneState, IVSceneBranchState>();

    private IVSceneState _selectedVirtualState;
    private float _virtualStateSelectTime;
    
    private Vector2 _inaccessibleStateScroll;

    private const float HORIZONTAL_LINE_OFFSET = 7f;
    private const float VERTICAL_LINE_OFFSET = 3f;
    private const float ARROW_WIDTH = 14f;
    private const float ARROW_HEIGHT = 18f;

    private const float MIN_BUTTON_WIDTH = 50f;
    private const float MAX_BUTTON_WIDTH = 200f;
    private const float BUTTON_WIDTH = 9f;
    private const float BUTTON_HEIGHT = 19f;
    private const float NEXT_HEIGHT = BUTTON_HEIGHT * 4;
    private const float TURN_BACK_OFFSET = 150f;

    private const string NONE_TEXT = "<None>";
    private static readonly float NONE_BUTTON_WIDTH = NONE_TEXT.Length * BUTTON_WIDTH + 5f;

    private const float EVENT_BOX_WIDTH = 100f;
    private const float EVENT_BOX_ADD_HEIGHT = 15f;
    private const float EVENT_OFFSET_X = 10f;

    private const float BRANCH_RIGHT_OFFSET = 80f;
    private const float BRANCH_TAB_WIDTH = 70f;
    private const float BRANCH_TAB_HEIGHT = 30f;
    private const float BRANCH_LINE_LENGTH = 150f;

    private const float VIRTUAL_BUTTON_BORDER_SIZE = 5f;

    private static readonly Color SELECTED_STATE_COLOR = new Color(0f, 0f, 0.5f);
    private const float SELECT_KEEP_TIME = 5f;

    private Rect _maxXRect;
    private Rect _maxYRect;
    private Vector2 _windowSize;
    private Vector2 _scrollPosition;

    private GUIStyle _labelStyle;

    public static void ShowWindow(IVSceneStateMachine stateMachine, IVSceneState initState,
        Dictionary<IVSceneState, IVSceneState> nextStates,
        Dictionary<IVSceneState, List<IVSceneState>> prevStates,
        Dictionary<IVSceneEventState, List<IVGameEventHandler>> eventStates,
        Dictionary<IVSceneBranchState, Dictionary<IVSceneState, List<IVGameEventHandler>>> branchStates,
        Dictionary<IVSceneBranchState, List<IVGameEventHandler>> branchNones,
        List<IVSceneState> inaccessibleStates)
    {
        IVStateFlowDrawer window = GetWindow<IVStateFlowDrawer>("State Flow Chart");

        window._stateMachine = stateMachine;
        window._initState = initState;
        window._nextStates = nextStates;
        window._prevStates = prevStates;
        window._eventStates = eventStates;
        window._branchStates = branchStates;    
        window._branchNones = branchNones;
        window._inaccessibleStates = inaccessibleStates;

        window.Initialize();

        window.Show();
    }

    private void Initialize()
    {
        _mainStateIndex.Clear();
        _stateRects.Clear();

        _turnBackState = (null, null);
        _eventStateScrolls.Clear();

        _mainBranches.Clear();
        _branchTabIndex.Clear();
        _branchTabRects.Clear();
        _branchTabScrolls.Clear();
        _branchTabPaths.Clear();

        _derivatedStateRects.Clear();
        _derivatedStateParents.Clear();
        _parentBranches.Clear();

        _selectedVirtualState = null;
        _virtualStateSelectTime = 0f;

        _inaccessibleStateScroll = new Vector2();

        _maxXRect = new Rect();
        _maxYRect = new Rect();
        _windowSize = new Vector2();

        SetRects();
        SetWindowSize();

        _labelStyle = new GUIStyle(EditorStyles.boldLabel);
        _labelStyle.alignment = TextAnchor.MiddleCenter;
    }

    private void SetRects()
    {
        // Next States
        _startRect = new Rect(150f, 20f, _stateMachine.name.Length * BUTTON_WIDTH, BUTTON_HEIGHT);

        if (_initState == null)
            return;

        _stateRects[_initState] = GetNextStateRect(_initState.name, GetStateCenterRect(_startRect));
        _mainStateIndex[_initState] = 0;
        SetNextStateRect(_initState);

        // Check Max Rect
        foreach (Rect stateRect in _stateRects.Values) 
            CheckMaxRect(stateRect); 

        // Event States
        foreach (IVSceneEventState eventState in _eventStates.Keys)
        {
            _eventStateScrolls.Add(eventState, new Vector2());
        }

        // Branch States
        FindMainBranches();
        foreach (IVSceneBranchState branchState in _mainBranches)
            SetBranchStateRect(branchState, _stateRects[branchState], true);
    }

    private void SetWindowSize()
    {
        _windowSize.x = _maxXRect.x + _maxXRect.width + NONE_BUTTON_WIDTH;
        _windowSize.y = _maxYRect.y + _maxYRect.height;

        _windowSize.x = Mathf.Max(_windowSize.x, _startRect.x + _startRect.width + EVENT_BOX_WIDTH + BRANCH_RIGHT_OFFSET);
        _windowSize.y = Mathf.Max(_windowSize.y, _startRect.y + _startRect.height + BUTTON_HEIGHT);

        _windowSize.x += 50f;
        _windowSize.y += NEXT_HEIGHT * 2f + BUTTON_HEIGHT;
    }

    private void SetNextStateRect(IVSceneState prevState)
    {
        if (!_nextStates.ContainsKey(prevState) || _nextStates[prevState] == null)
            return;

        IVSceneState nextState = _nextStates[prevState];
        if (_stateRects.ContainsKey(nextState))
        {
            _turnBackState = (prevState, nextState);
            return;
        }
                
        _stateRects[nextState] = GetNextStateRect(nextState.name, GetStateCenterRect(_stateRects[prevState]));
        _mainStateIndex[nextState] = _mainStateIndex[prevState] + 1;
        SetNextStateRect(nextState);
    }

    private Rect GetNextStateRect(string stateName, Rect prevCenterRect)
    {
        Rect rect = new Rect();
        rect.width = Mathf.Max(stateName.Length * BUTTON_WIDTH, MIN_BUTTON_WIDTH);
        rect.width = Mathf.Min(rect.width, MAX_BUTTON_WIDTH);
        rect.height = BUTTON_HEIGHT;
        rect.x = prevCenterRect.x - rect.width * 0.5f;
        rect.y = prevCenterRect.y + prevCenterRect.height * 0.5f + NEXT_HEIGHT;

        return rect;
    }

    private void FindMainBranches()
    {
        HashSet<IVSceneBranchState> mainBranches = new HashSet<IVSceneBranchState>();

        foreach (IVSceneBranchState branchState in _branchStates.Keys)
        {
            _branchTabIndex.Add(branchState, 0);

            if (_stateRects.ContainsKey(branchState))
                mainBranches.Add(branchState);
        }

        foreach (IVSceneBranchState branchState in _branchNones.Keys)
        {
            if (!_branchTabIndex.ContainsKey(branchState))
                _branchTabIndex.Add(branchState, 0);

            if (_stateRects.ContainsKey(branchState))
                mainBranches.Add(branchState);
        }

        foreach (IVSceneBranchState branchState in mainBranches)
            _mainBranches.Add(branchState);
        _mainBranches.Sort((a, b) => _mainStateIndex[a] - _mainStateIndex[b]);
    }

    private void SetBranchStateRect(IVSceneBranchState branchState, Rect stateRect, bool mainBranch = false)
    { 
        // Set Branch Rect
        Rect branchRect = GetStateCenterRect(stateRect);
        branchRect.width = BRANCH_TAB_WIDTH;
        branchRect.height = BRANCH_TAB_HEIGHT;
        branchRect.x = _maxXRect.x + _maxXRect.width + BRANCH_RIGHT_OFFSET;
        if (mainBranch)
            branchRect.y += branchRect.height * 0.5f;
        else
            branchRect.y -= branchRect.height * 0.5f;

        // Check Max Rect        
        CheckMaxRect(GetBranchTabEdgeRect(branchRect));

        // Add GUI
        _branchTabRects.Add(branchState, branchRect);
        _branchTabScrolls.Add(branchState, new Vector2());
        if (!_branchTabEventScrolls.ContainsKey(branchState))
            _branchTabEventScrolls.Add(branchState, new Vector2());

        // Set Branch Tab Paths
        List<IVSceneState> branchPaths = new List<IVSceneState>();
        if (_branchNones.ContainsKey(branchState))
            branchPaths.Add(null);
        foreach (IVSceneState nextState in _branchStates[branchState].Keys)
            branchPaths.Add(nextState);
        _branchTabPaths[branchState] = branchPaths;

        // Set Branch Center Rect
        Rect stateCenterRect = branchRect;
        stateCenterRect.x += branchRect.width + BRANCH_LINE_LENGTH;
        stateCenterRect.y = _maxYRect.y;

        // Set Derivated State Rects
        foreach (IVSceneState state in _branchStates[branchState].Keys)
        {
            if (_stateRects.ContainsKey(state) || _derivatedStateRects.ContainsKey(state))
                continue;

            SetDerivatedStateRect(stateCenterRect, branchState, state);

            if (state is IVSceneBranchState derivatedBranch)
            {
                SetBranchStateRect(derivatedBranch, _derivatedStateRects[state]);
            }
        }
    }

    private void SetDerivatedStateRect(Rect parentCenterRect, IVSceneState parentState, IVSceneState state)
    {
        Rect rect = GetNextStateRect(state.name, parentCenterRect);
        _derivatedStateRects.Add(state, rect);
        _derivatedStateParents.Add(state, parentState);

        CheckMaxRect(rect);

        if (!_nextStates.ContainsKey(state) || _nextStates[state] == null)
            return;

        IVSceneState nextState = _nextStates[state];

        if (_stateRects.ContainsKey(nextState) || _derivatedStateRects.ContainsKey(nextState))
            return;

        SetDerivatedStateRect(GetStateCenterRect(_derivatedStateRects[state]), state, nextState);
    }

    private void CheckMaxRect(Rect rect)
    { 
        if(rect.x + rect.width > _maxXRect.x + _maxXRect.width)
            _maxXRect = rect;

        if(rect.y + rect.height > _maxYRect.y + _maxYRect.height)
            _maxYRect = rect;
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(position.width), GUILayout.Height(position.height));

        DrawStateFlow();
        DrawInaccessibleStates();

        GUILayout.Space(_windowSize.y);
        GUILayout.BeginHorizontal();
        GUILayout.Space(_windowSize.x);
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView();

        if (_selectedVirtualState != null)
        {
            float selectedTime = Time.realtimeSinceStartup - _virtualStateSelectTime;
            if (selectedTime >= SELECT_KEEP_TIME)
                _selectedVirtualState = null;
        }
    }

    private void DrawStateFlow()
    {
        // StateMachine
        DrawSelectButton(_startRect, _stateMachine);

        if (_initState == null || _nextStates == null || _nextStates.Count == 0)
            return;

        DrawNextArrow(GetStateCenterRect(_startRect), GetStateCenterRect(_stateRects[_initState]));

        // Next States
        DrawNextState(_initState);

        // Turn Back State
        DrawTurnBackArrows();

        // Event States
        foreach (IVSceneEventState eventState in _eventStates.Keys)
        {
            if (!_stateRects.ContainsKey(eventState))
                continue;

            Rect stateRect = GetStateCenterRect(_stateRects[eventState]);
            Rect eventRect = stateRect;
            eventRect.x = stateRect.x - EVENT_OFFSET_X;
            eventRect.y = stateRect.y + stateRect.height * 0.5f + 5f;

            _eventStateScrolls[eventState] = DrawContentObjects(eventRect, _eventStateScrolls[eventState], _eventStates[eventState]);
        }

        // Branch States
        if (_mainBranches.Count > 0)
        {
            foreach (IVSceneBranchState branchState in _mainBranches)
            {
                DrawBranchTab(branchState);
                DrawBranchPath(branchState, _stateRects[branchState], true);
            }
        }
    }

    private void DrawInaccessibleStates()
    {
        if (_inaccessibleStates.Count == 0)
            return;

        const string labelText = "Inaccessible States";

        // Set Contents Rect
        Rect contentRect = _startRect;
        contentRect.width = EVENT_BOX_WIDTH;
        contentRect.height = BUTTON_HEIGHT;
        contentRect.x += _startRect.width + BRANCH_RIGHT_OFFSET + contentRect.width;
        contentRect.y += contentRect.height;

        // Set Label Rect
        Rect labelRect = contentRect;
        labelRect.width = labelText.Length * 7f;
        labelRect.y -= contentRect.height - 5f;
        labelRect.x -= (labelRect.width + contentRect.width) * 0.5f;

        // Set Color Rect
        Rect colorRect = labelRect;
        colorRect.height += contentRect.height + EVENT_BOX_ADD_HEIGHT + 10f;

        EditorGUI.DrawRect(colorRect, new Color(0.5f, 0.15f, 0.15f));
        _inaccessibleStateScroll = DrawContentObjects(contentRect, _inaccessibleStateScroll, _inaccessibleStates);
        EditorGUI.LabelField(labelRect, labelText, _labelStyle);
    }

    private void DrawNextState(IVSceneState state)
    {
        Rect rect = _stateRects[state];
        Rect centerRect = GetStateCenterRect(rect);

        DrawSelectButton(rect, state);

        IVSceneState nextState = _nextStates[state];
        if (nextState == null)
        {
            DrawNonePath(centerRect);
            return;
        }

        DrawNextArrow(centerRect, GetStateCenterRect(_stateRects[nextState]));

        if (_turnBackState.Item1 != nextState)
            DrawNextState(nextState);
        else
            DrawSelectButton(_stateRects[nextState], nextState);
    }

    private void DrawTurnBackArrows()
    {
        if (_turnBackState.Item1 == null || _turnBackState.Item2 == null)
            return;

        IVSceneState prevState, nextState;
        (prevState, nextState) = _turnBackState;

        DrawTurnBackArrow(_stateRects[prevState], _stateRects[nextState]);
    }

    private Vector2 DrawContentObjects<T>(Rect objectRect, Vector2 scroll, IList<T> contentObjects) where T : UnityEngine.Object
    {
        if (contentObjects.Count == 0) // None
        {
            objectRect.x -= NONE_BUTTON_WIDTH;
            DrawNone(objectRect.x, objectRect.y);
        }
        else if (contentObjects.Count == 1 && contentObjects[0].name.Length < 10) // Short and One Event
        {
            T eventHandler = contentObjects[0];

            objectRect.width = eventHandler.name.Length * BUTTON_WIDTH;
            objectRect.x -= objectRect.width;

            DrawSelectButton(objectRect, eventHandler);
        }
        else // Multiple Events
        {
            // Draw Box
            const float offset = 5f;
            Vector2 boxSize = new Vector2(EVENT_BOX_WIDTH, BUTTON_HEIGHT + EVENT_BOX_ADD_HEIGHT);
            Rect boxRect = new Rect();
            boxRect.width = boxSize.x;
            boxRect.height = boxSize.y;
            boxRect.x = objectRect.x - boxRect.width;
            boxRect.y = objectRect.y + EVENT_BOX_ADD_HEIGHT * 0.5f;

            GUI.Box(boxRect, GUIContent.none);

            // Draw Content and Scroll
            Rect contentRect = new Rect(0, 0, (contentObjects.Count + 1) * offset, EVENT_BOX_ADD_HEIGHT + 10f);
            foreach (T contentObject in contentObjects)
                contentRect.width += contentObject.name.Length * BUTTON_WIDTH;

            scroll = GUI.BeginScrollView(boxRect, scroll, contentRect);
            GUILayout.BeginArea(contentRect);

            // Draw Content: Buttons
            objectRect = new Rect(offset, 0f, 0f, BUTTON_HEIGHT);
            foreach (T contentObject in contentObjects)
            {
                objectRect.width = contentObject.name.Length * BUTTON_WIDTH;
                DrawSelectButton(objectRect, contentObject);

                objectRect.x += objectRect.width + offset;
            }

            GUILayout.EndArea();
            GUI.EndScrollView();

            return scroll;
        }

        return Vector2.zero;
    }

    private void DrawBranchTab(IVSceneBranchState branchState)
    {
        // Draw Box
        Rect branchTabRect = _branchTabRects[branchState];
        GUI.Box(branchTabRect, GUIContent.none);

        // Check Branch Count
        int length = _branchStates[branchState].Count;
        if (_branchNones.ContainsKey(branchState))
            length += 1;

        if (length <= 1)
            return;

        // Set Branch Count
        string[] texts = new string[length];

        for (int i = 0; i < texts.Length; i++)
            texts[i] = (i + 1).ToString();

        const float contentSize = 20f;
        const float offset = 2f;

        // Draw Content and Scroll
        Rect contentRect = new Rect(0f, 0f, contentSize, contentSize);
        contentRect.width = (contentSize + offset) * texts.Length + offset;

        _branchTabScrolls[branchState] = GUI.BeginScrollView(branchTabRect, _branchTabScrolls[branchState], contentRect);
        GUILayout.BeginArea(contentRect);

        // Draw Content: Tab Buttons
        Rect tabButtonRect = new Rect(0f, 0f, contentSize, contentSize);
        for (int i = 0; i < texts.Length; i++)
        {
            tabButtonRect.width = Mathf.Max(contentSize, texts[i].Length * BUTTON_WIDTH);

            if (i == _branchTabIndex[branchState])
            {
                EditorGUI.DrawRect(tabButtonRect, new Color(0.1f, 0.1f, 0.1f));
                EditorGUI.LabelField(tabButtonRect, texts[i], _labelStyle);
            }
            else
            {
                if (GUI.Button(tabButtonRect, texts[i]))
                    _branchTabIndex[branchState] = i;
            }

            tabButtonRect.x += tabButtonRect.width + offset;
        }

        GUILayout.EndArea();
        GUI.EndScrollView();
    }

    private void DrawBranchPath(IVSceneBranchState branchState, Rect stateRect, bool mainBranch = false)
    {
        // Set Rects
        Rect branchTabEdgeRect = GetBranchTabEdgeRect(_branchTabRects[branchState]);
        Rect eventRect = branchTabEdgeRect;
        eventRect.x -= EVENT_OFFSET_X;
        eventRect.height = BUTTON_HEIGHT;
        eventRect.y += BUTTON_HEIGHT * 0.5f;

        // Draw Tab Lines
        Rect tabRect = _branchTabRects[branchState];
        float x = stateRect.x + HORIZONTAL_LINE_OFFSET + stateRect.width;
        float y = tabRect.y + tabRect.height * 0.5f;
        
        if (mainBranch)
        {
            x = stateRect.x + stateRect.width * 0.75f;
            float verticalY = stateRect.y + stateRect.height + VERTICAL_LINE_OFFSET;
            DrawVerticalLine(x, verticalY, y - verticalY);
        }

        DrawHorizontalLine(x, y, tabRect.x - x - HORIZONTAL_LINE_OFFSET);
        DrawHorizontalLine(tabRect.x + tabRect.width + HORIZONTAL_LINE_OFFSET, y, BRANCH_LINE_LENGTH);

        // Select Path
        IVSceneState nextState = null;
        if (_branchTabPaths[branchState] != null && _branchTabPaths[branchState].Count > 0)
            nextState = _branchTabPaths[branchState][_branchTabIndex[branchState]];

        // Check NextState None
        if (nextState == null)
        {
            // Draw Event
            _branchTabEventScrolls[branchState] = DrawContentObjects(eventRect,
                _branchTabEventScrolls[branchState], _branchNones[branchState]);

            // Draw None and Arrow
            DrawArrow(branchTabEdgeRect.x, branchTabEdgeRect.y - ARROW_HEIGHT * 0.5f, DirectionEnum.Right);
            DrawNone(branchTabEdgeRect.x + ARROW_WIDTH + 5f, branchTabEdgeRect.y - BUTTON_HEIGHT * 0.5f);
            return;
        }

        // Draw Path Event
        _branchTabEventScrolls[branchState] = DrawContentObjects(eventRect, _branchTabEventScrolls[branchState], _branchStates[branchState][nextState]);

        // Check MainBranch Path
        if (_stateRects.ContainsKey(nextState))
        {
            Rect nextStateRect = GetStateCenterRect(_stateRects[nextState]);
            y = Mathf.Min(nextStateRect.y, branchTabEdgeRect.y);

            DrawVerticalLine(branchTabEdgeRect.x, y, Mathf.Abs(nextStateRect.y - branchTabEdgeRect.y));

            x = nextStateRect.x + nextStateRect.width * 0.5f + ARROW_WIDTH + HORIZONTAL_LINE_OFFSET * 0.5f;
            DrawHorizontalLine(x, nextStateRect.y, branchTabEdgeRect.x - x);
            DrawArrow(nextStateRect.x + nextStateRect.width * 0.5f + HORIZONTAL_LINE_OFFSET * 0.5f, nextStateRect.y - ARROW_HEIGHT * 0.5f, DirectionEnum.Left);
            return;
        }

        // Check Virtual State
        if (_derivatedStateParents[nextState] != branchState)
        {
            DrawVirtualState(branchTabEdgeRect, nextState);
            return;
        }

        // Derivated State
        DrawDerivatedState(branchTabEdgeRect, branchState, nextState, true);
    }

    private void DrawDerivatedState(Rect prevStateCenterRect, IVSceneState prevState, IVSceneState nextState, bool tab = false)
    {
        // Check Virtual State
        if (_derivatedStateParents[nextState] != prevState)
        {
            prevStateCenterRect.y += VERTICAL_LINE_OFFSET;
            DrawVirtualState(prevStateCenterRect, nextState);
            return;
        }

        // Draw Arrow
        Rect nextStateRect = _derivatedStateRects[nextState];
        Rect nextStateCenterRect = GetStateCenterRect(nextStateRect);

        if (tab)
        {
            DrawVerticalLine(prevStateCenterRect.x, prevStateCenterRect.y,
                nextStateCenterRect.y - prevStateCenterRect.y - ARROW_HEIGHT - nextStateCenterRect.height * 0.5f);
        }
        DrawNextArrow(prevStateCenterRect, nextStateCenterRect);
        DrawSelectButton(nextStateRect, nextState);

        // Check Event and Branch
        if (nextState is IVSceneEventState eventState)
        {
            Rect eventRect = nextStateCenterRect;
            eventRect.x = nextStateCenterRect.x - EVENT_OFFSET_X;
            eventRect.y = nextStateCenterRect.y + nextStateCenterRect.height * 0.5f + 5f;

            _eventStateScrolls[eventState] = DrawContentObjects(eventRect, _eventStateScrolls[eventState], _eventStates[eventState]);

            if (eventState is IVSceneBranchState branchState)
            {
                DrawBranchTab(branchState);
                DrawBranchPath(branchState, _derivatedStateRects[branchState]);
            }
        }

        // Check Next None
        if (!_nextStates.ContainsKey(nextState) || _nextStates[nextState] == null)
        {
            DrawNonePath(nextStateCenterRect);
            return;
        }

        IVSceneState nextNextState = _nextStates[nextState];

        // Check MainBranch Path
        if (_stateRects.ContainsKey(nextNextState))
        {
            // Draw Base Line
            float x = nextStateCenterRect.x + nextStateCenterRect.width * 0.25f;
            float y = nextStateCenterRect.y + nextStateCenterRect.height * 0.5f + VERTICAL_LINE_OFFSET * 0.5f;
            float lengthV = BUTTON_HEIGHT;
            float lengthH = BRANCH_RIGHT_OFFSET * 0.5f;

            DrawVerticalLine(x, y, lengthV);
            DrawHorizontalLine(x, y + lengthV, lengthH);

            // Draw Next Arrow
            Rect mainStateCenterRect = GetStateCenterRect(_stateRects[nextNextState]);
            float nextVX = x + lengthH;
            float nextVY = Mathf.Min(nextStateCenterRect.y, mainStateCenterRect.y);
            float nextHX = mainStateCenterRect.x + mainStateCenterRect.width * 0.5f + VERTICAL_LINE_OFFSET * 0.5f + ARROW_WIDTH;

            DrawVerticalLine(nextVX, nextVY, 
                Mathf.Abs(nextStateCenterRect.y - mainStateCenterRect.y) + lengthV + nextStateCenterRect.height * 0.5f + VERTICAL_LINE_OFFSET * 0.5f);
            DrawHorizontalLine(nextHX, mainStateCenterRect.y, nextVX - nextHX);
            DrawArrow(nextHX - ARROW_WIDTH, mainStateCenterRect.y - ARROW_HEIGHT * 0.5f, DirectionEnum.Left);

            return;
        }

        DrawDerivatedState(nextStateCenterRect, nextState, nextNextState);
    }

    private void DrawVirtualState(Rect prevCenterRect, IVSceneState virtualState)
    {
        // Draw Vertical Line
        float length = NEXT_HEIGHT - ARROW_HEIGHT;
        float y = prevCenterRect.y + prevCenterRect.height * 0.5f;
        DrawVerticalLine(prevCenterRect.x, y, length);
        
        // Set Rects
        Rect buttonRect = prevCenterRect;
        buttonRect.width = Mathf.Max(virtualState.name.Length * BUTTON_WIDTH, MIN_BUTTON_WIDTH);
        buttonRect.width = Mathf.Min(buttonRect.width, MAX_BUTTON_WIDTH);
        buttonRect.height = BUTTON_HEIGHT;
        buttonRect.x -= buttonRect.width * 0.5f;
        buttonRect.y += prevCenterRect.height * 0.5f + length + ARROW_HEIGHT;

        DrawButtonBorder(buttonRect, _selectedVirtualState == virtualState ? SELECTED_STATE_COLOR : Color.white);

        // Draw Arrow
        DrawArrow(prevCenterRect.x - ARROW_WIDTH * 0.5f, buttonRect.y - ARROW_HEIGHT - VERTICAL_LINE_OFFSET, DirectionEnum.Down);

        // Draw Virtual Button
        if (GUI.Button(buttonRect, virtualState.name))
        {
            Selection.activeObject = virtualState;
            _selectedVirtualState = virtualState;
            _virtualStateSelectTime = Time.realtimeSinceStartup;
        }
    }

    private void DrawButtonBorder(Rect buttonRect, Color color)
    {
        Rect borderRect = buttonRect;
        borderRect.width += VIRTUAL_BUTTON_BORDER_SIZE;
        borderRect.x -= (borderRect.width - buttonRect.width) * 0.5f - 0.5f;
        borderRect.height += VIRTUAL_BUTTON_BORDER_SIZE;
        borderRect.y -= (borderRect.height - buttonRect.height) * 0.5f + 0.5f;

        EditorGUI.DrawRect(borderRect, color);
    }

    private void DrawNextArrow(Rect prevCenterRect, Rect nextCenterRect)
    {
        DrawVerticalLine(prevCenterRect.x, prevCenterRect.y + prevCenterRect.height * 0.5f + VERTICAL_LINE_OFFSET,
            nextCenterRect.y - prevCenterRect.y - nextCenterRect.height - ARROW_HEIGHT - VERTICAL_LINE_OFFSET * 0.5f);

        DrawArrow(prevCenterRect.x - ARROW_WIDTH * 0.5f, nextCenterRect.y - nextCenterRect.height * 0.5f - ARROW_HEIGHT - VERTICAL_LINE_OFFSET * 0.5f, DirectionEnum.Down);
    }

    private void DrawTurnBackArrow(Rect current, Rect next, float offset = TURN_BACK_OFFSET)
    {
        float x = current.x + current.width * 0.5f - offset;
        Rect currentCenterRect = GetStateCenterRect(current);
        Rect nextCenterRect = GetStateCenterRect(next);

        // Vertical Line
        DrawVerticalLine(x, nextCenterRect.y, Mathf.Abs(currentCenterRect.y - nextCenterRect.y));
        
        // Current Horizontal Line
        DrawHorizontalLine(x, currentCenterRect.y, current.x - x - HORIZONTAL_LINE_OFFSET);

        // Next Horizontal Arrow
        DrawHorizontalLine(x, nextCenterRect.y, next.x - x - BUTTON_WIDTH - HORIZONTAL_LINE_OFFSET);
        DrawArrow(next.x - BUTTON_WIDTH - HORIZONTAL_LINE_OFFSET, nextCenterRect.y - next.height * 0.5f, DirectionEnum.Right);
    }

    private void DrawSelectButton(Rect rect, UnityEngine.Object obj)
    {
        if (obj == null)
            return;

        if (obj is IVSceneState state)
        {
            if (state == _selectedVirtualState)
                DrawButtonBorder(rect, SELECTED_STATE_COLOR);
        }

        if (GUI.Button(rect, obj.name))
        {
            Selection.activeObject = obj;
        }
    }

    private void DrawNonePath(Rect prevCenterRect)
    {
        float y = prevCenterRect.y + prevCenterRect.height * 0.5f + VERTICAL_LINE_OFFSET * 0.5f;
        float length = NEXT_HEIGHT * 0.5f;

        DrawVerticalLine(prevCenterRect.x, y, length);
        DrawArrow(prevCenterRect.x - ARROW_WIDTH * 0.5f, y + length - VERTICAL_LINE_OFFSET * 0.5f, DirectionEnum.Down);
        DrawNone(prevCenterRect.x - NONE_BUTTON_WIDTH * 0.5f, y + length + ARROW_HEIGHT);
    }

    private void DrawNone(float x, float y)
    {
        DrawNone(new Vector2(x, y));
    }

    private void DrawNone(Vector2 position)
    {
        Rect rect = new Rect(position.x, position.y, NONE_BUTTON_WIDTH, BUTTON_HEIGHT);

        EditorGUI.DrawRect(rect, new Color(0.6f, 0f, 0f));
        EditorGUI.LabelField(rect, NONE_TEXT, _labelStyle);
    }

    private void DrawHorizontalLine(float x, float y, float length, float thickness = 1f, float padding = 0f)
    {
        DrawHorizontalLine(new Vector2(x, y), length, thickness, padding);
    }

    private void DrawHorizontalLine(Vector2 position, float length, float thickness = 1f, float padding = 0f)
    {
        Rect lineRect = new Rect();
        lineRect.x = position.x + padding * 0.5f;
        lineRect.y = position.y + thickness * 0.5f;
        lineRect.width = length - padding * 0.5f;
        lineRect.height = thickness;

        EditorGUI.DrawRect(lineRect, new Color(0.9f, 0.9f, 0.9f));
    }

    private void DrawVerticalLine(float x, float y, float length, float thickness = 1f, float padding = 0f)
    {
        DrawVerticalLine(new Vector2(x, y), length, thickness, padding);
    }

    private void DrawVerticalLine(Vector2 position, float length, float thickness = 1f, float padding = 0f)
    {
        Rect lineRect = new Rect();
        lineRect.x = position.x - thickness * 0.5f;
        lineRect.y = position.y + padding * 0.5f;
        lineRect.width = thickness;
        lineRect.height = length - padding * 0.5f;

        EditorGUI.DrawRect(lineRect, new Color(0.9f, 0.9f, 0.9f));
    }

    private void DrawArrow(float x, float y, DirectionEnum direction)
    {
        DrawArrow(new Vector2(x, y), direction);
    }

    private void DrawArrow(Vector2 position, DirectionEnum direction)
    {
        Rect rect = new Rect(position.x, position.y, ARROW_WIDTH, BUTTON_HEIGHT);

        string arrow;
        switch (direction)
        {
            default:
            case DirectionEnum.Right:
                arrow = "▷"; 
                break;
            case DirectionEnum.Left:
                arrow = "◁";
                break;
            case DirectionEnum.Up:
                arrow = "△";
                break;
            case DirectionEnum.Down:
                arrow = "▽";
                break;
        }

        EditorGUI.LabelField(rect, arrow);
    }

    private Rect GetStateCenterRect(Rect stateRect)
    {
        Rect rect = stateRect;
        rect.x = stateRect.x + stateRect.width * 0.5f;
        rect.y = stateRect.y + stateRect.height * 0.5f;

        return rect;
    }

    private Rect GetBranchTabEdgeRect(Rect branchTabRect)
    {
        Rect rect = branchTabRect;
        rect.width = 0f;
        rect.height = 0f;
        rect.x += branchTabRect.width + BRANCH_LINE_LENGTH + HORIZONTAL_LINE_OFFSET;
        rect.y += BRANCH_TAB_HEIGHT * 0.5f;

        return rect;
    }
}