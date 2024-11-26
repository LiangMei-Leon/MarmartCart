using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameObjectPool", menuName = "Scriptable Objects/GameObjectPool")]
public class GameObjectPool : ScriptableObject
{
    public GameObject prefab;

    public int amount;

    private Queue<GameObject> spawnedObjs;

    private Transform parent;

    public void SpawnPool()
    {
        if (spawnedObjs == null || spawnedObjs.Count == 0)
        {
            spawnedObjs = new Queue<GameObject>();
        }

        if (spawnedObjs != null)
        {
            // Debug.Log("Pool Existed!");
            while (spawnedObjs.Count > 0)
            {
                GameObject obj = spawnedObjs.Dequeue();
                if (obj != null)
                {
                    Destroy(obj);
                }
            }

            // Debug.Log(spawnedObjs.Count);

            parent = new GameObject(name).transform;

            while (spawnedObjs.Count < amount)
            {
                GameObject obj = Instantiate(prefab, parent);
                obj.SetActive(false);
                spawnedObjs.Enqueue(obj);
            }
            return;
        }

        if (!parent)
        {
            parent = new GameObject(name).transform;
        }

        while (spawnedObjs.Count < amount)
        {
            GameObject obj = Instantiate(prefab, parent);
            obj.SetActive(false);
            spawnedObjs.Enqueue(obj);
        }
    }
    public GameObject GetGameObject()
    {
        if (spawnedObjs == null || spawnedObjs.Count == 0)
        {
            SpawnPool();
            Debug.LogWarning($"{name} spawned mid-game, consider spawning it at the start of the game");
        }

        GameObject obj;
        if (spawnedObjs.Count > 0)
        {
            obj = spawnedObjs.Dequeue();
        }
        else
        {
            obj = Instantiate(prefab, parent);
        }

        // spawnedObjs.Enqueue(obj);
        obj.SetActive(false);
        obj.SetActive(true);

        return obj;
    }

    public GameObject GetGameObject(Vector3 newPos, Quaternion newRot)
    {
        if (spawnedObjs == null || spawnedObjs.Count == 0)
        {
            SpawnPool();
            Debug.LogWarning($"{name} spawned mid-game, consider spawning it at the start of the game");
        }

        GameObject obj = spawnedObjs.Dequeue();

        spawnedObjs.Enqueue(obj);
        obj.SetActive(false);
        obj.transform.position = newPos;
        obj.transform.rotation = newRot;
        obj.SetActive(true);

        return obj;
    }

    public void ReturnObject(GameObject obj)
    {
        obj.SetActive(false);
        obj.transform.SetParent(parent); // Reset parent to pool
        spawnedObjs.Enqueue(obj);
    }
}