using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts all screen UI on one dedicated UI camera that's stacked on top of the main camera, so the retro filter
/// covers it without jitter, flicker or walls cutting through it.
///
/// Why a separate camera: screen-space canvases on the main camera live a fraction of a unit in front of it, and
/// the player is ~600 units from the world origin, where float precision is coarser than a screen pixel at that
/// size: the UI wobbled ("UI jitter"). The UI camera sits at the origin, renders only the UI layer, clears depth
/// (world geometry can't cover it) and is a URP Overlay camera in the main camera's stack, so RetroStylizeFeature's
/// final pass, which runs on the last camera of the stack, still stylizes it.
///
/// Overlay canvases (drawn after cameras, out of reach of any camera effect) and canvases already on the main
/// camera are routed to it; canvases built in code via SegUIKit are routed as they're created. Each gets its own
/// plane distance so equal sort orders still draw in a fixed order. With no usable main camera, routed canvases
/// fall back to Overlay so UI never disappears. Set <see cref="Enabled"/> false (before scene load) to leave UI alone.
/// </summary>
public class RetroCanvasRouter : MonoBehaviour
{
    public static bool Enabled = true;

    private const float BaseDistance = 1f;
    private const float DistanceStep = 0.01f;

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
    private Camera uiCamera;
    private Camera stackedOn;
    private int uiLayer;

    /// <summary>The overlay camera all screen UI renders through (null until the first canvas is routed).</summary>
    public static Camera UICamera => instance != null ? instance.uiCamera : null;

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
    /// Takes charge of a root screen canvas (Overlay, or Screen Space - Camera on the main camera). World Space and
    /// nested canvases are left alone.
    /// </summary>
    public static void Route(Canvas canvas)
    {
        if(!Enabled || canvas == null) return;
        //nested canvases take their render mode from the root
        if(canvas.GetComponentsInParent<Canvas>(true).Length > 1) return;

        RetroCanvasRouter router = GetInstance();
        bool overlay = canvas.renderMode == RenderMode.ScreenSpaceOverlay;
        bool onMainCamera = canvas.renderMode == RenderMode.ScreenSpaceCamera
            && (canvas.worldCamera == null || canvas.worldCamera == Camera.main || canvas.worldCamera == router.uiCamera);
        if(!overlay && !onMainCamera) return;
        if(router.entries.Any(e => e.canvas == canvas)) return;

        //a camera-space canvas inherits its parent's scale (overlay ones don't), so it comes out too big/small
        //and clicks miss; keep screen canvases at the scene root or under unscaled objects
        Transform parent = canvas.transform.parent;
        if(parent != null && (parent.lossyScale - Vector3.one).sqrMagnitude > 1e-4f)
        {
            Debug.LogWarning("[RetroCanvasRouter] Canvas \"" + canvas.name + "\" is under \"" + parent.name + "\", which is scaled "
                + parent.lossyScale + ". On the camera it will render scaled by that too; move it to the scene root.", canvas);
        }

        //the UI camera only renders the UI layer
        foreach(Transform t in canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = router.uiLayer;

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
            instance.CreateUICamera();
        }
        return instance;
    }

    private void CreateUICamera()
    {
        uiLayer = LayerMask.NameToLayer("UI");
        if(uiLayer < 0) uiLayer = 5;

        var go = new GameObject("Retro UI Camera");
        go.transform.SetParent(transform, false);
        //at the origin: full float precision for the canvases in front of it
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        uiCamera = go.AddComponent<Camera>();
        uiCamera.clearFlags = CameraClearFlags.Depth;
        uiCamera.cullingMask = 1 << uiLayer;
        uiCamera.orthographic = true;
        uiCamera.orthographicSize = 5f;
        uiCamera.nearClipPlane = 0.1f;
        uiCamera.farClipPlane = 10f;
        UniversalAdditionalCameraData data = uiCamera.GetUniversalAdditionalCameraData();
        data.renderType = CameraRenderType.Overlay;
        data.renderPostProcessing = false;
    }

    //puts the UI camera at the top of the main camera's stack (and out of the previous one's)
    private bool StackOn(Camera main)
    {
        if(main == stackedOn && main != null && main.isActiveAndEnabled) return true;
        if(stackedOn != null)
        {
            UniversalAdditionalCameraData old = stackedOn.GetUniversalAdditionalCameraData();
            if(old != null) old.cameraStack.Remove(uiCamera);
        }
        stackedOn = null;
        if(main == null || !main.isActiveAndEnabled) return false;
        UniversalAdditionalCameraData data = main.GetUniversalAdditionalCameraData();
        if(data.renderType != CameraRenderType.Base) return false;
        if(!data.cameraStack.Contains(uiCamera)) data.cameraStack.Add(uiCamera);
        stackedOn = main;
        return true;
    }

    private void Restack(Camera main)
    {
        entries.RemoveAll(e => e.canvas == null);
        needsRestack = false;
        bool usable = StackOn(main);

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
                if(canvas.renderMode != RenderMode.ScreenSpaceOverlay) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                continue;
            }
            if(canvas.renderMode != RenderMode.ScreenSpaceCamera) canvas.renderMode = RenderMode.ScreenSpaceCamera;
            if(canvas.worldCamera != uiCamera) canvas.worldCamera = uiCamera;
            //drawn last = closest
            float distance = BaseDistance + (ordered.Count - 1 - i) * DistanceStep;
            if(!Mathf.Approximately(canvas.planeDistance, distance)) canvas.planeDistance = distance;
        }
    }

    //cheap: a handful of canvases; only restacks when the main camera or a canvas's setup changed
    void LateUpdate()
    {
        Camera main = Camera.main;
        bool usable = main != null && main.isActiveAndEnabled;
        if(main != stackedOn || usable != lastUsable) needsRestack = true;
        foreach(Entry entry in entries)
        {
            Canvas canvas = entry.canvas;
            if(canvas == null) { needsRestack = true; continue; }
            if(usable && (canvas.renderMode != RenderMode.ScreenSpaceCamera || canvas.worldCamera != uiCamera)) needsRestack = true;
        }
        if(needsRestack) Restack(main);
        lastUsable = usable;
    }

    private bool lastUsable;

    void OnDestroy()
    {
        if(stackedOn != null && uiCamera != null)
        {
            UniversalAdditionalCameraData data = stackedOn.GetUniversalAdditionalCameraData();
            if(data != null) data.cameraStack.Remove(uiCamera);
        }
        if(instance == this) instance = null;
    }
}
