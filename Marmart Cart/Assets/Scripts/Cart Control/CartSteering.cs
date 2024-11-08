using System.Collections.Generic;
using UnityEngine;

public class CartSteering : MonoBehaviour
{
    [SerializeField] CartControlScript cartControlInput; // Refer to the gathered player input
    [SerializeField] Rigidbody cartBody; // The rigidbody of the leading cart
    public List<GameObject> objectsToRotate;
    // Exposed variables to set maximum rotation angle and rotation speed
    [SerializeField] float maxRotationAngle = 30f; // Maximum allowed angle in degrees
    [SerializeField] float rotationSpeed = 80f;    // Speed at which the wheel rotates

    private float currentRotationAngle = 0f; // Tracks the current rotation angle


    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 desiredDirection = cartControlInput.desiredDirection;

        if (desiredDirection.sqrMagnitude > 0.01f)
        {
            // Calculate the angle difference between current forward and desired direction
            float angleDifference = Vector3.SignedAngle(transform.forward, desiredDirection, Vector3.up);

            // Determine target wheel angle based on max wheel angle limit
            float targetWheelAngle = Mathf.Clamp(angleDifference, -maxRotationAngle, maxRotationAngle);

            // Smoothly adjust current wheel angle towards target wheel angle
            currentRotationAngle = Mathf.Lerp(currentRotationAngle, targetWheelAngle, Time.deltaTime * rotationSpeed);

            // Apply the rotation to each object in the list
            foreach (GameObject obj in objectsToRotate)
            {
                if (obj != null) // Make sure the object reference is not null
                {
                    obj.transform.localRotation = Quaternion.Euler(0f, currentRotationAngle, 0f);
                }
            }
        }
        else
        {
            // If no input, smoothly snap back to 0 (going straight)
            currentRotationAngle = Mathf.MoveTowards(currentRotationAngle, 0f, rotationSpeed * Time.deltaTime);
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
}
