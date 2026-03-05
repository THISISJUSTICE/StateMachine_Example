using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

public class ReferenceViewer : EditorWindow
{
    private Vector2 _scrollPosition;
    private GUIStyle _labelStyle;

    private Object _target;
    private IList<Object> _objects;

    public static void OpenWindow(Object target, IList<string> paths)
    { 
        List<Object> objects = new List<Object>();
        foreach (string path in paths)
        { 
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset != null)
                objects.Add(asset);
        }

        OpenWindow(target, objects);
    }

    public static void OpenWindow(Object target, IList<Object> objects)
    {
        ReferenceViewer window = GetWindow<ReferenceViewer>(true, $"Reference Viewer", true);
        window._target = target;
        window._objects = objects;

        window._labelStyle = new GUIStyle(EditorStyles.boldLabel);
        window._labelStyle.alignment = TextAnchor.MiddleCenter;

        window.Show();
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(position.width), GUILayout.Height(position.height));

        SelectFileButtons();

        EditorGUILayout.EndScrollView();
    }

    private void SelectFileButtons()
    {
        GUILayout.Space(10f);

        if (_target != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);

            if (GUILayout.Button(_target.name))
            {
                Selection.activeObject = _target;
                EditorGUIUtility.PingObject(_target);
            }

            GUILayout.Space(20f);
            GUILayout.EndHorizontal();
            GUILayout.Space(20f);
        }

        if (_objects == null || _objects.Count == 0)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            EditorGUILayout.LabelField("No references");
            GUILayout.EndHorizontal();
            return;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Space(20f + position.width * 0.1f);

        EditorGUILayout.LabelField($"References ({_objects.Count})", _labelStyle);
        GUILayout.Space(15f);
        if (GUILayout.Button("Select All"))
            Selection.objects = _objects.ToArray();

        GUILayout.Space(20f);
        GUILayout.EndHorizontal();
        GUILayout.Space(5f);

        foreach (Object obj in _objects)
        {
            if (obj == null)
                continue;

            GUILayout.BeginHorizontal();
            GUILayout.Space(20f + position.width * 0.1f);

            if (GUILayout.Button(obj.name))
            {
                if (obj != null)
                {
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                }
            }

            GUILayout.Space(20f + position.width * 0.1f);
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);
        }
    }
}