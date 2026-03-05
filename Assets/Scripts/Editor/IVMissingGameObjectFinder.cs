using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using System.Linq;
using UnityEngine.Events;
using System;

public class IVMissingGameObjectFinder : EditorWindow
{
    private readonly HashSet<GameObject> _missingScripts = new HashSet<GameObject>();
    private readonly HashSet<GameObject> _missingReferences = new HashSet<GameObject>();
    private readonly HashSet<GameObject> _missingEvents = new HashSet<GameObject>();

    private Vector2 _scrollPosition;
    private bool[] _foldouts = new bool[3];

    private BindingFlags BindingFlag => BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("SquareGolf/Tools/MissingGameObjectFinder")]
    public static void ShowWindow()
    {
        GetWindow<IVMissingGameObjectFinder>("Missing GameObject Finder").Show();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(position.width), GUILayout.Height(position.height));

        GUILayout.Space(5f);
        GUILayout.BeginHorizontal();
        GUILayout.Space(position.width * 0.5f);
        if (GUILayout.Button("Refresh"))
            Refresh();
        GUILayout.Space(10f);
        GUILayout.EndHorizontal();

        GUILayout.Space(20f);
        DrawObjects("Missing Scripts", _missingScripts, ref _foldouts[0]);
        GUILayout.Space(20f);
        DrawObjects("Missing References", _missingReferences, ref _foldouts[1]);
        GUILayout.Space(20f);
        DrawObjects("Missing Events", _missingEvents, ref _foldouts[2]);

        EditorGUILayout.EndScrollView();
    }

    private void DrawObjects(string label, HashSet<GameObject> objects, ref bool foldout)
    {
        if (objects.Count == 0)
        {
            SpaceHorizontalLayout(20f, () =>
            {
                EditorGUILayout.LabelField(label);
            });

            SpaceHorizontalLayout(50f, () =>
            {
                EditorGUILayout.LabelField("None");
            });

            return;
        }

        bool fold = foldout;
        SpaceHorizontalLayout(20f, () =>
        {
            fold = EditorGUILayout.Foldout(fold, $"{label} ({objects.Count})");
            GUILayout.Space(15f);
            if (GUILayout.Button("Select All"))
                Selection.objects = objects.ToArray();
        });
        foldout = fold;

        if (!fold)
            return;

        foreach (GameObject go in objects)
        {
            if (go == null)
                continue;

            SpaceHorizontalLayout(40f, () =>
            {
                if (GUILayout.Button(go.name))
                {
                    Selection.activeObject = go;
                    EditorGUIUtility.PingObject(go);
                }
            });
            GUILayout.Space(5f);
        }

        void SpaceHorizontalLayout(float space, Action draw)
        {
            if (draw == null)
                return;

            GUILayout.BeginHorizontal();
            GUILayout.Space(space);
            draw.Invoke();
            GUILayout.Space(space);
            GUILayout.EndHorizontal();
        }
    }

    private void Refresh()
    {
        FindMissingScripts();
        FindMissingReferences();
        FindMissingEvents();
    }

    private void FindMissingScripts()
    {
        _missingScripts.Clear();

        foreach (GameObject go in FindCurrentGameObjects())
        {
            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    _missingScripts.Add(go);
                    break;
                }
            }
        }
    }

    private void FindMissingReferences()
    { 
        _missingReferences.Clear();

        foreach (GameObject go in FindCurrentGameObjects())
        {
            Component[] components = go.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null) 
                    continue;

                SerializedObject so = new SerializedObject(component);
                var prop = so.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
                    {
                        _missingReferences.Add(go);
                        break;
                    }
                }
            }
        }
    }

    private void FindMissingEvents()
    { 
        _missingEvents.Clear();

        foreach (GameObject go in FindCurrentGameObjects())
        {
            Component[] components = go.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null)
                    continue;

                 IEnumerable<FieldInfo> fields = component.GetType()
                        .GetFields(BindingFlag)
                        .Where(f => typeof(UnityEventBase).IsAssignableFrom(f.FieldType));

                foreach (FieldInfo field in fields)
                {
                    UnityEventBase evt = field.GetValue(component) as UnityEventBase;
                    if (evt == null) 
                        continue;

                    for (int i = 0; i < evt.GetPersistentEventCount(); i++)
                    {
                        UnityEngine.Object target = evt.GetPersistentTarget(i);
                        string methodName = evt.GetPersistentMethodName(i);

                        if (target == null || string.IsNullOrEmpty(methodName))
                            continue;

                        (PersistentListenerMode mode, Type argType, string assemblyTypeName) =
                                ReadPersistentCallMeta(component, field, i);

                        Type[] expectedParams = BuildExpectedParameterTypes(evt, mode, argType);
                        MethodInfo mi = FindValidMethod(target, methodName, expectedParams);

                        if (mi == null)
                        {
                            _missingEvents.Add(go);
                            break;
                        }
                    }
                }
            }
        }
    }


    private (PersistentListenerMode mode, Type argType, string assemblyTypeName)
        ReadPersistentCallMeta(Component component, FieldInfo eventField, int callIndex)
    {
        var so = new SerializedObject(component);
        var sp = so.GetIterator();
        PersistentListenerMode mode = PersistentListenerMode.EventDefined;
        Type argType = null;
        string asmTypeName = null;

        // 해당 필드 SerializedProperty 찾기
        if (sp.NextVisible(true))
        {
            do
            {
                if (sp.propertyType == SerializedPropertyType.Generic && sp.name == eventField.Name)
                {
                    var calls = sp.FindPropertyRelative("m_PersistentCalls.m_Calls");
                    if (calls != null && calls.isArray && callIndex < calls.arraySize)
                    {
                        var call = calls.GetArrayElementAtIndex(callIndex);
                        var modeProp = call.FindPropertyRelative("m_Mode"); // enum int
                        if (modeProp != null) mode = (PersistentListenerMode)modeProp.enumValueIndex;

                        var args = call.FindPropertyRelative("m_Arguments");
                        if (args != null)
                        {
                            var asm = args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName");
                            if (asm != null) asmTypeName = asm.stringValue;

                            // 가능한 경우 어셈블리 한정 타입명을 실제 Type으로
                            if (!string.IsNullOrEmpty(asmTypeName))
                            {
                                argType = Type.GetType(asmTypeName);
                                // 로드 실패 시 몇 가지 널 처리(어셈블리명 바뀐 경우 등)
                                if (argType == null)
                                {
                                    // 흔한 케이스: UnityEngine 오브젝트들은 어셈블리 변경 적음
                                    // 그래도 실패시 null 그대로 두고 이후 fallback 처리
                                }
                            }
                        }
                    }
                    break;
                }
            } while (sp.NextVisible(true));
        }

        return (mode, argType, asmTypeName ?? string.Empty);
    }

    private Type[] BuildExpectedParameterTypes(UnityEventBase evt, PersistentListenerMode mode, Type objectArgType)
    {
        // UnityEvent<T1, T2, ...> 의 제네릭 인자
        var evtType = evt.GetType();
        var genericArgs = evtType.IsGenericType ? evtType.GetGenericArguments() : Type.EmptyTypes;

        switch (mode)
        {
            case PersistentListenerMode.EventDefined:
                // 인스펙터에서 Dynamic으로 표시되는 경우: 이벤트 제네릭 인자와 동일
                return genericArgs;

            case PersistentListenerMode.Void:
                return Type.EmptyTypes;

            case PersistentListenerMode.Object:
                // Object 인자는 ArgumentCache의 어셈블리 한정 타입을 우선,
                // 실패 시 UnityEngine.Object로 완화
                return new[] { objectArgType ?? typeof(UnityEngine.Object) };

            case PersistentListenerMode.Int:
                return new[] { typeof(int) };
            case PersistentListenerMode.Float:
                return new[] { typeof(float) };
            case PersistentListenerMode.String:
                return new[] { typeof(string) };
            case PersistentListenerMode.Bool:
                return new[] { typeof(bool) };

#if UNITY_2021_3_OR_NEWER
            // 필요 시 다른 모드 확장
#endif
            default:
                // 모드를 해석할 수 없으면 이벤트 정의(제네릭)로 가정
                return genericArgs;
        }
    }

    private MethodInfo FindValidMethod(object target, string methodName, Type[] expectedParams)
    {
        if (target == null || string.IsNullOrEmpty(methodName)) return null;
        var targetType = target.GetType();
        expectedParams ??= Type.EmptyTypes;

        // 1) UnityEventBase.GetValidMethodInfo가 있으면 먼저 사용 (버전별 시그니처 대응)
        var ueb = typeof(UnityEngine.Events.UnityEventBase);
        // 시그니처 1: (Type, string, Type[])
        var miTypeSig = ueb.GetMethod("GetValidMethodInfo",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(Type), typeof(string), typeof(Type[]) },
            modifiers: null);

        if (miTypeSig != null)
        {
            var found = miTypeSig.Invoke(null, new object[] { targetType, methodName, expectedParams }) as MethodInfo;
            if (found != null) return found;
        }

        // 시그니처 2(혹시 있는 경우): (object, string, Type[])
        var miObjSig = ueb.GetMethod("GetValidMethodInfo",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(object), typeof(string), typeof(Type[]) },
            modifiers: null);

        if (miObjSig != null)
        {
            var found = miObjSig.Invoke(null, new object[] { target, methodName, expectedParams }) as MethodInfo;
            if (found != null) return found;
        }

        // 2) 수동 탐색: 이름과 파라미터 타입 호환성으로 매칭
        // Unity의 규칙에 가깝게: instance, public/non-public, 상속/인터페이스 호환 허용
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var m in targetType.GetMethods(flags))
        {
            if (m.Name != methodName) continue;
            if (m.IsGenericMethod) continue;

            var ps = m.GetParameters();
            if (ps.Length != expectedParams.Length) continue;

            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
            {
                var want = expectedParams[i] ?? typeof(UnityEngine.Object); // Object 모드 널 방어
                var have = ps[i].ParameterType;

                // UnityEvent의 매칭 규칙: have(메서드 파라미터)가 want(이벤트/인스펙터가 기대하는 타입)를 받아줄 수 있어야 함
                // ex) want == Derived, have == Base 면 ok (Base가 Derived를 받을 수 없음) → 반대로 체크!
                // 실제론 "want를 have로 넣을 수 있어야" → have.IsAssignableFrom(want)
                if (!have.IsAssignableFrom(want))
                {
                    ok = false;
                    break;
                }
            }
            if (ok) return m;
        }

        return null;
    }

    private IList<GameObject> FindCurrentGameObjects()
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage == null)
            return FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        List<GameObject> objects = new List<GameObject>();
        foreach (Transform child in prefabStage.prefabContentsRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child != null)
                objects.Add(child.gameObject);
        }

        return objects;
    }
}