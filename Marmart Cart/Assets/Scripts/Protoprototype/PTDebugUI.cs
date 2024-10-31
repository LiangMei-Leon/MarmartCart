using UnityEngine;
using TMPro;

public class PTDebugUI : MonoBehaviour
{
    [SerializeField] private Rigidbody cartBody;
    [SerializeField] TextMeshProUGUI speedUI;
    [SerializeField] private WheelSuspensionScript wsScript;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        float speed = Mathf.Min(cartBody.linearVelocity.magnitude, wsScript.maxSpeed); // Cap speed at Maximum Value
        speedUI.text = "Speed: " + speed.ToString("F2"); // Format to two decimal places
    }
}
