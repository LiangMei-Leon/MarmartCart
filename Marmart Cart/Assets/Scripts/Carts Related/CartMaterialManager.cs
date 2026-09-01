using UnityEngine;

/// <summary>
/// Controls the leading cart's visual ghost/cooldown effect.
///
/// Gameplay systems decide when ghost mode starts and how long it lasts.
/// This component only handles the visual material swap and blinking.
/// </summary>
public class CartMaterialManager : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private Renderer cartRenderer;
    [SerializeField] private Material ghostMaterial;

    #endregion

    #region Blink Settings

    [Header("Blink Settings")]
    [Tooltip("Remaining time at which blinking begins accelerating toward Max Blink Rate.")]
    [Min(0.01f)]
    [SerializeField] private float blinkStartTime = 1f;

    [Min(0f)]
    [SerializeField] private float minBlinkRate = 2f;

    [Min(0f)]
    [SerializeField] private float maxBlinkRate = 10f;

    #endregion

    #region Runtime

    private Material[] originalMaterials;
    private Material[] ghostMaterialInstances;

    private bool isGhostVisualActive;
    private float ghostTimeRemaining;
    private float blinkTimer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (cartRenderer == null)
        {
            Debug.LogError("[CartMaterialManager] Cart Renderer is not assigned.", this);
            enabled = false;
            return;
        }

        if (ghostMaterial == null)
        {
            Debug.LogError("[CartMaterialManager] Ghost Material is not assigned.", this);
            enabled = false;
            return;
        }

        CacheMaterials();
    }

    private void Update()
    {
        if (!isGhostVisualActive) return;

        ghostTimeRemaining -= Time.deltaTime;
        blinkTimer += Time.deltaTime;

        UpdateBlinkAlpha();

        if (ghostTimeRemaining <= 0f) EndGhostVisual();
    }

    private void OnDestroy()
    {
        CleanupGhostMaterialInstances();
    }

    #endregion

    #region Initialization

    private void CacheMaterials()
    {
        originalMaterials = cartRenderer.sharedMaterials;

        ghostMaterialInstances = new Material[originalMaterials.Length];

        for (int i = 0; i < ghostMaterialInstances.Length; i++)
        {
            ghostMaterialInstances[i] = new Material(ghostMaterial);
        }
    }

    #endregion

    #region Ghost Visual

    public void SetGhostMode(float duration)
    {
        if (cartRenderer == null || ghostMaterialInstances == null) return;

        ghostTimeRemaining = Mathf.Max(0f, duration);
        blinkTimer = 0f;
        isGhostVisualActive = ghostTimeRemaining > 0f;

        if (!isGhostVisualActive)
        {
            EndGhostVisual();
            return;
        }

        cartRenderer.materials = ghostMaterialInstances;
        SetGhostAlpha(1f);
    }

    private void EndGhostVisual()
    {
        ghostTimeRemaining = 0f;
        blinkTimer = 0f;
        isGhostVisualActive = false;

        if (cartRenderer != null && originalMaterials != null)
        {
            cartRenderer.sharedMaterials = originalMaterials;
        }
    }

    private void UpdateBlinkAlpha()
    {
        if (ghostMaterialInstances == null) return;

        float normalizedRemainingTime = blinkStartTime > 0f
            ? Mathf.Clamp01(ghostTimeRemaining / blinkStartTime)
            : 0f;

        float blinkSpeed = Mathf.Lerp(minBlinkRate, maxBlinkRate, 1f - normalizedRemainingTime);
        float alpha = Mathf.Abs(Mathf.Sin(blinkTimer * blinkSpeed));

        SetGhostAlpha(alpha);
    }

    private void SetGhostAlpha(float alpha)
    {
        foreach (Material materialInstance in ghostMaterialInstances)
        {
            if (materialInstance == null || !materialInstance.HasProperty("_Color")) continue;

            Color color = materialInstance.color;
            color.a = alpha;
            materialInstance.color = color;
        }
    }

    #endregion

    #region Cleanup

    private void CleanupGhostMaterialInstances()
    {
        if (ghostMaterialInstances == null) return;

        foreach (Material materialInstance in ghostMaterialInstances)
        {
            if (materialInstance != null) Destroy(materialInstance);
        }

        ghostMaterialInstances = null;
    }

    #endregion
}