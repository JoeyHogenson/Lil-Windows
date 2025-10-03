using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SpriteRendererMeshBaker : MonoBehaviour
{
    [ContextMenu("Bake SpriteRenderers to Mesh")]
    void BakeSprites()
    {
        var spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        if (spriteRenderers.Length == 0)
        {
            Debug.LogWarning("No SpriteRenderers found.");
            return;
        }

        Material sharedMaterial = spriteRenderers[0].sharedMaterial;
        Texture2D sharedTexture = spriteRenderers[0].sprite.texture;

        // Ensure all sprites use same texture/material
        foreach (var sr in spriteRenderers)
        {
            if (sr.sprite.texture != sharedTexture)
            {
                Debug.LogError("Sprites use different textures. Atlas them first.");
                return;
            }
            if (sr.sharedMaterial != sharedMaterial)
            {
                Debug.LogError("SpriteRenderers use different materials.");
                return;
            }
        }

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        int vertexOffset = 0;
        foreach (var sr in spriteRenderers)
        {
            Sprite sprite = sr.sprite;
            Transform t = sr.transform;

            Vector2[] spriteVerts = sprite.vertices;
            ushort[] spriteTris = sprite.triangles;
            Vector2[] spriteUV = sprite.uv;

            foreach (Vector2 v in spriteVerts)
            {
                // Convert sprite space to world space
                Vector3 worldV = t.localToWorldMatrix.MultiplyPoint(new Vector3(v.x, v.y, 0));
                verts.Add(transform.InverseTransformPoint(worldV)); // local to parent
            }

            uvs.AddRange(spriteUV);

            foreach (ushort tri in spriteTris)
            {
                tris.Add(vertexOffset + tri);
            }

            vertexOffset += spriteVerts.Length;
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject combined = new GameObject("CombinedSprites");
        combined.transform.SetParent(transform, false);
        combined.transform.localPosition = Vector3.zero;

        var mf = combined.AddComponent<MeshFilter>();
        var mr = combined.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;
        mr.sharedMaterial = sharedMaterial;

#if UNITY_EDITOR
        string path = "Assets/BakedMesh.asset";
        AssetDatabase.CreateAsset(mesh, path);
        Debug.Log("Mesh saved to: " + path);
#endif

        // Optionally disable original renderers
        foreach (var sr in spriteRenderers)
        {
            sr.gameObject.SetActive(false);
        }
    }
}
