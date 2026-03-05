using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;

public class IVSceneReferenceFinder : EditorWindow
{
    private const string MENU_NAME = "Find Direct References in Scene";
    private const string ASSETS_MENU = "Assets/SquareGolf/";
    private const string GAMEOBJECT_MENU = "GameObject/SquareGolf/";

    private const int INDEX = int.MaxValue;

    private static UnityEngine.Object _target;

    [MenuItem(GAMEOBJECT_MENU + MENU_NAME, true)]
    [MenuItem(ASSETS_MENU + MENU_NAME, true)]
    private static bool ValidateMenu()
    {
        _target = Selection.activeObject;

        if (_target == null)
            return false;

        return true;
    }

    [MenuItem(ASSETS_MENU + MENU_NAME, false, INDEX)]
    [MenuItem(GAMEOBJECT_MENU + MENU_NAME, false, INDEX)]
    private static void Find()
    {
        if (_target == null)
            return;

        if (AssetDatabase.Contains(_target))
            FindInAssets();
        else
            FindInGameObject();
    }
    
    private static void FindInAssets()
    {
        string path = AssetDatabase.GetAssetPath(_target);
        FindReferences((component, property) =>
        {
            if (property == null)
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(component.gameObject))
                    return false;

                Transform parent = component.transform.parent;
                if (parent != null && PrefabUtility.IsPartOfPrefabInstance(parent.gameObject))
                {
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(parent.gameObject) == path)
                        return false;
                }

                return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component.gameObject) == path;
            }

            UnityEngine.Object obj = property.objectReferenceValue;
            if (obj != null && AssetDatabase.Contains(obj) && property.name != "m_CorrespondingSourceObject")
                return AssetDatabase.GetAssetPath(obj) == path;

            return false;
        });
    }

    private static void FindInGameObject()
    {
        if (_target is not GameObject go)
            return;

        Component[] components = go.GetComponents<Component>();
        int[] ids = new int[components.Length + 1];

        ids[0] = go.GetInstanceID();
        for (int i = 0; i < components.Length; i++)
            ids[i + 1] = components[i].GetInstanceID();

        FindReferences((component, property) =>
        {
            if (property == null)
                return false;

            for (int i = 0; i < ids.Length; i++)
            {
                if (property.objectReferenceInstanceIDValue == ids[i])
                {
                    if (component.gameObject == _target && property.name == "m_GameObject")
                        return false;

                    return true;
                }
            }

            return false;
        });
    }

    private static void FindReferences(Func<Component, SerializedProperty, bool> hasReference)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();

        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
            roots = new GameObject[] { stage.prefabContentsRoot };
        
        List<Component> components = new List<Component>();
        foreach (GameObject root in roots)
            components.AddRange(root.GetComponentsInChildren<Component>(true));
        
        HashSet<GameObject> references = new HashSet<GameObject>();
        foreach (Component component in components)
        {
            if (component == null || references.Contains(component.gameObject))
                continue;

            if (component is Transform)
            {
                if (hasReference.Invoke(component, null))
                    references.Add(component.gameObject);
                continue;
            }

            using (SerializedObject so = new SerializedObject(component))
            {
                SerializedProperty property = so.GetIterator();

                bool next = true;
                while (property.Next(next))
                {
                    next = true;

                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    if (hasReference.Invoke(component, property))
                    {
                        references.Add(component.gameObject);
                        break;
                    }
                }
            }            
        }

        ReferenceViewer.OpenWindow(_target, references.ToList<UnityEngine.Object>());
    }
}