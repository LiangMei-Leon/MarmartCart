using UnityEngine;
using TMPro;

public class PTDebugUI : MonoBehaviour
{
    [SerializeField] private Rigidbody cartBody;
    [SerializeField] TextMeshProUGUI speedUI;
    [SerializeField] private LeadingCartBehaviour cartScript;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        float speed = cartBody.linearVelocity.magnitude; // Cap speed at Maximum Value
        speedUI.text = "Speed: " + speed.ToString("F2"); // Format to two decimal places
    }
}
