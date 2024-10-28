using UnityEngine;
using TMPro;

public class PTDebugUI : MonoBehaviour
{
    [SerializeField] private Rigidbody cartBody;
    [SerializeField] TextMeshProUGUI speedUI;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        float speed = Mathf.Min(cartBody.linearVelocity.magnitude, 10f); // Cap speed at 10
        speedUI.text = "Speed: " + speed.ToString("F2"); // Format to two decimal places
    }
}
