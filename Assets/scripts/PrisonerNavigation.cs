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
    }

    // Update is called once per frame
    void Update()
    {
        float distanceBetween = Vector3.Distance(prisoner.transform.position, player.position);
        if(gameControllerScript.isLockdown && distanceBetween < 5)
        {
            prisoner.isStopped = true;
            prisonerAnimator.SetBool("closeEnough", true);
            prisonerAnimator.SetBool("getUp", false);
        }
        else if(gameControllerScript.isLockdown)
        {
            prisoner.isStopped = false;
            prisonerAnimator.SetBool("getUp", true);
            prisonerAnimator.SetBool("closeEnough",false);

            prisoner.destination = player.position;
        }
    }
}
