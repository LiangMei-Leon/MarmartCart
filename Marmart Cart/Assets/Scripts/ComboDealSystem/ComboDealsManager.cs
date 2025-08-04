using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ComboDealsManager : MonoBehaviour
{
    [SerializeField] private float generationFrequency = 30f;
    [SerializeField] private float comboDuration = 60f;
    [SerializeField] private float baseReward = 100f;
    [SerializeField] private GameObject comboDealPrefab;
    [SerializeField] private Transform comboUIParent;

    private List<ComboDeal> activeDeals = new();
    private int dealIndex = 0;
    private float nextGenerationTime = 0f;

    [SerializeField]
    private ComboDealUIController uiController;
    void Update()
    {
        if (Time.time >= nextGenerationTime)
        {
            StartCoroutine(DelayedGenerateComboDeal());
            nextGenerationTime = Time.time + generationFrequency;
        }

        // Clean up expired deals
        activeDeals.RemoveAll(d => d.IsExpired || d.IsCompleted);
    }
    private IEnumerator DelayedGenerateComboDeal()
    {
        yield return 1f;

        GenerateComboDeal();
    }
    private void GenerateComboDeal()
    {
        var reqs = new Dictionary<CartRarity, int>
        {
            { CartRarity.Common, Random.Range(0, 3) },
            { CartRarity.Rare, Random.Range(0, 3) },
            { CartRarity.Epic, Random.Range(0, 2) },
            { CartRarity.Legendary, Random.Range(0, 2) }
        };

        // Ensure at least one cart is required
        if (reqs.Values.All(v => v == 0))
            reqs[CartRarity.Common] = 1;

        //old code for single ui controller
        GameObject dealGO = Instantiate(comboDealPrefab, comboUIParent);
        dealGO.GetComponent<ComboDeal>().uiController = uiController;
        ComboDeal deal = dealGO.GetComponent<ComboDeal>();
        deal.Initialize(dealIndex++, baseReward, reqs, comboDuration);
        activeDeals.Add(deal);

    }

    public void SubmitCartToCombos(CartRarity rarity)
    {
        foreach (var deal in activeDeals)
        {
            if (deal.SubmitCart(rarity))
            {
                Debug.Log($"Cart of rarity {rarity} submitted to Deal {deal.DealID}");
                return; // submit to first (oldest) match only
            }
        }
    }

    public List<ComboDeal> GetActiveDeals() => activeDeals;
}
