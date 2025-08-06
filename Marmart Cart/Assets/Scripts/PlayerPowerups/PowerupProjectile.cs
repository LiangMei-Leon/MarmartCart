using UnityEngine;
using UnityEngine.VFX;

public class PowerupProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 30f;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private LayerMask hitMask;
    [SerializeField] private bool isPlayer1 = true;

    private Rigidbody rb;
    private string ownerTag;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Launch(Vector3 direction, bool isP1)
    {
        isPlayer1 = isP1;
        ownerTag = isP1 ? "Player1" : "Player2";
        rb.linearVelocity = direction.normalized * speed;
        Destroy(gameObject, lifeTime);
        Debug.Log($"Player {(isPlayer1 ? 1 : 2)} launched projectile with direction: {direction.normalized} and speed: {speed}");
    }

    void OnTriggerEnter(Collider other)
    {
        GameObject hitObj = other.gameObject;

        // Ignore self or friendly fire
        if (hitObj.CompareTag(ownerTag)) return;

        // Check for Chained Cart
        if (hitObj.TryGetComponent(out ChainedCartManager hitCart))
        {
            if (hitCart.isCollectedByPlayer)
            {
                var cartRoot = hitObj.transform.root;
                if (cartRoot.GetComponentInChildren<CartControlScript>().GetIsInPit()) return;

                // Detach that cart
                Vector3 hitDirection = -rb.linearVelocity.normalized;
                hitCart.OnDetach(hitDirection);
                Destroy(gameObject);
                return;
            }
        }

        // Check for Leading Cart
        if (hitObj.TryGetComponent(out LeadingCartRaycaster leadingCart))
        {
            var rootCart = hitObj.transform.root;
            if (rootCart.GetComponentInChildren<CartControlScript>().GetIsInPit()) return;

            leadingCart.DetachSelfCompletely();

            Destroy(gameObject);
            return;
        }

        if (hitObj.CompareTag("Obstacles"))
        {
            Destroy(gameObject);
        }

        if (hitObj.CompareTag("Walls"))
        {
            Destroy(gameObject);
        }
    }
}
