using UnityEngine;
using UnityEngine.AI;

public class PrisonerNavigation : MonoBehaviour
{
    public GameController gameControllerScript;
    public Transform player;
    private Animator prisonerAnimator;
    private NavMeshAgent prisoner;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        prisoner = GetComponent<NavMeshAgent>();
        prisonerAnimator = GetComponent<Animator>();
        //an enabled agent snaps the prisoner onto the nearest NavMesh, yanking him off his seat;
        //it stays off until a lockdown actually needs him to walk
        if(prisoner != null) prisoner.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        if(prisoner == null || gameControllerScript == null || player == null || !gameControllerScript.isLockdown) return;

        if(!prisoner.enabled) prisoner.enabled = true;
        if(!prisoner.isOnNavMesh) return;

        float distanceBetween = Vector3.Distance(prisoner.transform.position, player.position);
        if(distanceBetween < 5)
        {
            prisoner.isStopped = true;
            prisonerAnimator.SetBool("closeEnough", true);
            prisonerAnimator.SetBool("getUp", false);
        }
        else
        {
            prisoner.isStopped = false;
            prisonerAnimator.SetBool("getUp", true);
            prisonerAnimator.SetBool("closeEnough",false);
            prisoner.destination = player.position;
        }
    }
}
