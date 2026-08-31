using System.Collections;
using UnityEngine;

/// <summary>
/// Detects battle contacts from the leading cart's front battle trigger shape
/// and resolves this cart's loss.
///
/// Battle rule:
/// - Hit your own collected chain cart -> you lose.
/// - Hit another player's collected chain cart -> you lose.
/// - Hit another player's leading cart -> you lose.
/// - Free/uncollected carts do not count.
///
/// Several trigger BoxColliders can live on this same GameObject to build the
/// desired battle detection shape. Duplicate trigger callbacks are naturally
/// ignored once this cart enters its battle cooldown.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LeadingCartBattleController : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private CartControlScript cartControlInput;
    [SerializeField] private Rigidbody cartBody;

    [Tooltip("The four virtual-wheel LeadingCartBehaviour components that drive this Rigidbody.")]
    [SerializeField] private LeadingCartBehaviour[] wheelMovements;

    [SerializeField] private SfxManager sfxManager;
    [SerializeField] private CartMaterialManager cartMaterialManager;

    // The leading cart is instantiated under SnakeCartManager at runtime, so
    // this reference is resolved once after spawning instead of being prefab-wired.
    private SnakeCartManager snakeCartManager;

    #endregion

    #region Battle Loss Settings

    [Header("Battle Loss")]
    [Tooltip("How long this leading cart ignores further battle losses after losing.")]
    [Min(0f)]
    [SerializeField] private float ghostDuration = 4f;

    [Tooltip("How long wheel drive stays stopped after losing.")]
    [Min(0f)]
    [SerializeField] private float stopDuration = 2f;

    [Tooltip("Single centralized knockback impulse applied to the leading Rigidbody.")]
    [Min(0f)]
    [SerializeField] private float knockbackImpulse = 200f;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private float ghostUntilTime;

    private Coroutine stopRoutine;

    public bool IsInGhostMode => Time.time < ghostUntilTime;

    #endregion

    #region Events

    public System.Action OnBattleLost;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (cartBody == null) Debug.LogError("[LeadingCartBattleController] Cart Rigidbody is not assigned.", this);
        if (cartControlInput == null) Debug.LogError("[LeadingCartBattleController] CartControlScript is not assigned.", this);

        if ((wheelMovements == null || wheelMovements.Length == 0) && cartBody != null)
        {
            wheelMovements = cartBody.GetComponentsInChildren<LeadingCartBehaviour>(true);
        }

        if (cartMaterialManager == null && cartBody != null)
        {
            cartMaterialManager = cartBody.GetComponentInChildren<CartMaterialManager>(true);
        }

        ValidateTriggerColliders();
    }

    private void Start()
    {
        snakeCartManager = GetComponentInParent<SnakeCartManager>();

        if (snakeCartManager == null)
        {
            Debug.LogError("[LeadingCartBattleController] Could not find runtime SnakeCartManager owner.", this);
        }
    }

    private void OnDisable()
    {
        if (stopRoutine != null)
        {
            StopCoroutine(stopRoutine);
            stopRoutine = null;
        }
    }

    #endregion

    #region Battle Detection

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || IsInGhostMode) return;

        // Ignore every collider belonging to this same leading-cart Rigidbody,
        // including our own physical colliders and our other battle triggers.
        if (other.attachedRigidbody == cartBody) return;

        if (IsCollectedChainCart(other))
        {
            LoseBattle();
            return;
        }

        if (IsOtherLeadingCart(other))
        {
            LoseBattle();
        }
    }

    private bool IsCollectedChainCart(Collider other)
    {
        ChainedCartManager chainedCart = other.GetComponentInParent<ChainedCartManager>();
        return chainedCart != null && chainedCart.isCollectedByPlayer;
    }

    private bool IsOtherLeadingCart(Collider other)
    {
        Rigidbody otherBody = other.attachedRigidbody;

        if (otherBody == null || otherBody == cartBody) return false;

        // The battle controller lives on a child of the leading-cart Rigidbody,
        // so resolve it from the contacted Rigidbody's hierarchy.
        LeadingCartBattleController otherBattleController = otherBody.GetComponentInChildren<LeadingCartBattleController>(true);

        return otherBattleController != null && otherBattleController != this;
    }

    #endregion

    #region Battle Loss Resolution

    public void LoseBattle()
    {
        if (IsInGhostMode) return;

        EnterGhostMode(ghostDuration);
        DetachOwnChain();
        StopAndKnockBackCart();

        OnBattleLost?.Invoke();
    }

    private void DetachOwnChain()
    {
        if (snakeCartManager == null) return;

        var snakeBody = snakeCartManager.GetSnakeBody();

        if (snakeBody == null || snakeBody.Count < 2 || snakeBody[1] == null) return;

        ChainedCartManager firstFollower = snakeBody[1].GetComponent<ChainedCartManager>();

        if (firstFollower == null) return;

        firstFollower.OnDetach();

        if (sfxManager != null) sfxManager.PlaySFX("Detach");
    }

    private void StopAndKnockBackCart()
    {
        if (cartBody == null) return;

        // Capture direction before the wheel scripts stop the Rigidbody.
        Vector3 knockbackDirection = GetSafeKnockbackDirection();

        if (stopRoutine != null) StopCoroutine(stopRoutine);
        stopRoutine = StartCoroutine(StopCartRoutine(knockbackDirection));
    }

    private IEnumerator StopCartRoutine(Vector3 knockbackDirection)
    {
        // Stop all four wheel-drive components, but do NOT use their old
        // SetSpeedToZero(duration) overload because that applies knockback once
        // per wheel to the same Rigidbody.
        if (wheelMovements != null)
        {
            foreach (LeadingCartBehaviour wheelMovement in wheelMovements)
            {
                if (wheelMovement != null) wheelMovement.SetSpeedToZero();
            }
        }

        cartBody.linearVelocity = Vector3.zero;

        // Apply exactly one centralized knockback to the Rigidbody.
        if (knockbackImpulse > 0f)
        {
            cartBody.AddForce(knockbackDirection * knockbackImpulse, ForceMode.Impulse);
        }

        if (stopDuration > 0f) yield return new WaitForSeconds(stopDuration);

        if (wheelMovements != null)
        {
            foreach (LeadingCartBehaviour wheelMovement in wheelMovements)
            {
                if (wheelMovement != null) wheelMovement.ResetSpeed();
            }
        }

        stopRoutine = null;
    }

    private Vector3 GetSafeKnockbackDirection()
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(cartBody.linearVelocity, Vector3.up);

        if (planarVelocity.sqrMagnitude > 0.001f) return -planarVelocity.normalized;

        Vector3 backward = -Vector3.ProjectOnPlane(cartBody.transform.forward, Vector3.up);

        if (backward.sqrMagnitude > 0.001f) return backward.normalized;

        return -Vector3.forward;
    }

    #endregion

    #region Ghost / Cooldown

    public void SetGhostMode(float duration)
    {
        EnterGhostMode(duration);
    }

    private void EnterGhostMode(float duration)
    {
        float safeDuration = Mathf.Max(0f, duration);
        ghostUntilTime = Mathf.Max(ghostUntilTime, Time.time + safeDuration);

        // Set the visual immediately instead of waiting for Update. This also
        // removes the old one-frame race where collection could still occur
        // before the ghost flag was updated.
        if (cartMaterialManager != null && safeDuration > 0f)
        {
            cartMaterialManager.SetCooldown(safeDuration);
        }
    }

    #endregion

    #region Validation

    private void ValidateTriggerColliders()
    {
        Collider[] colliders = GetComponents<Collider>();

        if (colliders.Length == 0)
        {
            Debug.LogError("[LeadingCartBattleController] No trigger colliders found on this GameObject.", this);
            return;
        }

        foreach (Collider battleCollider in colliders)
        {
            if (!battleCollider.isTrigger)
            {
                Debug.LogError("[LeadingCartBattleController] Every battle detection Collider must have Is Trigger enabled.", battleCollider);
            }
        }
    }

    #endregion
}