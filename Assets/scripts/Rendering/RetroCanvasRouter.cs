using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts all screen UI on the main camera, at stable distances, so the retro filter covers it and nothing flickers.
///
/// Screen Space - Overlay canvases are drawn after the camera has finished, out of reach of any camera effect,
/// so they're moved onto the main camera as Screen Space - Camera (canvases built in code via SegUIKit are routed
/// as they're created). Every root canvas on that camera (including the HUD, which already was one) is then given
/// its own plane distance just past the near clip plane:
///   - distinct distances, because camera canvases with the same sorting layer/order are ordered by distance,
///     and equal distances leave the order undefined (the UI "z-fighting");
///   - just past the near plane, because world geometry closer than a canvas covers it (the HUD sat 1 unit out,
///     which walls reach at this scene's scale).
/// Former overlays stack above camera canvases of the same order, as they did before.
///
/// If the main camera changes (cutscenes) the canvases follow it; with no usable camera, routed canvases fall back
/// to Overlay so UI never disappears. Set <see cref="Enabled"/> false (before scene load) to leave UI alone.
/// </summary>
public class RetroCanvasRouter : MonoBehaviour
{
    public static bool Enabled = true;

    private const float NearPlaneOffset = 0.01f;
    private const float DistanceStep = 0.002f;

    private class Entry
    {
        public Canvas canvas;
        public bool wasOverlay;
        public int sequence;
    }

    private static RetroCanvasRouter instance;
    private readonly List<Entry> entries = new List<Entry>();
    private int nextSequence;
    private bool needsRestack;

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

    /// <summary>
    /// Takes charge of a root screen canvas: an Overlay one is moved onto the main camera, a Screen Space - Camera
    /// one on the main camera gets a stable distance. World Space and nested canvases are left alone.
    /// </summary>
    public static void Route(Canvas canvas)
    {
        if(!Enabled || canvas == null) return;
        //nested canvases take their render mode from the root
        if(canvas.GetComponentsInParent<Canvas>(true).Length > 1) return;

        bool overlay = canvas.renderMode == RenderMode.ScreenSpaceOverlay;
        bool onMainCamera = canvas.renderMode == RenderMode.ScreenSpaceCamera
            && (canvas.worldCamera == null || canvas.worldCamera == Camera.main);
        if(!overlay && !onMainCamera) return;

        RetroCanvasRouter router = GetInstance();
        if(router.entries.Any(e => e.canvas == canvas)) return;
        router.entries.Add(new Entry { canvas = canvas, wasOverlay = overlay, sequence = router.nextSequence++ });
        router.Restack(Camera.main);
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

    private void Restack(Camera camera)
    {
        entries.RemoveAll(e => e.canvas == null);
        needsRestack = false;
        bool usable = camera != null && camera.isActiveAndEnabled;

        //same order Unity draws camera canvases in: layer, then order; ties broken by us (overlays last = on top)
        List<Entry> ordered = entries
            .OrderBy(e => SortingLayer.GetLayerValueFromID(e.canvas.sortingLayerID))
            .ThenBy(e => e.canvas.sortingOrder)
            .ThenBy(e => e.wasOverlay ? 1 : 0)
            .ThenBy(e => e.sequence)
            .ToList();

        for(int i = 0; i < ordered.Count; i++)
        {
            Canvas canvas = ordered[i].canvas;
            //only write what differs: each write re-dirties the canvas
            if(!usable)
            {
                if(ordered[i].wasOverlay && canvas.renderMode != RenderMode.ScreenSpaceOverlay) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                continue;
            }
            if(canvas.renderMode != RenderMode.ScreenSpaceCamera) canvas.renderMode = RenderMode.ScreenSpaceCamera;
            if(canvas.worldCamera != camera) canvas.worldCamera = camera;
            //drawn last = closest
            float distance = camera.nearClipPlane + NearPlaneOffset + (ordered.Count - 1 - i) * DistanceStep;
            if(!Mathf.Approximately(canvas.planeDistance, distance)) canvas.planeDistance = distance;
        }
    }

    //cheap: a handful of canvases; only restacks when a camera or sort order actually changed
    void LateUpdate()
    {
        Camera main = Camera.main;
        bool usable = main != null && main.isActiveAndEnabled;
        if(usable != lastUsable || main != lastCamera) needsRestack = true;
        foreach(Entry entry in entries)
        {
            Canvas canvas = entry.canvas;
            if(canvas == null) { needsRestack = true; continue; }
            //something else switched a canvas off our camera (or a script changed its mode)
            if(usable && (canvas.renderMode != RenderMode.ScreenSpaceCamera || canvas.worldCamera != main)) needsRestack = true;
        }
        if(needsRestack) Restack(main);
        lastCamera = main;
        lastUsable = usable;
    }

    private Camera lastCamera;
    private bool lastUsable;

    void OnDestroy()
    {
        if(instance == this) instance = null;
    }
}
