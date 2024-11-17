using UnityEngine;

public class LeadingCartRaycaster : MonoBehaviour
{
    [Header("Raycast Settings")]
    [SerializeField] LayerMask layerMask;
    [SerializeField] float distance;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void FixedUpdate()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, distance, layerMask))
        {
            Debug.Log(hit.transform.gameObject.name);
        }
    }

    void OnDrawGizmos()
    {
        // Draw our friend ray
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * distance);
    }
}
