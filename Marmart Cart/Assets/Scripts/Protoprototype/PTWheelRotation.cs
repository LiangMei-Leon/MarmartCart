using UnityEngine;
using System.Collections.Generic;

public class PTWheelRotation : MonoBehaviour
{
    public List<GameObject> objectsToRotate;
    // Exposed variables to set maximum rotation angle and rotation speed
    public float maxRotationAngle = 30f; // Maximum allowed angle in degrees
    public float rotationSpeed = 50f;    // Speed at which the wheel rotates

    private float currentRotationAngle = 0f; // Tracks the current rotation angle

    void Update()
    {
        // Get input from user (A and D keys)
        float input = Input.GetAxisRaw("Horizontal"); // A = -1, D = 1

        // Determine if the input should be applied or reset
        if (input != 0)
        {
            // Calculate the desired rotation change based on input and speed
            float rotationChange = input * rotationSpeed * Time.deltaTime;

            // Adjust current rotation angle but clamp it to maxRotationAngle
            currentRotationAngle = Mathf.Clamp(currentRotationAngle + rotationChange, -maxRotationAngle, maxRotationAngle);
        }
        else
        {
            // If no input, smoothly snap back to 0 (going straight)
            currentRotationAngle = Mathf.MoveTowards(currentRotationAngle, 0f, rotationSpeed * Time.deltaTime);
        }

        // Apply the rotation to each object in the list
        foreach (GameObject obj in objectsToRotate)
        {
            if (obj != null) // Make sure the object reference is not null
            {
                obj.transform.localRotation = Quaternion.Euler(0f, currentRotationAngle, 0f);
            }
        }
    }
}
