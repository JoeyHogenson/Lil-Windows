using UnityEngine;

public class SwitchArt : MonoBehaviour
{
    public Material[] artMaterials;
    public GameObject wall;
    private int times;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SwitchArtMaterial()
    {
        if(times <= artMaterials.Length - 1)
        {
            MeshRenderer mesh_renderer = wall.GetComponent<MeshRenderer>();
            mesh_renderer.material = artMaterials[times];
            times++;
        }
    }
}
