using UnityEngine;
using Unity.Cinemachine;

public class PlayerCameraManager : MonoBehaviour
{
    [Header("Player identity")]
    [SerializeField] private int playerIndex = 1; // 1 or 2, mostly for debugging

    [Header("Cameras for this player")]
    [SerializeField] private CinemachineCamera followCamera;       // top-down / normal
    [SerializeField] private CinemachineCamera checkoutCameraLane1;
    [SerializeField] private CinemachineCamera checkoutCameraLane2;

    [Header("Priority settings")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int idlePriority = 10;

    private CinemachineCamera _current;

    void Start()
    {
        // Start in gameplay mode
        SetActiveCamera(followCamera);
    }

    // -------- Gameplay follow setup --------

    public void SetFollowTarget(Transform target)
    {
        if (followCamera != null)
        {
            followCamera.Follow = target;
            // If you use LookAt on this camera:
            // followCamera.LookAt = target;
        }
    }

    // -------- Checkout entry/exit --------

    public void EnterCheckoutLane(int laneIndex)
    {
        CinemachineCamera targetCam = null;

        switch (laneIndex)
        {
            case 1: targetCam = checkoutCameraLane1; break;
            case 2: targetCam = checkoutCameraLane2; break;
            default:
                Debug.LogWarning($"[PlayerCameraManager P{playerIndex}] Invalid lane index {laneIndex}");
                return;
        }

        if (targetCam == null)
        {
            Debug.LogWarning($"[PlayerCameraManager P{playerIndex}] Checkout camera for lane {laneIndex} not assigned.");
            return;
        }

        SetActiveCamera(targetCam);
    }

    public void ExitCheckout()
    {
        if (followCamera == null)
        {
            Debug.LogWarning($"[PlayerCameraManager P{playerIndex}] Follow camera not assigned.");
            return;
        }

        SetActiveCamera(followCamera);
    }

    // -------- Core: priority switching for THIS player only --------

    void SetActiveCamera(CinemachineCamera cam)
    {
        if (cam == null) return;

        // raise new cam
        cam.Priority = activePriority;

        // lower old cam
        if (_current != null && _current != cam)
            _current.Priority = idlePriority;

        _current = cam;
    }
}