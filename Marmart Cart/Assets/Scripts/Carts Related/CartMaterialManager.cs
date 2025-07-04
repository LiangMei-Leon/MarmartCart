using UnityEngine;

public class CartMaterialManager : MonoBehaviour
{
    [Header("Material References")]
    [SerializeField] private Material ghostMaterial; // Assign a transparent blinking material
    private Material[] originalMaterials;
    private Material[] ghostInstances;

    [Header("Blink Settings")]
    [SerializeField] private float blinkStartTime = 1f;
    [SerializeField] private float minBlinkRate = 2f;
    [SerializeField] private float maxBlinkRate = 10f;

    [SerializeField] private GameObject cartModel;
    private Renderer cartRenderer;
    private bool inGhostMode = false;
    private float inGhostModeDuration;
    private float blinkTimer = 0f;

    void Awake()
    {
        cartRenderer = cartModel.GetComponent<Renderer>();
        originalMaterials = cartRenderer.materials;

        // Create instances of ghostMaterial to animate independently
        ghostInstances = new Material[originalMaterials.Length];
        for (int i = 0; i < ghostInstances.Length; i++)
        {
            ghostInstances[i] = new Material(ghostMaterial); // clone instance
        }
    }

    public void SetCooldown(float duration)
    {
        inGhostModeDuration = duration;
        inGhostMode = true;
        blinkTimer = 0f;
        // Swap to ghost materials
        cartRenderer.materials = ghostInstances;
    }

    void Update()
    {
        if (!inGhostMode) return;

        inGhostModeDuration -= Time.deltaTime;

        blinkTimer += Time.deltaTime;
        // Animate alpha blink
        AnimateBlinkAlpha(inGhostModeDuration);

        if (inGhostModeDuration <= 0f)
        {
            // Restore original materials
            cartRenderer.materials = originalMaterials;
            inGhostMode = false;
        }
    }

    void AnimateBlinkAlpha(float remainingTime)
    {
        float blinkSpeed = Mathf.Lerp(minBlinkRate, maxBlinkRate, 1f - Mathf.Clamp01(remainingTime / blinkStartTime));
        float alpha = Mathf.Abs(Mathf.Sin(blinkTimer * blinkSpeed));

        foreach (var mat in ghostInstances)
        {
            if (mat.HasProperty("_Color"))
            {
                Color color = mat.color;
                color.a = alpha;
                mat.color = color;
            }
        }
    }
}
