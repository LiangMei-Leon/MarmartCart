using UnityEngine;

public class PitIndictor : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool isForPlayer1 = true;
    [SerializeField] private float offsetDistance = 2f;

    [SerializeField] private Transform player;
    private Transform pitT;
    [SerializeField] private GameObject pit;
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
            Debug.Log("Null");
            return;
        }

        if (pit != null)
        {
            GetComponent<MeshRenderer>().enabled = true;
            pitT = pit.transform;
            // Position ball in direction of dino
            Vector3 direction = pitT.position - player.position;
            direction.y = 0f;
            direction.Normalize();

            transform.position = player.position + direction * offsetDistance + Vector3.up * -1f;
        }
        else
        {
            pitT = null;
            GetComponent<MeshRenderer>().enabled = false;
        }
    }
}
