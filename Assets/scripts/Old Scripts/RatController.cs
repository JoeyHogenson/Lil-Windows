using UnityEngine;

public class RatController : MonoBehaviour
{
    public float speed = 3.0f;
    public int health = 1;

    private Vector3 direction;
    private CharacterController controller;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        direction = Random.insideUnitSphere;
    }

    void Update()
    {
        if (!controller.isGrounded)
        {
            direction.y = -1.0f;  // Apply gravity
        }
        else if (Random.value < 0.01f)  // Change direction randomly
        {
            direction = Random.insideUnitSphere;
            direction.y = 0;
        }

        controller.Move(direction * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("axe"))
        {
            health--;
            if (health <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
}