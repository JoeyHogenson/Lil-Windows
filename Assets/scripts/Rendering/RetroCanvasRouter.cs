using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Screen Space - Overlay canvases are drawn after the camera has finished, so no camera effect (including the
/// retro filter) can touch them. This moves every overlay canvas onto the main camera as Screen Space - Camera,
/// just past the near clip plane so world geometry never covers it, which lets RetroStylizeFeature's final
/// pass stylize it like the HUD. Canvases built in code (SegUIKit) are routed as they're created.
///
/// If the main camera changes (cutscenes) the canvases follow it; with no usable camera they fall back to
/// Overlay so UI never disappears. Set <see cref="Enabled"/> false (before scene load) to leave overlays alone.
/// </summary>
public class RetroCanvasRouter : MonoBehaviour
{
    public static bool Enabled = true;

    private const float NearPlaneOffset = 0.01f;

    private static RetroCanvasRouter instance;
    private readonly List<Canvas> routed = new List<Canvas>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        RouteAllInLoadedScenes();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RouteAllInLoadedScenes();
    }

    public static void RouteAllInLoadedScenes()
    {
        if(!Enabled) return;
        foreach(Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Route(canvas);
        }
    }

    /// <summary>Puts an overlay root canvas on the main camera so camera effects reach it. No-op for anything else.</summary>
    public static void Route(Canvas canvas)
    {
        if(!Enabled || canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) return;
        //nested canvases take their render mode from the root
        if(canvas.GetComponentsInParent<Canvas>(true).Length > 1) return;

        RetroCanvasRouter router = GetInstance();
        if(router.routed.Contains(canvas)) return;
        router.routed.Add(canvas);
        router.Attach(canvas, Camera.main);
    }

    private static RetroCanvasRouter GetInstance()
    {
        if(instance == null)
        {
            //HideInHierarchy, not HideAndDontSave: the latter would outlive Play mode in the editor
            var go = new GameObject("Retro Canvas Router") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RetroCanvasRouter>();
        }
        return instance;
    }

    private void Attach(Canvas canvas, Camera camera)
    {
        if(camera == null || !camera.isActiveAndEnabled)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return;
        }
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = camera.nearClipPlane + NearPlaneOffset;
    }

    //cheap: a handful of canvases, and only re-attached when their camera stops being usable
    void LateUpdate()
    {
        Camera main = null;
        bool mainLookedUp = false;
        for(int i = routed.Count - 1; i >= 0; i--)
        {
            Canvas canvas = routed[i];
            if(canvas == null)
            {
                routed.RemoveAt(i);
                continue;
            }
            Camera current = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
            if(current != null && current.isActiveAndEnabled) continue;

            if(!mainLookedUp)
            {
                main = Camera.main;
                mainLookedUp = true;
            }
            Attach(canvas, main);
        }
    }

    void OnDestroy()
    {
        if(instance == this) instance = null;
    }
}
