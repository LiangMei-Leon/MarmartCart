using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class AimDirectionVisualizer : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool isForPlayer1 = true;
    [SerializeField] private CartControlScript cartControllerP1;
    [SerializeField] private CartControlScript cartControllerP2;
    [SerializeField] private float lineLength = 5f;
    [SerializeField] private Transform playerTransform;

    private LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }
    private void Start()
    {
        Invoke(nameof(RegisterPlayer), 2f);
    }
    public void RegisterPlayer()
    {
        GameObject playerRef;
        if (isForPlayer1)
            playerRef = GameObject.FindGameObjectWithTag("Player1");
        else
            playerRef = GameObject.FindGameObjectWithTag("Player2");

        playerTransform = isForPlayer1 ? playerRef?.transform : playerRef?.transform;
        if (playerTransform == null)
        {
            Debug.LogError("Player not found for AimDirectionVisualizer.");
            return;
        }
        cartControllerP1 = playerTransform.GetComponentInChildren<CartControlScript>();
        cartControllerP2 = playerTransform.GetComponentInChildren<CartControlScript>();
    }
    void Update()
    {
        if (isForPlayer1)
        {
            if (cartControllerP1 == null || !cartControllerP1.enabled) return;

            if (cartControllerP1.GetCanAim())
            {
                Vector3 start = playerTransform.position;
                Vector3 end = start + cartControllerP1.AimDirection * lineLength;
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, start);
                lineRenderer.SetPosition(1, end);
            }
            else
            {
                lineRenderer.enabled = false;
            }
        }
        else
        {
            if (cartControllerP2 == null || !cartControllerP2.enabled) return;

            if (cartControllerP2.GetCanAim())
            {
                Vector3 start = playerTransform.position;
                Vector3 end = start + cartControllerP2.AimDirection * lineLength;
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, start);
                lineRenderer.SetPosition(1, end);
            }
            else
            {
                lineRenderer.enabled = false;
            }
        }
    }
}