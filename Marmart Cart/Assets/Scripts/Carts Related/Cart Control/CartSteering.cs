using System.Collections.Generic;
using UnityEngine;

public class CartSteering : MonoBehaviour
{
    [SerializeField] CartControlScript cartControlInput;
    [SerializeField] Rigidbody cartBody;

    public List<GameObject> objectsToRotate;

    [Header("Normal Direction-Seek Steering")]
    [SerializeField] float maxRotationAngle = 30f;
    [SerializeField] float rotationSpeed = 80f;

    [Header("Prototype A - Left/Right Steering")]
    [SerializeField] private bool enablePrototypeLeftRightSteering = false;
    [SerializeField] private float prototypeMaxSteerAngle = 35f;
    [SerializeField] private float prototypeSteerDegreesPerSecond = 180f;
    [SerializeField] private bool debugPrototypeSteering = false;

    [Header("Camera-Mapped Drift Steering")]
    [SerializeField] private bool enableCameraMappedDriftSteering = true;
    [SerializeField] private CartDriftController driftController;
    [SerializeField] private float driftSteerDegreesPerSecond = 220f;
    [SerializeField] private bool debugDriftSteering = false;

    private float currentRotationAngle = 0f;

    void Update()
    {
        if (ShouldUseDriftSteering())
        {
            UpdateDriftSteering();
        }
        else if (enablePrototypeLeftRightSteering)
        {
            UpdatePrototypeLeftRightSteering();
        }
        else
        {
            UpdateDirectionSeekSteering();
        }

        ApplyWheelRotation();
    }

    private bool ShouldUseDriftSteering()
    {
        return enableCameraMappedDriftSteering &&
               driftController != null &&
               driftController.IsDrifting &&
               driftController.EnableDriftSteeringOutput;
    }

    private void UpdateDirectionSeekSteering()
    {
        Vector3 desiredDirection = cartControlInput.desiredDirection;

        if (desiredDirection.sqrMagnitude > 0.001f)
        {
            float angleDifference = Vector3.SignedAngle(transform.forward, desiredDirection, Vector3.up);
            float targetWheelAngle = Mathf.Clamp(angleDifference, -maxRotationAngle, maxRotationAngle);

            currentRotationAngle = Mathf.Lerp(
                currentRotationAngle,
                targetWheelAngle,
                Time.deltaTime * rotationSpeed
            );
        }
        else
        {
            currentRotationAngle = Mathf.MoveTowards(
                currentRotationAngle,
                0f,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    private void UpdatePrototypeLeftRightSteering()
    {
        float steerInput = cartControlInput.GetSteerInput();
        float targetWheelAngle = steerInput * prototypeMaxSteerAngle;

        currentRotationAngle = Mathf.MoveTowards(
            currentRotationAngle,
            targetWheelAngle,
            prototypeSteerDegreesPerSecond * Time.deltaTime
        );

        if (debugPrototypeSteering)
        {
            Debug.Log(
                $"[Prototype Steering] input: {steerInput:F2}, " +
                $"targetAngle: {targetWheelAngle:F1}, " +
                $"currentAngle: {currentRotationAngle:F1}"
            );
        }
    }

    private void UpdateDriftSteering()
    {
        float targetWheelAngle = driftController.DriftSteeringAngle;

        currentRotationAngle = Mathf.MoveTowards(
            currentRotationAngle,
            targetWheelAngle,
            driftSteerDegreesPerSecond * Time.deltaTime
        );

        if (debugDriftSteering)
        {
            Debug.Log(
                $"[Drift Steering] side: {driftController.DriftSideName}, " +
                $"tightness: {driftController.CurrentTightness:F2}, " +
                $"targetAngle: {targetWheelAngle:F1}, " +
                $"currentAngle: {currentRotationAngle:F1}"
            );
        }
    }

    private void ApplyWheelRotation()
    {
        foreach (GameObject obj in objectsToRotate)
        {
            if (obj != null)
            {
                obj.transform.localRotation = Quaternion.Euler(0f, currentRotationAngle, 0f);
            }
        }
    }

    public float GetCurrentSteeringAngle()
    {
        return currentRotationAngle;
    }
}