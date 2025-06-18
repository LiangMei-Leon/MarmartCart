using UnityEngine;

public class DinoTile : MonoBehaviour
{
    public enum PlayerOwner { Player1, Player2 }
    [SerializeField] private PlayerOwner owner;

    [Header("Status")]
    [SerializeField] private bool isCovered = false;

    public bool IsCovered => isCovered;
    public PlayerOwner Owner => owner;
    private GameObject coveringObstacle;
    private bool obstacleActive = false;

    [Header("Materials")]
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material coveredMaterial;

    private Renderer tileRenderer;
    void Awake()
    {
        tileRenderer = this.transform.GetChild(0).GetComponent<Renderer>();
        if (tileRenderer && defaultMaterial)
            tileRenderer.material = defaultMaterial;
    }
    private void OnTriggerStay(Collider other)
    {
        // Match player/carts by tag
        if (owner == PlayerOwner.Player1 && (other.CompareTag("Player1")))
        {
            isCovered = true;
            SetCoveredVisual(true);
        }
        else if (owner == PlayerOwner.Player2 && other.CompareTag("Player2"))
        {
            isCovered = true;
            SetCoveredVisual(true);
        }

        if (other.CompareTag("Obstacles") || other.CompareTag("Walls"))
        {
            coveringObstacle = other.gameObject;
            obstacleActive = true;

            isCovered = true;
            SetCoveredVisual(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (owner == PlayerOwner.Player1 && other.CompareTag("Player1"))
        {
            isCovered = false;
            SetCoveredVisual(false);
        }
        else if (owner == PlayerOwner.Player2 && other.CompareTag("Player2"))
        {
            isCovered = false;
            SetCoveredVisual(false);
        }

    }

    private void OnTriggerEnter(Collider other)
    {
        // Destroy if it overlaps a shelf (obstacle)
//         if (other.CompareTag("Obstacles"))
//         {
//             //DestroyTile();
//             isCovered = true;
//             SetCoveredVisual(true);
//             return;
//         }

        // Destroy if it hits a tile of the opposite player
        DinoTile otherTile = other.GetComponent<DinoTile>();
        if (otherTile != null && otherTile.owner != this.owner)
        {
            DestroyTile();
        }
    }

    private void SetCoveredVisual(bool covered)
    {
        if (tileRenderer)
        {
            tileRenderer.material = covered ? coveredMaterial : defaultMaterial;
        }
    }
    private void Update()
    {
        if (obstacleActive && coveringObstacle == null)
        {
            isCovered = false;
            SetCoveredVisual(false);
            obstacleActive = false; // stop checking
        }
    }
    public void DestroyTile()
    {
        Destroy(gameObject);
    }
}