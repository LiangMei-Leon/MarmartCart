using UnityEngine;
using UnityEngine.SceneManagement;

public class PTCameraControl : MonoBehaviour
{
    public Transform[] cameraAngles; // Array to hold preset camera positions and rotations
    public float switchSpeed = 5f; // Speed at which the camera transitions to a new angle

    private Transform targetAngle; // The current target camera position and rotation

    void Start()
    {
        if (cameraAngles.Length > 0)
        {
            targetAngle = cameraAngles[0]; // Set the initial camera angle
            transform.position = targetAngle.position;
            transform.rotation = targetAngle.rotation;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) && cameraAngles.Length > 0)
        {
            targetAngle = cameraAngles[0];
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) && cameraAngles.Length > 1)
        {
            targetAngle = cameraAngles[1];
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) && cameraAngles.Length > 2)
        {
            targetAngle = cameraAngles[2];
        }

        // Smoothly transition to the target angle
        if (targetAngle != null)
        {
            transform.position = Vector3.Lerp(transform.position, targetAngle.position, Time.deltaTime * switchSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetAngle.rotation, Time.deltaTime * switchSpeed);
        }
        // Reload the scene when "R" is pressed
        if (Input.GetKeyDown(KeyCode.R))
        {
            ReloadScene();
        }
        void ReloadScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
