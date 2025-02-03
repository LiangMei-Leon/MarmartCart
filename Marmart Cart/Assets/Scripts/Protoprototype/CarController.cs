using UnityEngine;

public class CarController : MonoBehaviour
{
    public float moveSpeed = 50f;
    public float maxSpeed = 15f;

    public float drag = 0.98f;
    public float steerAngle = 20f;
    public float traction = 1f;
    private Vector3 moveForce;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        moveForce += transform.forward * moveSpeed * Input.GetAxis("Vertical") * Time.deltaTime;
        transform.position += moveForce * Time.deltaTime;
        //I am now working on Linux
        float steerInput = Input.GetAxis("Horizontal");
        transform.Rotate(Vector3.up * steerInput * moveForce.magnitude * steerAngle * Time.deltaTime);
        moveForce *= drag;
        moveForce = Vector3.ClampMagnitude(moveForce, maxSpeed);

        moveForce = Vector3.Lerp(moveForce.normalized, transform.forward, traction * Time.deltaTime) * moveForce.magnitude;
    }
}
