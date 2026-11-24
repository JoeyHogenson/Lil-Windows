using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Lil Windows > Place Wall Canvas: click a wall in the Scene view to make that patch of it drawable.</summary>
public static class WallCanvasSetup
{
    private const string MaterialPath = "Assets/Materials/Wall Drawing.mat";
    //same range as doors and everything else the player uses (see InteractionSetup)
    private const float UseRange = 15f;

    private static bool placing;

    [MenuItem("Lil Windows/Place Wall Canvas")]
    private static void StartPlacing()
    {
        if(placing) return;
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[WallCanvasSetup] Leave Play mode first, or the canvas won't be kept.");
            return;
        }
        placing = true;
        SceneView.duringSceneGui += OnSceneGUI;
        if(SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Focus();
        SceneView.RepaintAll();
    }

    private static void StopPlacing()
    {
        placing = false;
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
    }

    private static void OnSceneGUI(SceneView view)
    {
        Handles.BeginGUI();
        GUI.Label(new Rect(60f, 10f, 320f, 22f), "Click a wall to place a drawing canvas (Esc cancels)", EditorStyles.helpBox);
        Handles.EndGUI();

        Event e = Event.current;
        if(e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if(e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            e.Use();
            StopPlacing();
            return;
        }
        //alt-click orbits the view
        if(e.type != EventType.MouseDown || e.button != 0 || e.alt) return;
        e.Use();

        if(!HandleUtility.PlaceObject(e.mousePosition, out Vector3 position, out Vector3 normal))
        {
            Debug.LogWarning("[WallCanvasSetup] Nothing under the cursor; click a wall.");
            return;
        }
        if(Mathf.Abs(normal.normalized.y) > 0.7f)
        {
            Debug.LogWarning("[WallCanvasSetup] That's a floor or ceiling; click a wall.");
            return;
        }
        GameObject wall = HandleUtility.PickGameObject(e.mousePosition, false);
        StopPlacing();
        Place(position, normal.normalized, wall);
    }

    private static void Place(Vector3 position, Vector3 normal, GameObject wall)
    {
        //the scene isn't in meters, so size everything from the player
        float playerHeight = 2f;
        var logic = Object.FindFirstObjectByType<StarterAssets.PlayerLogic>(FindObjectsInactive.Include);
        if(logic != null && logic.TryGetComponent(out CharacterController body))
        {
            playerHeight = body.height * Mathf.Abs(body.transform.lossyScale.y);
        }

        //clear of the wall, so the two don't flicker through each other
        float gap = playerHeight * 0.006f;
        //the wall's collider can stick out past its mesh; the canvas's has to be in front of it to be looked at
        float front = ColliderBulge(position, normal, playerHeight * 0.15f) + gap * 0.5f;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Wall Canvas";
        if(wall != null) SceneManager.MoveGameObjectToScene(go, wall.scene);
        Undo.RegisterCreatedObjectUndo(go, "Place Wall Canvas");
        //a quad shows its face toward local -Z
        go.transform.SetPositionAndRotation(position + normal * gap, Quaternion.LookRotation(-normal, Vector3.up));
        go.transform.localScale = new Vector3(playerHeight * 0.9f, playerHeight * 0.6f, 1f);
        //NPCs turn around at things tagged Wall
        if(wall != null && wall.CompareTag("Wall")) go.tag = "Wall";

        Object.DestroyImmediate(go.GetComponent<MeshCollider>());
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(1f, 1f, front + gap);
        box.center = new Vector3(0f, 0f, (gap - front) * 0.5f);

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        //WallCanvas turns it on in Play mode
        renderer.enabled = false;

        var usable = go.AddComponent<PixelCrushers.DialogueSystem.Wrappers.Usable>();
        usable.overrideName = "Wall";
        usable.overrideUseMessage = "(E to draw)";
        usable.maxUseDistance = UseRange;
        go.AddComponent<WallCanvas>();

        Selection.activeGameObject = go;
        Debug.Log("[WallCanvasSetup] Placed a wall canvas. Move or scale it (X/Y) to fit, then save the scene.", go);
    }

    //how far solid colliders stick out past the clicked surface
    private static float ColliderBulge(Vector3 position, Vector3 normal, float reach)
    {
        Physics.SyncTransforms();
        float bulge = 0f;
        foreach(RaycastHit hit in Physics.RaycastAll(position + normal * reach, -normal, reach, ~0, QueryTriggerInteraction.Ignore))
        {
            bulge = Mathf.Max(bulge, reach - hit.distance);
        }
        return bulge;
    }

    //a real asset rather than one made at runtime, so builds keep the shader variant it needs
    private static Material GetMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(material != null) return material;

        material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
        //cutout: pixels are drawn or not, so it sorts and pixelates like the wall itself
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.5f);
        material.SetFloat("_SpecularHighlights", 0f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)RenderQueue.AlphaTest;
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }
}
