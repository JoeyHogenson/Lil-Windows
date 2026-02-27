using UnityEngine;
using UnityEngine.AI;

public class Navigation : MonoBehaviour
{
    public Transform player;
    private NavMeshAgent agent;
    private Transform NPC;
    public Transform countGoalPosition;
    public GameController gameController;
    private Animator COAnimator;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        NPC = GetComponent<Transform>();
        COAnimator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        float distanceBetween = Vector3.Distance(agent.transform.position, player.position);
        if(gameController.isLockdown && distanceBetween < 7.5)
        {
            agent.isStopped = true;
            COAnimator.SetBool("isStanding", true);
            COAnimator.SetBool("isWalking", false);
        }
        else if(gameController.isLockdown)
        {
            agent.isStopped = false;
            COAnimator.SetBool("isWalking",true);
            COAnimator.SetBool("isStanding",false);
            Debug.Log("doing this");
            agent.destination = player.position;
        }
        else
        {
            agent.isStopped = false;
            COAnimator.SetBool("isWalking",true);
            COAnimator.SetBool("isStanding",false);
            agent.destination = countGoalPosition.position;
        }
        
    }
}
