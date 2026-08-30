using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class IVBackgroundCaptureCamera : MonoBehaviour
{
    [SerializeField] private int _croppedHeight;
    [SerializeField] private float _tolerance = 0.17f;

    private const int TEXTURE_SIZE = 2048;

    private Camera _camera;

    private Vector3[] _captureAngles = new Vector3[] {
        Vector3.zero,
        Vector3.up * 90f,
        Vector3.up * 180f,
        Vector3.up * 270f,
    };

    public bool Capturing { get; private set; } = false;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    public void Capture()
    {
        Capturing = true;
        StartCoroutine(StartCapture());
    }

    private IEnumerator StartCapture()
    {
        const string mainPath = "D:/temp";
        string fileName = SceneManager.GetActiveScene().name;
        _camera.depthTextureMode = DepthTextureMode.None;

        Texture2D[] textures = new Texture2D[_captureAngles.Length];

        for (int i = 0; i < _captureAngles.Length; i++)
        {
            yield return new WaitForSeconds(0.1f);
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            transform.eulerAngles = _captureAngles[i];

            Texture2D colorTex = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGB24, false);
            RenderTexture faceRT = CreateRenderTexture(TEXTURE_SIZE, TEXTURE_SIZE, 24, 1, null, 2);
            _camera.targetTexture = faceRT;

            GL.Clear(true, true, new Color(0, 0, 0, 0), 0);
            yield return new WaitForEndOfFrame();
            RenderTexture.active = faceRT;
            colorTex.ReadPixels(new Rect(0, 0, TEXTURE_SIZE, TEXTURE_SIZE), 0, 0, false);
            textures[i] = colorTex;
            //File.WriteAllBytes($"{mainPath}/{fileName}_{i}.png", colorTex.EncodeToPNG());
        }

        Texture2D bg = new Texture2D(TEXTURE_SIZE * _captureAngles.Length, TEXTURE_SIZE / 2, TextureFormat.RGBA32, false);
        for (int i = 0; i < textures.Length; i++)
        {
            Texture2D src = textures[i];
            Color[] pixels = src.GetPixels(0, _croppedHeight, src.width, bg.height);
            for (int p = 0; p < pixels.Length; p++)
            {
                if (IsSimilarColor(pixels[p], _camera.backgroundColor))
                    pixels[p] = new Color(0f, 0f, 0f, 0f);
            }
            bg.SetPixels(i * src.width, 0, src.width, bg.height, pixels);
        }

        bg.Apply();
        File.WriteAllBytes($"{mainPath}/{fileName}_bg.png", bg.EncodeToPNG());

        Capturing = false;
    }

    private bool IsSimilarColor(Color a, Color b)
    {
        float dist = Vector3.Distance(new Vector3(a.r, a.g, a.b), new Vector3(b.r, b.g, b.b));
        return dist < _tolerance;
    }

    public static RenderTexture CreateRenderTexture(int width, int height, int depth, int antiAliasing, RenderTexture t2Create, int imgcnt, bool create = true)
    {
        if (t2Create &&
          (t2Create.width == width) && (t2Create.height == height) && (t2Create.depth == depth) &&
          (t2Create.antiAliasing == antiAliasing) && (t2Create.IsCreated() == create))
            return t2Create;

        if (t2Create != null)
        {
            UnityEngine.Object.Destroy(t2Create);
        }
        if (imgcnt == 4) t2Create = new RenderTexture(width / 4, height / 4, depth, RenderTextureFormat.R16);
        else if (imgcnt == 2 || imgcnt == 3) t2Create = new RenderTexture(width, height, depth, RenderTextureFormat.ARGB32);
        else t2Create = new RenderTexture(width, height, depth, RenderTextureFormat.ARGB32);

        //t2Create = new RenderTexture(width, height, depth, RenderTextureFormat.Default);
        t2Create.antiAliasing = antiAliasing;
        t2Create.hideFlags = HideFlags.HideAndDontSave;

        // Make sure render texture is created.
        if (create)
            t2Create.Create();

        return t2Create;
    }
}

#if UNITY_EDITOR
[ExecuteAlways]
[CustomEditor(typeof(IVBackgroundCaptureCamera))]
public class IVBackgroundCaptureCameraInspector : Editor
{
    private IVBackgroundCaptureCamera _capture;

    private bool _once = false;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (!EditorApplication.isPlaying)
            return;

        if (_once == false)
        {
            _once = true;

            _capture = (IVBackgroundCaptureCamera)target;
        }

        GUILayout.Space(20f);

        if (_capture.Capturing)
            EditorGUILayout.LabelField("Capturing...");
        else if (GUILayout.Button("Capture"))
            _capture.Capture();
    }
}
#endif