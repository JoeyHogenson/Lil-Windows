using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class TreeArenaController : MonoBehaviour
{
    public GameObject tree;
    public BoxCollider bCollider;  // Reference to the mesh collider
    public int numberOfObjects = 10;  // Number of objects to spawn
    public float fixedY = 1.0f;  // Fixed Y position
    // Start is called before the first frame update
    void Start()
    {
        PopulateTrees();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void PopulateTrees()
    {
        Bounds bounds = bCollider.bounds;

        for (int i = 0; i < numberOfObjects; i++)
        {
            // Generate random positions within mesh bounds
          
            float posX = Random.Range(bounds.min.x, bounds.max.x);
            washrands();
            float posZ = Random.Range(bounds.min.z, bounds.max.z);
            washrands();
            float posY= Random.Range(bounds.min.y, bounds.min.y+1);
            washrands();

            Vector3 spawnPosition = new Vector3(posX, posY + fixedY, posZ);

            // Instantiate the object at the random position with fixed Y
            if (bounds.Contains(spawnPosition))
            {
                Instantiate(tree, spawnPosition, Quaternion.identity);
            }
          
           
        }
    }
    void washrands()
    {
        for (int i = 0; i < 200; i++)
        {
            float wash = Random.Range(0, 40000);
        }
    }
}
