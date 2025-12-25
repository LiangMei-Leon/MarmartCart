using UnityEngine;

[DisallowMultipleComponent]
public class TutorialGate : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool isOpen = false;

    [Header("Collision")]
    [SerializeField] private BoxCollider gateCollider; // BoxCollider or any collider

    [Header("Visuals")]
    [SerializeField] private Renderer gateRenderer;
    [SerializeField] private Material lockedRed;
    [SerializeField] private Material unlockedGreen;

    private void Reset()
    {
        gateCollider = GetComponent<BoxCollider>();
        gateRenderer = GetComponentInChildren<Renderer>();
    }

    private void Awake()
    {
        if (!gateCollider) gateCollider = GetComponent<BoxCollider>();
        if (!gateRenderer) gateRenderer = GetComponentInChildren<Renderer>();

        ApplyState();
    }

    // === Public API (this is all missions need) ===

    public void OpenGate()
    {
        isOpen = true;
        ApplyState();
    }

    public void CloseGate()
    {
        isOpen = false;
        ApplyState();
    }

    public void SetGate(bool open)
    {
        isOpen = open;
        ApplyState();
    }

    // === Internal ===

    private void ApplyState()
    {
        if (gateCollider)
            gateCollider.enabled = !isOpen; // closed = blocking, open = passable

        if (!gateRenderer) return;

        if (isOpen && unlockedGreen)
            gateRenderer.sharedMaterial = unlockedGreen;
        else if (!isOpen && lockedRed)
            gateRenderer.sharedMaterial = lockedRed;
    }
}