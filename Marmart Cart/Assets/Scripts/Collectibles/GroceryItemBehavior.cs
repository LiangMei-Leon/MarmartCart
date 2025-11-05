using UnityEngine;

public class GroceryItemBehavior : MonoBehaviour
{
    [SerializeField] private SfxManager sfxManager;
    [SerializeField] private float selfCleanTime = 20f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        // Automatically destroy the powerup item after 30 seconds if not collected
        Destroy(this.gameObject, selfCleanTime);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player1"))
        {
            SnakeCartManager p1SnakeCartManager = other.GetComponentInParent<SnakeCartManager>();
            if (p1SnakeCartManager.HasEmptyCartForGroceryItem())
            {
                sfxManager.PlaySFX("CollectGroceryItem");
                p1SnakeCartManager.CollectGroceryItem();
                Destroy(this.gameObject);
            }
        }

        if (other.gameObject.CompareTag("Player2"))
        {
            SnakeCartManager p2SnakeCartManager = other.GetComponentInParent<SnakeCartManager>();
            if (p2SnakeCartManager.HasEmptyCartForGroceryItem())
            {
                sfxManager.PlaySFX("CollectGroceryItem");
                p2SnakeCartManager.CollectGroceryItem();
                Destroy(this.gameObject);
            }
        }
    }
}
