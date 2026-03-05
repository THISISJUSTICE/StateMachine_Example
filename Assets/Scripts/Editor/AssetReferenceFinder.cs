using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Threading;
using Object = UnityEngine.Object;

public class AssetReferenceFinder : EditorWindow
{
    private class TaskValueList<T>
    {
        private readonly object _lock = new object();

        private List<T> _list;
        private Action<List<T>> _callback;
        private DateTime _startTime;

        private int _flag;
        private int _endFlag;

        public TaskValueList(int endflag, Action<List<T>> callback)
        {
            _startTime = DateTime.Now;
            _list = new List<T>();
            _flag = 0;
            _endFlag = endflag;
            _callback = callback;
        }

        public void AddList(List<T> list)
        {
            if (IsTaskDone())
                return;

            lock (_lock)
            {
                foreach (T item in list)
                {
                    _list.Add(item);
                }
                _flag++;

                if (IsTaskDone())
                {
                    Debug.Log($"Searching Time: {(DateTime.Now - _startTime).TotalSeconds}");
                    if (_callback != null)
                        _callback(_list);
                }
            }
        }

        public bool IsTaskDone()
        {
            return _flag >= _endFlag;
        }
    }

    private const string MAIN_PATH = "Assets";
    private const float FOLD_OFFSET_X = 20f;
    private const string MENU_NAME = "Asset References Finder";

    private Vector2 _scrollPosition;
    private Vector2 _findOptScroll;

    private bool _foldout = false;

    private Object _targetAsset;

    private Dictionary<string, List<string>> _extensionPaths;
    private Dictionary<string, bool> _extensionCheck;
    private List<string> _extensions;

    private string _folderPath = MAIN_PATH;

    private bool _saveTxt = false;
    private string _txtPath;

    private int _timeLimit = 60;

    private TaskValueList<string> _tvl;
    private int ProcessCount { get => Environment.ProcessorCount; }

    [MenuItem("Assets/SquareGolf/Find References in Project")]
    public static void FindInWindow()
    {
        AssetReferenceFinder window = GetWindow<AssetReferenceFinder>(MENU_NAME);

        UnityEngine.Object obj = Selection.activeObject;
        if (obj != null && AssetDatabase.Contains(obj))
            window._targetAsset = obj;

        window.Show();
    }

    [MenuItem("SquareGolf/Tools/" + MENU_NAME)]
    public static void ShowWindow()
    {
        GetWindow<AssetReferenceFinder>(MENU_NAME).Show();
    }

    private void OnEnable()
    {
        string mainPath = MAIN_PATH;
        if (_folderPath != null && _folderPath != string.Empty && _folderPath.Length > 0 && _folderPath.StartsWith(mainPath))
            mainPath = _folderPath;
        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths()
                                            .Where(path => path.StartsWith(mainPath))
                                            .ToArray();

        _extensionPaths = new Dictionary<string, List<string>>();
        _extensionCheck = new Dictionary<string, bool>();

        foreach (string path in allAssetPaths)
        {
            if (Directory.Exists(path))
                continue;

            string extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension) || extension.Length < 1)
                extension = "None";

            if (!_extensionPaths.ContainsKey(extension))
            {
                _extensionPaths[extension] = new List<string>();
            }
            _extensionPaths[extension].Add(path);

            if (!_extensionCheck.ContainsKey(extension))
                _extensionCheck[extension] = false;
        }

        string[] initCheck = new string[] { ".prefab", ".unity" };
        for (int i = 0; i < initCheck.Length; i++)
        {
            if (_extensionCheck.ContainsKey(initCheck[i]))
                _extensionCheck[initCheck[i]] = true;
        }

        _extensions = new List<string>();
        foreach (var key in _extensionPaths.Keys)
        {
            _extensions.Add(key);
        }
        _extensions.Sort();
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(position.width), GUILayout.Height(position.height));

        _targetAsset = EditorGUILayout.ObjectField("Target Asset", _targetAsset, typeof(Object), false);
        GUILayout.Space(2f);

        DrawSetFolderPath();

        GUILayout.Space(5f);
        _foldout = EditorGUILayout.Foldout(_foldout, "Find Options");
        if (_foldout)
        {
            CheckPathOptionField();
        }
        GUILayout.Space(10f);

        FindReferncesButton();

        EditorGUILayout.EndScrollView();
    }

    #region GUI Functions
    private void DrawSetFolderPath()
    {
        GUILayout.BeginHorizontal();
        bool set = false;
        if (GUILayout.Button("Folder Path"))
            set = true;
        EditorGUILayout.LabelField($"({_folderPath}/)");
        GUILayout.EndHorizontal();

        if (!set)
            return;

        SetFolderPath();
    }

    private void SetFolderPath()
    {
        string folderPath = EditorUtility.OpenFolderPanel("", MAIN_PATH, "");

        if (folderPath == null || folderPath.Length <= 0)
            return;

        string projectPath = Directory.GetCurrentDirectory().Replace("\\", "/");
        folderPath = folderPath.Replace("\\", "/");

        if (folderPath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
        {
            string relativePath = folderPath.Substring(projectPath.Length + 1);
            if (relativePath.StartsWith(MAIN_PATH))
                folderPath = relativePath;
        }

        if (!folderPath.StartsWith(MAIN_PATH))
            return;

        if (folderPath != _folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                _folderPath = folderPath;
                OnEnable();
            }
        }
    }

    private void CheckPathOptionField()
    {
        int fileCount = 0;
        foreach (var key in _extensions)
        {
            if (!_extensionCheck[key])
                continue;

            fileCount += _extensionPaths[key].Count;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Space(FOLD_OFFSET_X);
        _timeLimit = EditorGUILayout.IntField("Find Time Limit (seconds)", _timeLimit);
        GUILayout.EndHorizontal();
        GUILayout.Space(5f);

        GUIFoldTitle($"Find Files: {fileCount}");

        GUIFoldContentScroll(ref _findOptScroll, (_extensions.Count + 1) * 20f + 5f, (scrollRect) =>
        {
            float btnSize = 16f;
            Rect rect = scrollRect;
            rect.width = btnSize; rect.height = btnSize;
            rect.x += scrollRect.width - 63f;
            rect.y -= scrollRect.height / 2f + 10f;

            if (_extensions.Count <= 0)
                return;
            bool check = !_extensionCheck[_extensions[0]];
            string btnText = check ? "v" : "";
            if (GUI.Button(rect, btnText))
            {
                foreach (var key in _extensions)
                {
                    _extensionCheck[key] = check;
                }
            }

            rect = scrollRect;
            rect.width = 46f; rect.height = btnSize;
            rect.x -= FOLD_OFFSET_X;
            rect.y -= scrollRect.height / 2f + 10f;
            EditorGUI.LabelField(rect, "Formats");

            GUILayout.Space(22f);
            foreach (var key in _extensions)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{key}");
                _extensionCheck[key] = EditorGUILayout.Toggle(_extensionCheck[key]);
                GUILayout.EndHorizontal();
            }
        });

        GUILayout.BeginHorizontal();
        GUILayout.Space(FOLD_OFFSET_X);
        EditorGUILayout.LabelField("Save text");
        _saveTxt = EditorGUILayout.Toggle(_saveTxt);
        GUILayout.EndHorizontal();
        if (_saveTxt)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(FOLD_OFFSET_X);
            EditorGUILayout.LabelField("File Path");
            _txtPath = EditorGUILayout.TextField(_txtPath);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(20f);
    }

    private void FindReferncesButton()
    {
        if (!GUILayout.Button("Find References"))
            return;

        if (_targetAsset == null)
        {
            Debug.LogError("Target Asset is Null");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(_targetAsset);
        string guid = AssetDatabase.AssetPathToGUID(assetPath);

        List<string> referencingFiles = FindFilesReferencingGUID(guid);
        if (referencingFiles == null || referencingFiles.Count == 0)
        {
            Debug.Log($"No references of {_targetAsset.name} found.");
            return;
        }

        string text = $"{Path.GetFileName(assetPath)} References: \n";
        referencingFiles.Sort();

        foreach (string file in referencingFiles)
        {
            text += $"{file}\n";
        }
        text = text.TrimEnd('\n');
        Debug.Log(text);

        if (_saveTxt)
        {
            if (Directory.Exists(_txtPath))
                File.WriteAllText(Path.Combine(_txtPath, "ReferencingFiles.txt"), text);
            else
            {
                Debug.Log($"Not Exist Path: {_txtPath}");
            }
        }

        ReferenceViewer.OpenWindow(_targetAsset, referencingFiles);
    }

    private void GUIFoldTitle(string title)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(FOLD_OFFSET_X);
        EditorGUILayout.LabelField(title);
        GUILayout.EndHorizontal();
        GUILayout.Space(5f);
    }

    private void GUIFoldContentScroll(ref Vector2 scrollPos, float contentSize, Action<Rect> content)
    {
        Vector2 scrollSize = new Vector2(250f, 200f);
        Rect scrollRect = GUILayoutUtility.GetLastRect();
        scrollRect.x += FOLD_OFFSET_X;
        scrollRect.y += 5f;
        scrollRect.width = scrollSize.x;
        scrollRect.height = scrollSize.y + 5f;
        GUI.Box(scrollRect, GUIContent.none);

        Rect contentRect = new Rect(0, scrollSize.y / 3f - 20f, scrollSize.x - FOLD_OFFSET_X, contentSize);

        scrollPos = GUI.BeginScrollView(scrollRect, scrollPos, contentRect);
        GUILayout.BeginArea(contentRect);

        content(scrollRect);

        GUILayout.EndArea();
        GUI.EndScrollView();
        GUILayout.Space(scrollSize.y + 10f);
    }
    #endregion

    #region Utils
    private List<string> FindFilesReferencingGUID(string guid)
    {
        List<string> checkPaths = new List<string>();

        foreach (var key in _extensions)
        {
            if (!_extensionCheck[key])
                continue;

            foreach (var value in _extensionPaths[key])
                checkPaths.Add(value);
        }

        checkPaths = SortListByFileSize(checkPaths);

        if (checkPaths.Count > ProcessCount)
        {
            List<string>[] findedFiles = new List<string>[ProcessCount];
            List<string> referencingFiles = new List<string>();

            _tvl = new TaskValueList<string>(findedFiles.Length, (list) => referencingFiles.AddRange(list));

            for (int i = 0; i < findedFiles.Length; i++)
                findedFiles[i] = new List<string>();
            for (int i = 0; i < checkPaths.Count; i++)
            {
                findedFiles[i % findedFiles.Length].Add(checkPaths[i]);
            }

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                cts.CancelAfter(_timeLimit * 1000);

                try
                {
                    for (int i = 1; i < findedFiles.Length; i++)
                    {
                        int index = i;
                        Task.Run(() =>
                        {
                            List<string> files = FindFiles(findedFiles[index], guid, cts.Token);
                            _tvl.AddList(files);
                        });
                    }

                    List<string> files = FindFiles(findedFiles[0], guid, cts.Token);
                    _tvl.AddList(files);
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("Find Timeout");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Find failed: {ex.Message}");
                }
            }

            return referencingFiles;
        }
        else
        {
            DateTime startTime = DateTime.Now;
            List<string> referencingFiles = FindFiles(checkPaths, guid);

            Debug.Log($"Searching Time: {(DateTime.Now - startTime).TotalSeconds}");

            return referencingFiles;
        }
    }

    private List<string> FindFiles(List<string> paths, string guid, CancellationToken? token = null)
    {
        List<string> findedFiles = new List<string>();
        CancellationToken ct;
        if (token != null)
            ct = (CancellationToken)token;

        foreach (string path in paths)
        {
            if (token != null && ct.IsCancellationRequested)
                ct.ThrowIfCancellationRequested();

            string fileContent = File.ReadAllText(path);
            if (fileContent.Contains(guid))
                findedFiles.Add(path);
        }

        return findedFiles;
    }

    private List<string> SortListByFileSize(List<string> paths)
    {
        return paths
            .Where(File.Exists)
            .OrderByDescending(path => new FileInfo(path).Length)
            .ToList();
    }
    #endregion
}
