using UnityEngine;

public class bobber : MonoBehaviour
{
    [Header("Bobbing Settings")]
    public float amplitude = 0.5f;    // Vertical bob height
    public float frequency = 1f;      // Vertical bob speed

    [Header("Rotation Settings")]
    public Vector3 rotationAxis = new Vector3(0f, 1f, 0f);  // Rotation axis
    public float rotationSpeed = 45f; // Degrees per second

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        // Bobbing motion
        float offset = Mathf.Sin(Time.time * frequency) * amplitude;
        transform.localPosition = startPos + Vector3.up * offset;

        // Continuous rotation
        transform.Rotate(rotationAxis.normalized * rotationSpeed * Time.deltaTime);
    }
}
