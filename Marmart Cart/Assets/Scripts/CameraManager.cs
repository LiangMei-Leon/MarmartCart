using UnityEngine;
using Unity.Cinemachine;
public class CameraManager : MonoBehaviour
{
    [SerializeField] private GameObject chainedCarts;

    [Header("Cameras")]
    [SerializeField] CinemachineCamera topDownCamera;

    private void OnEnable()
    {
        CameraSwitcher.Register(topDownCamera);
    }

    private void OnDisable()
    {
        CameraSwitcher.Unregister(topDownCamera);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetCameraToLookAtLeadingCart()
    {
        if(chainedCarts == null || topDownCamera == null)
        {
            Debug.LogError("Missing target objects or target camera to setup");
        }
        CameraSwitcher.UpdateCameraFocus(topDownCamera, chainedCarts.transform.GetChild(0));
    }
}
