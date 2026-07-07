using UnityEngine;
using Unity.Cinemachine;
public class CameraManager : MonoBehaviour
{
    /// <summary>
    /// hi i made changes.
    /// </summary>
    [SerializeField] private GameObject chainedCartsP1;
    [SerializeField] private GameObject chainedCartsP2;
    [SerializeField] private GameObject chainedCartsP3;
    [SerializeField] private GameObject chainedCartsP4;

    [Header("Cameras")]
    [SerializeField] CinemachineCamera topDownCameraP1;
    [SerializeField] CinemachineCamera topDownCameraP2;
    [SerializeField] CinemachineCamera topDownCameraP3;
    [SerializeField] CinemachineCamera topDownCameraP4;

    private void OnEnable()
    {
        CameraSwitcher.Register(topDownCameraP1);
        CameraSwitcher.Register(topDownCameraP2);
        CameraSwitcher.Register(topDownCameraP3);
        CameraSwitcher.Register(topDownCameraP4);
    }

    private void OnDisable()
    {
        CameraSwitcher.Unregister(topDownCameraP1);
        CameraSwitcher.Unregister(topDownCameraP2);
        CameraSwitcher.Unregister(topDownCameraP3);
        CameraSwitcher.Unregister(topDownCameraP4);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetCameraP1ToLookAtLeadingCart()
    {
        if(chainedCartsP1 == null || topDownCameraP1 == null)
        {
            Debug.LogError("Missing target objects or target cameraP1 to setup");
        }
        CameraSwitcher.UpdateCameraFocus(topDownCameraP1, chainedCartsP1.transform.GetChild(0));
    }
    public void SetCameraP2ToLookAtLeadingCart()
    {
        if (chainedCartsP2 == null || topDownCameraP2 == null)
        {
            Debug.LogError("Missing target objects or target cameraP2 to setup");
        }
        CameraSwitcher.UpdateCameraFocus(topDownCameraP2, chainedCartsP2.transform.GetChild(0));
    }
    public void SetCameraP3ToLookAtLeadingCart()
    {
        if (chainedCartsP3 == null || topDownCameraP3 == null)
        {
            Debug.LogError("Missing target objects or target cameraP3 to setup");
        }
        CameraSwitcher.UpdateCameraFocus(topDownCameraP3, chainedCartsP3.transform.GetChild(0));
    }
    public void SetCameraP4ToLookAtLeadingCart()
    {
        if (chainedCartsP4 == null || topDownCameraP4 == null)
        {
            Debug.LogError("Missing target objects or target cameraP4 to setup");
        }
        CameraSwitcher.UpdateCameraFocus(topDownCameraP4, chainedCartsP4.transform.GetChild(0));
    }
}
