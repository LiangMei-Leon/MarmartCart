using UnityEngine;

public class PTCollectible : MonoBehaviour
{
    private PTObjectCollect collector; // Reference to PTObjectCollect manager

    private void Start()
    {
        collector = FindObjectOfType<PTObjectCollect>(); // Automatically find the collector
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && collector != null)
        {
            collector.MarkAsCollected(gameObject); // Notify collector
            gameObject.SetActive(false); // Hide the object (simulates "collected")
        }
    }
}
