using System.Collections.Generic;
using UnityEngine;

public class DinoTileManager : MonoBehaviour
{
    [Header("Tile Prefabs")]
    public GameObject p1TilePrefab;
    public GameObject p2TilePrefab;

    [Header("Path Generation Settings")]
    [SerializeField] private int pathLength = 10;
    [SerializeField] private int turnCount = 3;
    [SerializeField] private float tileSpacing = 3.25f;
    [SerializeField] private float minStartDistance = 3f;
    [SerializeField] private float maxStartDistance = 6f;

    [Header("References")]
    public Transform dinoCenter;

    [Header("Manual Tile References")]
    public List<DinoTile> player1Tiles = new List<DinoTile>();
    public List<DinoTile> player2Tiles = new List<DinoTile>();

    private bool pathCompleted = false;

    [SerializeField] private DinoBehaviour dinoBehaviour;

    void Update()
    {
        if (!pathCompleted && AllTilesCovered())
        {
            pathCompleted = true;
            HandleFullCoverage();
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            GeneratePath();
        }
    }
    public void SetPathLength(int length)
    {
        pathLength = length;
    }

    public void SetTurnCounts(int count)
    {
        turnCount = count;
    }

    public void GeneratePath()
    {
        ClearExistingTiles();

        Vector3 currentPos = GetRandomStartPosition();
        Vector3 currentDir = GetRandomDirection();
        int stepBeforeTurn = Mathf.Max(1, pathLength / Mathf.Max(1, turnCount));

        for (int i = 0; i < pathLength; i++)
        {
            // Instantiate red tile
            GameObject p1Tile = Instantiate(p1TilePrefab, currentPos, Quaternion.identity);
            DinoTile p1TileComponent = p1Tile.GetComponent<DinoTile>();
            if (p1TileComponent != null) player1Tiles.Add(p1TileComponent);

            // Instantiate mirrored blue tile
            Vector3 mirroredPos = GetMirroredPosition(currentPos);
            GameObject p2Tile = Instantiate(p2TilePrefab, mirroredPos, Quaternion.identity);
            DinoTile p2TileComponent = p2Tile.GetComponent<DinoTile>();
            if (p2TileComponent != null) player2Tiles.Add(p2TileComponent);

            // Move forward
            currentPos += currentDir * tileSpacing;

            // Change direction at turn intervals
            if (i > 0 && i % stepBeforeTurn == 0)
            {
                currentDir = GetNextDirection(currentDir);
            }
        }

        // Clean up any tiles that destroyed themselves due to overlap
        player1Tiles.RemoveAll(t => t == null);
        player2Tiles.RemoveAll(t => t == null);
    }

    private bool AllTilesCovered()
    {
        foreach (var tile in player1Tiles)
        {
            if (tile == null) continue;
            if (!tile.IsCovered)
                return false;
        }

        foreach (var tile in player2Tiles)
        {
            if (tile == null) continue;
            if (!tile.IsCovered)
                return false;
        }

        return true;
    }

    private void HandleFullCoverage()
    {
        Debug.Log("All tiles covered! Dino can now take damage.");
        dinoBehaviour.TakeDamage();
    }

    private void ClearExistingTiles()
    {
        foreach (var tile in player1Tiles)
            if (tile) Destroy(tile.gameObject);

        foreach (var tile in player2Tiles)
            if (tile) Destroy(tile.gameObject);

        player1Tiles.Clear();
        player2Tiles.Clear();
        pathCompleted = false;
    }

    private Vector3 GetRandomStartPosition()
    {
        Vector2 randomOffset = Random.insideUnitCircle.normalized * Random.Range(minStartDistance, maxStartDistance);
        Vector3 offset3D = new Vector3(randomOffset.x, -dinoCenter.position.y, randomOffset.y);
        return RoundToGrid(dinoCenter.position + offset3D);
    }

    private Vector3 GetMirroredPosition(Vector3 original)
    {
        Vector3 center = dinoCenter.position;
        Vector3 offset = original - center;
        Vector3 result = center - offset;
        return new Vector3(result.x, 0f, result.z);
    }

    private Vector3 GetRandomDirection()
    {
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        return directions[Random.Range(0, directions.Length)];
    }

    private Vector3 GetNextDirection(Vector3 previousDirection)
    {
        List<Vector3> options = new();

        if (previousDirection == Vector3.forward || previousDirection == Vector3.back)
        {
            options.Add(Vector3.left);
            options.Add(Vector3.right);
        }
        else
        {
            options.Add(Vector3.forward);
            options.Add(Vector3.back);
        }

        return options[Random.Range(0, options.Count)];
    }

    private Vector3 RoundToGrid(Vector3 pos)
    {
        return new Vector3(
            Mathf.Round(pos.x),
            pos.y,
            Mathf.Round(pos.z)
        );
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, minStartDistance);
        Gizmos.DrawWireSphere(transform.position, maxStartDistance);
    }
}
