using UnityEngine;
using System.Collections;

public class water : MonoBehaviour
{
    public float scrollSpeed = 0.01F;
    public Renderer rend;
    void Start()
    {
        rend = GetComponent<Renderer>();
    }
    void Update()
    {
        float offset = Time.time * scrollSpeed /4;
        rend.material.SetTextureOffset("_MainTex", new Vector2(offset, 0));
    }
}