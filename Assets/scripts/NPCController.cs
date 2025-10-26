using UnityEngine;

public class NPCController : MonoBehaviour
{
    public GameObject NPC;

    public float NPCspeed;
    public Animator animator;

    public Ray ray;
    public RaycastHit hit;

    public Vector3 y_rotation = new Vector3(0,90,0);
    private Vector3 up = new Vector3 ( 0,5,0);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Vector3 up = new Vector3 ( 0,5,0);
        animator.SetBool("isWalking", true);
    }

    // Update is called once per frame
    void Update()
    { 
        
        moveNPC();
        
        
    }
    public void moveNPC()
    {
        NPC.transform.position += NPCspeed * transform.forward;
        if (Physics.Raycast(transform.position+up, transform.forward, out hit) && hit.collider.CompareTag("Wall"))
        {
            if(hit.distance <= 5f)
            {
                NPC.transform.Rotate(y_rotation);
                Debug.Log("NPC hit: " + hit.collider.name);
            }
            // Raycast hit something
            
            
            // Access hit.collider, hit.point, hit.normal, etc.
        }
        

    }
    
}
