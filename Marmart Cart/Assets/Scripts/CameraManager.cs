using UnityEngine;
using Unity.Cinemachine;
public class CameraManager : MonoBehaviour
{
    [SerializeField] private GameObject chainedCartsP1;
    [SerializeField] private GameObject chainedCartsP2;

    [Header("Cameras")]
    [SerializeField] CinemachineCamera topDownCameraP1;
    [SerializeField] CinemachineCamera topDownCameraP2;

    private void OnEnable()
    {
        CameraSwitcher.Register(topDownCameraP1);
        CameraSwitcher.Register(topDownCameraP2);
    }

    private void OnDisable()
    {
        CameraSwitcher.Unregister(topDownCameraP1);
        CameraSwitcher.Unregister(topDownCameraP2);
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
}
