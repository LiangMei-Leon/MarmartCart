using UnityEngine;

public class DinoTile : MonoBehaviour
{
    public enum PlayerOwner { Player1, Player2 }
    [SerializeField] private PlayerOwner owner;

    [Header("Status")]
    [SerializeField] private bool isCovered = false;

    public bool IsCovered => isCovered;
    public PlayerOwner Owner => owner;

    private void OnTriggerStay(Collider other)
    {
        // Match player/carts by tag
        if (owner == PlayerOwner.Player1 && (other.CompareTag("Player1")))
            isCovered = true;
        else if (owner == PlayerOwner.Player2 && other.CompareTag("Player2"))
            isCovered = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (owner == PlayerOwner.Player1 && other.CompareTag("Player1"))
            isCovered = false;
        else if (owner == PlayerOwner.Player2 && other.CompareTag("Player2"))
            isCovered = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Destroy if it overlaps a shelf (obstacle)
        if (other.CompareTag("Obstacles"))
        {
            DestroyTile();
            return;
        }

        // Destroy if it hits a tile of the opposite player
        DinoTile otherTile = other.GetComponent<DinoTile>();
        if (otherTile != null && otherTile.owner != this.owner)
        {
            DestroyTile();
        }
    }

    public void DestroyTile()
    {
        Destroy(gameObject);
    }
}