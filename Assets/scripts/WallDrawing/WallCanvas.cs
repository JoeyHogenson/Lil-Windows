using System;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// A flat patch of wall the player can draw on. It's a see-through quad sitting just in front of the wall, so the
/// wall's own material and UVs don't matter. Looking at it offers "(E to draw)" through its Usable, which sends
/// OnUse here and opens <see cref="WallDrawMode"/>. Place with Lil Windows > Place Wall Canvas; resize by scaling.
/// Drawings last until the scene is unloaded (they aren't in the save file yet).
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class WallCanvas : MonoBehaviour
{
    [Serializable]
    public struct Pen
    {
        public string name;
        public Color color;
    }

    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly Color32 Blank = new Color32(0, 0, 0, 0);

    [Tooltip("What the player can draw with, in toolbar order.")]
    public Pen[] pens =
    {
        new Pen { name = "Pencil", color = new Color(0.17f, 0.17f, 0.2f) },
        new Pen { name = "Red", color = new Color(0.65f, 0.13f, 0.11f) },
        new Pen { name = "Blue", color = new Color(0.14f, 0.24f, 0.58f) },
    };
    [Tooltip("Drawing resolution from top to bottom. Width follows the canvas shape. Lower = chunkier pixels.")]
    [Range(32, 512)] public int pixelsTall = 192;
    [Tooltip("How much of the wall's grain shows through strokes. 0 = solid lines.")]
    [Range(0f, 0.6f)] public float grain = 0.15f;

    private Texture2D texture;
    private Material material;
    private bool changed;

    void Awake()
    {
        float aspect = Mathf.Abs(transform.lossyScale.x / transform.lossyScale.y);
        int width = Mathf.Clamp(Mathf.RoundToInt(pixelsTall * aspect), 16, 2048);
        texture = new Texture2D(width, pixelsTall, TextureFormat.RGBA32, false)
        {
            name = name + " Drawing",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Clear();

        //hidden in the editor (it would be a blank white board); only shown once it has a texture
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        material = meshRenderer.material;
        material.SetTexture(BaseMap, texture);
        meshRenderer.enabled = true;
    }

    //one upload per frame however many strokes were drawn
    void LateUpdate()
    {
        if(!changed) return;
        changed = false;
        texture.Apply(false);
    }

    void OnDestroy()
    {
        if(texture != null) Destroy(texture);
        if(material != null) Destroy(material);
    }

    //sent by the player's LookSelector
    void OnUse(Transform actor)
    {
        WallDrawMode.Enter(this);
    }

    /// <summary>Where a ray meets the canvas's plane, in pixels. The point can lie outside the canvas.</summary>
    public bool TryGetPixel(Ray ray, out Vector2 pixel)
    {
        pixel = default;
        if(texture == null) return false;
        //the quad faces local -Z; nothing to draw on from behind
        if(Vector3.Dot(ray.direction, transform.forward) <= 0f) return false;
        if(!new Plane(transform.forward, transform.position).Raycast(ray, out float distance)) return false;

        Vector3 local = transform.InverseTransformPoint(ray.GetPoint(distance));
        //a grazing ray lands absurdly far away
        if(Mathf.Abs(local.x) > 2f || Mathf.Abs(local.y) > 2f) return false;
        pixel = new Vector2((local.x + 0.5f) * texture.width, (local.y + 0.5f) * texture.height);
        return true;
    }

    /// <summary>Draws a line between two pixel positions; parts off the canvas are clipped.</summary>
    public void Stroke(Vector2 from, Vector2 to, float diameter, Color color)
    {
        //the material is cutout, so a see-through pen would draw nothing
        color.a = 1f;
        Paint(from, to, diameter, color, grain);
    }

    public void Erase(Vector2 from, Vector2 to, float diameter)
    {
        Paint(from, to, diameter, Blank, 0f);
    }

    public void Clear()
    {
        NativeArray<Color32> pixels = texture.GetPixelData<Color32>(0);
        for(int i = 0; i < pixels.Length; i++) pixels[i] = Blank;
        changed = true;
    }

    private void Paint(Vector2 from, Vector2 to, float diameter, Color32 color, float skip)
    {
        NativeArray<Color32> pixels = texture.GetPixelData<Color32>(0);
        //one stamp per pixel of travel, so fast strokes have no gaps
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y))));
        for(int i = 0; i <= steps; i++)
        {
            Stamp(pixels, Vector2.Lerp(from, to, (float)i / steps), diameter * 0.5f, color, skip);
        }
        changed = true;
    }

    private void Stamp(NativeArray<Color32> pixels, Vector2 center, float radius, Color32 color, float skip)
    {
        int width = texture.width;
        int centerX = Mathf.FloorToInt(center.x);
        int centerY = Mathf.FloorToInt(center.y);
        int reach = Mathf.FloorToInt(radius);
        int minX = Mathf.Max(0, centerX - reach), maxX = Mathf.Min(width - 1, centerX + reach);
        int minY = Mathf.Max(0, centerY - reach), maxY = Mathf.Min(texture.height - 1, centerY + reach);
        for(int y = minY; y <= maxY; y++)
        {
            for(int x = minX; x <= maxX; x++)
            {
                int dx = x - centerX, dy = y - centerY;
                if(dx * dx + dy * dy > radius * radius) continue;
                if(skip > 0f && Grain(x, y) < skip) continue;
                pixels[y * width + x] = color;
            }
        }
    }

    //fixed per pixel rather than random, so it reads as the wall's texture and going over it doesn't fill it in
    private static float Grain(int x, int y)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65536f;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.3f, 0.8f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 1f, 0f));
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.12f);
        Gizmos.DrawCube(Vector3.zero, new Vector3(1f, 1f, 0f));
    }
}
