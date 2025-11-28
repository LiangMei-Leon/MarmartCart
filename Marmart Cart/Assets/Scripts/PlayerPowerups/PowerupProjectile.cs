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
    public string ownerTag;

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
    }
}
