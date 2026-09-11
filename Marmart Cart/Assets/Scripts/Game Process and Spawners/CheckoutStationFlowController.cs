using UnityEngine;

public enum CheckoutStationId
{
    North,
    East,
    South,
    West
}

/// <summary>
/// Match-flow wrapper around one existing checkout station.
///
/// Closing a station prevents NEW entry. If a player is already checking out,
/// their committed checkout session is allowed to finish normally.
/// </summary>
[DisallowMultipleComponent]
public class CheckoutStationFlowController : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private CheckoutStationId stationId = CheckoutStationId.North;

    [Header("Checkout")]
    [SerializeField] private CheckOutManager checkOutManager;

    [Header("Optional Prototype State Visuals")]
    [SerializeField] private GameObject telegraphIndicator;
    [SerializeField] private GameObject openIndicator;
    [SerializeField] private GameObject closedIndicator;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isOpen;
    [SerializeField] private bool isTelegraphing;

    public CheckoutStationId StationId => stationId;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (checkOutManager == null) checkOutManager = GetComponent<CheckOutManager>();
    }

    public void SetTelegraphing(bool telegraphing)
    {
        isTelegraphing = telegraphing;

        if (telegraphing)
        {
            SetOpen(false);
            if (telegraphIndicator != null) telegraphIndicator.SetActive(true);
            return;
        }

        if (telegraphIndicator != null) telegraphIndicator.SetActive(false);
    }

    public void SetOpen(bool open)
    {
        isOpen = open;
        isTelegraphing = false;

        if (checkOutManager != null)
        {
            if (open) checkOutManager.EnableStation();
            else checkOutManager.DisableStation();
        }

        if (telegraphIndicator != null) telegraphIndicator.SetActive(false);
        if (openIndicator != null) openIndicator.SetActive(open);
        if (closedIndicator != null) closedIndicator.SetActive(!open);
    }
}
