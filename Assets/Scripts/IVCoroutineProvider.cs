using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class IVCoroutineProvider : MonoBehaviour
{
    private Dictionary<string, Func<IEnumerator>> _coroutines = null;

    private void MakeCoroutines()
    {
        _coroutines = new Dictionary<string, Func<IEnumerator>>();

        MethodInfo[] methods = GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Where(m => typeof(IEnumerator).IsAssignableFrom(m.ReturnType))
        .ToArray();

        foreach (var method in methods)
            _coroutines[method.Name] = (Func<IEnumerator>)Delegate.CreateDelegate(typeof(Func<IEnumerator>), this, method);
    }

    public Func<IEnumerator> GetCoroutine(string name)
    {
        if (_coroutines == null)
            MakeCoroutines();

        if (_coroutines.TryGetValue(name, out var func))
            return func;

        return null;
    }
}

[Serializable]
public class IVCoroutineModule
{ 
    [SerializeField] private IVCoroutineProvider _provider;
    [SerializeField] private string _coroutineName;

    public IEnumerator GetCoroutine()
    {
        if (_provider == null || _provider.GetCoroutine(_coroutineName) == null)
            return null;

        return _provider.GetCoroutine(_coroutineName).Invoke();
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(IVCoroutineModule))]
public class IVCoroutineModuleEditor : PropertyDrawer
{
    private IVCoroutineProvider _provider = null;
    private string[] _coroutineNames;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty providerProperty = property.FindPropertyRelative("_provider");
        EditorGUILayout.PropertyField(providerProperty, label);        

        IVCoroutineProvider provider = providerProperty.objectReferenceValue as IVCoroutineProvider;
        if (provider == null)
            DrawDefaultTextField(position);
        else
        {
            if (_provider != provider)
            {
                _provider = provider;

                MethodInfo method = typeof(IVCoroutineProvider).GetMethod("MakeCoroutines", BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(_provider, null);
                FieldInfo field = typeof(IVCoroutineProvider).GetField("_coroutines", BindingFlags.Instance | BindingFlags.NonPublic);
                Dictionary<string, Func<IEnumerator>> routines = field.GetValue(_provider) as Dictionary<string, Func<IEnumerator>>;

                _coroutineNames = routines.Keys.ToArray();
            }

            if (_coroutineNames != null && _coroutineNames.Length > 0)
            {
                GUILayout.Space(1f);
                GUILayout.BeginHorizontal();
                GUILayout.Space(position.width * 1f);

                SerializedProperty coroutineName = property.FindPropertyRelative("_coroutineName");

                int currentIndex = Array.IndexOf(_coroutineNames, coroutineName.stringValue);
                if (currentIndex < 0 || currentIndex >= _coroutineNames.Length)
                {
                    if (GUILayout.Button("None"))
                    {
                        currentIndex = 0;
                        DrawPopup();
                    }
                }
                else
                    DrawPopup();

                void DrawPopup()
                {
                    int selectedIndex = EditorGUILayout.Popup(currentIndex, _coroutineNames);
                    coroutineName.stringValue = _coroutineNames[selectedIndex];
                }

                GUILayout.EndHorizontal();
            }
            else
                DrawDefaultTextField(position);
        }
    }

    private void DrawDefaultTextField(Rect position)
    {
        GUILayout.Space(1f);
        GUILayout.BeginHorizontal();
        GUILayout.Space(position.width);
        _provider = null;
        EditorGUILayout.TextField("");
        GUILayout.EndHorizontal();
    }
}
#endif