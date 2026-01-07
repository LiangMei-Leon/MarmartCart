using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class TutorialOneSlotSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private GameObject prefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform spawnParent; // optional

    [Header("Respawn Feel")]
    [Tooltip("How long after players leave the zone before a new item can respawn.")]
    [SerializeField] private float respawnDelay = 1.5f;

    [Tooltip("If the spawned item moved this far from the spawn point, we consider it 'taken'.")]
    [SerializeField] private float takenDistance = 1.0f;

    [Header("Area Clear Rules")]
    [Tooltip("Only respawn if the trigger area is clear (prevents respawning on top of someone stuck there).")]
    [SerializeField] private bool requireZoneClearToRespawn = true;

    [Tooltip("Which layers count as 'blocking' the respawn (players, carts, etc).")]
    [SerializeField] private LayerMask zoneBlockMask;

    [Tooltip("How often to re-check zone clear while waiting to respawn.")]
    [SerializeField] private float clearCheckInterval = 0.15f;

    private BoxCollider _zone;
    private GameObject _current;
    private int _playersInside = 0;
    private Coroutine _respawnRoutine;
    private ISpawnerHoldable _holdable;
    private void Reset()
    {
        _zone = GetComponent<BoxCollider>();
        _zone.isTrigger = true;
    }

    private void Awake()
    {
        _zone = GetComponent<BoxCollider>();
        _zone.isTrigger = true;

        if (!spawnPoint) spawnPoint = transform; // fallback
    }

    private void Start()
    {
        SpawnNow();
    }

    private void Update()
    {
        // If we have an item and it got moved away/destroyed -> treat as taken.
        if (_current == null) return;

        // If item is inactive/destroyed, clear reference
        if (!_current.activeInHierarchy)
        {
            _holdable?.OnSpawnerHoldEnd();
            _holdable = null;
            _current = null;
            return;
        }

        // If item moved far away from spawn, it's "taken"
        float d = Vector3.Distance(_current.transform.position, spawnPoint.position);
        if (d >= takenDistance)
        {
            _holdable?.OnSpawnerHoldEnd();
            _holdable = null;
            _current = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Any player can use it. We don't lock to a player.
        // Easiest filter: tags "Player1/Player2/Player3/Player4" OR put players on a Player layer.
        if (IsPlayer(other))
        {
            _playersInside++;
            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);
                _respawnRoutine = null;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            _playersInside = Mathf.Max(0, _playersInside - 1);

            // Start respawn countdown ONLY after last player leaves
            if (_playersInside == 0)
                TryStartRespawn();
        }
    }

    private bool IsPlayer(Collider other)
    {
        // Tag approach (matches your current setup)
        return other.CompareTag("Player1") || other.CompareTag("Player2");
            // || other.CompareTag("Player3") || other.CompareTag("Player4");
    }

    private void TryStartRespawn()
    {
        // Only respawn if slot is empty (taken/destroyed)
        if (_current != null) return;

        if (_respawnRoutine != null) return;
        _respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        float t = 0f;

        // Wait delay, but cancel if a player comes back in
        while (t < respawnDelay)
        {
            if (_playersInside > 0) { _respawnRoutine = null; yield break; }
            t += Time.deltaTime;
            yield return null;
        }

        // Optional: only respawn if area is clear (prevents stuck-respawn)
        if (requireZoneClearToRespawn)
        {
            while (!IsZoneClear())
            {
                if (_playersInside > 0) { _respawnRoutine = null; yield break; }
                yield return new WaitForSeconds(clearCheckInterval);
            }
        }

        SpawnNow();
        _respawnRoutine = null;
    }

    private bool IsZoneClear()
    {
        // Use the box collider’s bounds for an overlap check
        var center = _zone.bounds.center;
        var halfExtents = _zone.bounds.extents;

        // OverlapBox uses world rotation; BoxCollider bounds already axis-aligned.
        // Good enough for tutorial zones. If you rotate zones heavily, tell me and I’ll switch to BoxCollider local-space overlap.
        var hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, zoneBlockMask, QueryTriggerInteraction.Ignore);

        return hits == null || hits.Length == 0;
    }

    public void SpawnNow()
    {
        if (!prefab || _current != null) return;

        _current = Instantiate(prefab, spawnPoint.position, prefab.transform.rotation, spawnParent);

        _holdable = _current.GetComponentInChildren<ISpawnerHoldable>();
        _holdable?.OnSpawnerHoldStart();
    }

    // Handy for resets / debugging
    public void ForceClearSlot()
    {
        _holdable?.OnSpawnerHoldEnd();
        _holdable = null;

        if (_current != null) Destroy(_current);
        _current = null;
    }
}