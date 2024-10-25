using UnityEngine;
using System.Collections.Generic;

public class PTWheelRotation : MonoBehaviour
{
    public List<GameObject> objectsToRotate;
    // Exposed variables to set maximum rotation angle and rotation speed
    public float maxRotationAngle = 30f; // Maximum allowed angle in degrees
    public float rotationSpeed = 50f;    // Speed at which the wheel rotates

    private float currentRotationAngle = 0f; // Tracks the current rotation angle

    // Update is called once per frame
    void Update()
    {
        // Get input from user (A and D keys)
        float input = Input.GetAxis("Horizontal"); // A = -1, D = 1
        
        // Calculate the desired rotation change based on input and speed
        float rotationChange = input * rotationSpeed * Time.deltaTime;
        
        // Adjust current rotation angle but clamp it to maxRotationAngle
        currentRotationAngle = Mathf.Clamp(currentRotationAngle + rotationChange, -maxRotationAngle, maxRotationAngle);

        foreach (GameObject obj in objectsToRotate)
        {
            if (obj != null) // Make sure the object reference is not null
            {
                obj.transform.localRotation = Quaternion.Euler(0f, currentRotationAngle, 0f);
            }
        }
    }
}
