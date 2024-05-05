using UnityEngine;

public class WaterMover : MonoBehaviour
{
    public float amplitude = 0.5f; // Height amplitude
    public float frequency = 1.0f; // Oscillation frequency

    private float originalY;

    void Start()
    {
        originalY = transform.position.y;
    }

    void Update()
    {
        float newY = originalY + amplitude * Mathf.Sin(Time.time * frequency);
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
}