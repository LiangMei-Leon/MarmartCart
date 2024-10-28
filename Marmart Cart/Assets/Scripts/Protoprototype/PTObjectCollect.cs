using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PTObjectCollect : MonoBehaviour
{
    public List<GameObject> collectibles; // List of collectible objects
    private HashSet<GameObject> collectedObjects = new HashSet<GameObject>(); // Track collected objects
    public TextMeshProUGUI itemsLeftText; // UI Text to display items left

    private void Start()
    {
        UpdateItemsLeftDisplay(); // Initial display update
    }
    public void MarkAsCollected(GameObject collectible)
    {
        if (collectibles.Contains(collectible) && !collectedObjects.Contains(collectible))
        {
            collectedObjects.Add(collectible); // Mark as collected
            UpdateItemsLeftDisplay();
        }
    }

    public bool AllCollected()
    {
        return collectedObjects.Count >= collectibles.Count; // Check if all are collected
    }

    private void UpdateItemsLeftDisplay()
    {
        int itemsLeft = collectibles.Count - collectedObjects.Count;
        itemsLeftText.text = "Items Left: " + itemsLeft + "/" + collectibles.Count;
    }
}
