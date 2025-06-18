using UnityEngine;

public class DinoIndictor : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool isForPlayer1 = true;
    [SerializeField] private float offsetDistance = 2f;

    private Transform player;
    private Transform dino;
    private DinoGenerationScript dinoSpawner;

    private void Start()
    {
        var p1 = GameObject.FindGameObjectWithTag("Player1");
        var p2 = GameObject.FindGameObjectWithTag("Player2");

        player = isForPlayer1 ? p1?.transform : p2?.transform;
        dinoSpawner = FindFirstObjectByType<DinoGenerationScript>();
    }

    private void Update()
    {
        if (dinoSpawner == null || player == null) return;

        var dinoObj = dinoSpawner.GetExistingDino(); // safer than FindFirstObjectByType
        if (dinoObj != null)
        {
            GetComponent<MeshRenderer>().enabled = true;
            dino = dinoObj.transform;
            // Position ball in direction of dino
            Vector3 direction = dino.position - player.position;
            direction.y = 0f;
            direction.Normalize();

            transform.position = player.position + direction * 2f + Vector3.up * 0.5f;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            dino = null;
            GetComponent<MeshRenderer>().enabled = false;
        }
    }
}
