using UnityEngine;

public class PitIndictor : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool isForPlayer1 = true;
    [SerializeField] private float offsetDistance = 2f;

    [SerializeField] private Transform player;
    private Transform pitT;
    [SerializeField] private GameObject pit;
    [Header("Rotation")]
    [Tooltip("Extra rotation around Y to fine-tune arrow facing")]
    [SerializeField] private float yRotationOffset = 0f;

    [Tooltip("Base X rotation needed for the mesh to look correct (your -90)")]
    [SerializeField] private float baseXRotation = -90f;
    private void Start()
    {
        pit = GameObject.FindGameObjectWithTag("CheckOutStation");
        Invoke(nameof(RegisterPlayer), 2f);
    }
    public void RegisterPlayer()
    {
        GameObject playerRef;
        if (isForPlayer1)
            playerRef = GameObject.FindGameObjectWithTag("Player1");
        else
            playerRef = GameObject.FindGameObjectWithTag("Player2");

        player = isForPlayer1 ? playerRef?.transform : playerRef?.transform;
    }
    private void Update()
    {
        if (pit == null || player == null)
        {
            // Optional: hide if data missing
            GetComponent<MeshRenderer>().enabled = false;
            return;
        }

        pitT = pit.transform;
        GetComponent<MeshRenderer>().enabled = true;

        // --- POSITION: same as your previous approach ---
        Vector3 direction = pitT.position - player.position;
        direction.y = 0f;              // ignore height
        if (direction.sqrMagnitude < 0.0001f)
            return;                    // avoid NaN when very close

        direction.Normalize();
        transform.position = player.position + direction * offsetDistance + Vector3.up * -1f;

        // --- ROTATION: point arrow head toward pit, yaw only + offset ---

        // Angle on XZ plane, using Z as forward
        float angleY = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        // Add manual tweak
        angleY += yRotationOffset;

        // Final rotation: your mesh needs -90 on X, and we rotate on Y
        transform.rotation = Quaternion.Euler(baseXRotation, angleY, 0f);
    }
}
