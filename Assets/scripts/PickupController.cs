using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickupController : MonoBehaviour
{
    // Start is called before the first frame update
    public string type;
    public GameObject parent;
    // How far down we check. Big enough to cover tall drops.
    public float maxDropDistance = 100000f;
    // Layer mask to control what counts as "ground" (optional).
    public LayerMask groundMask = Physics.DefaultRaycastLayers;

    public void SnapToGround()
    {
        Vector3 start = transform.position + Vector3.up * 0.5f; // Start slightly above to avoid clipping.
        Ray ray = new Ray(start, Vector3.down);

        RaycastHit[] hits = Physics.RaycastAll(ray, maxDropDistance, groundMask);

        if (hits.Length > 0)
        {
            // Sort hits so the first one is the highest point.
            System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));

            // Move the object to the highest point found.
            transform.position = new Vector3(transform.position.x, hits[0].point.y+0.3f, transform.position.z);
        }
        else
        {
            Debug.LogWarning("No ground found below " + gameObject.name);
        }
    }
    void Start()
    {
        SnapToGround(); // Snap to the ground when the object is created.
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other != null)
        {
            if (other.CompareTag("Player"))
            {
                if (type == "oil")
                {
                    MovementController player = GameObject.FindObjectOfType<MovementController>();
                    player.rust = player.rust - 3;
                    Destroy(parent);
                }
                if (type == "fuel")
                {
                    MovementController player = GameObject.FindObjectOfType<MovementController>();
                    player.fuel++;
                    Destroy(parent);
                }
                if (type == "axe")
                {
                    MovementController player = GameObject.FindObjectOfType<MovementController>();
                   //give player axe
                    Destroy(parent);
                }
                if (type == "log")
                {
                    MovementController player = GameObject.FindObjectOfType<MovementController>();
                    if (player.wood < player.maxWood)
                    {
                        player.wood++;  // Give the player currency
                        Destroy(parent);
                    }
                    else
                    {
                        Debug.Log("Cannot pick up more logs.");
                    }

                }
            }
                
            }
        }

    }

