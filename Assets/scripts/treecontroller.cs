using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class treecontroller : MonoBehaviour
{
    public Collider me;
    public int hits;
    public int currencyValue = 1;  // Amount of currency this tree is worth
    private MovementController player;  // Reference to the player
    public GameObject logdrop;  // Prefab to drop when the tree is cut down

    // Start is called before the first frame update
    void Start()
    {
        hits = 0;
        player = GameObject.FindObjectOfType<MovementController>();  // Find the player in the scene
    }

    // Update is called once per frame
    void Update()
    {
        if (hits > 2)
        {

            //drop a log object
            GameObject log = Instantiate(logdrop, transform.position, Quaternion.identity);
            Destroy(transform.parent.gameObject);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("axe") == true)
        {
            hits++;
        }
    }
}