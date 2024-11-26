using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SnakeCartManager : MonoBehaviour
{
    [SerializeField] float distanceBetween = 0.2f; // The spawn rate time difference that creates an illusion of distance in between snake bodies
    [SerializeField] List<GameObject> bodyParts = new List<GameObject>();
    [SerializeField] List<GameObject> snakeBody = new List<GameObject>();

    LeadingCartRaycaster LeadingCartRaycaster;

    [Header("Related Events")]
    [SerializeField] GameEvent setupCamera;

    float countUp = 0;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CreateBodyParts();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        ManageSnakeBody();
        SnakeMovement();
    }

    void SnakeMovement()
    {
        if (snakeBody.Count > 1)
        {
            for (int i = 1; i < snakeBody.Count; i++)
            {
                MarkerManager markM = snakeBody[i - 1].GetComponent<MarkerManager>();
                snakeBody[i].transform.position = markM.markerList[0].position;
                snakeBody[i].transform.rotation = markM.markerList[0].rotation;
                markM.markerList.RemoveAt(0);
            }
        }
    }

    void CreateBodyParts()
    {
        if (snakeBody.Count == 0)
        {
            GameObject temp1 = Instantiate(bodyParts[0], transform.position, transform.rotation, transform);
            //temp1.tag = "Player";
            // Ensure MarkerManager is added
            if (!temp1.GetComponent<MarkerManager>())
            {
                temp1.AddComponent<MarkerManager>();
            }

            // Set as collected by the player
            var cartManager = temp1.GetComponent<ChainedCartManager>();
            if (cartManager != null)
            {
                cartManager.CollectByPlayer();
            }

            snakeBody.Add(temp1);
            LeadingCartRaycaster = temp1.GetComponent<LeadingCartRaycaster>();
            setupCamera.Raise();
            bodyParts.RemoveAt(0);
            return;
        }

        MarkerManager markM = snakeBody[snakeBody.Count - 1].GetComponent<MarkerManager>();
        if (countUp == 0)
        {
            markM.ClearMarkerList();
        }
        countUp += Time.deltaTime;
        if (countUp >= distanceBetween)
        {
            GameObject temp = Instantiate(bodyParts[0], markM.markerList[0].position, markM.markerList[0].rotation, transform);
            //temp.tag = "Player";
            // Ensure MarkerManager is added
            if (!temp.GetComponent<MarkerManager>())
            {
                temp.AddComponent<MarkerManager>();
            }

            // Set as collected by the player
            var cartManager = temp.GetComponent<ChainedCartManager>();
            if (cartManager != null)
            {
                cartManager.CollectByPlayer();
            }

            snakeBody.Add(temp);
            bodyParts.RemoveAt(0);
            temp.GetComponent<MarkerManager>().ClearMarkerList();
            countUp = 0;
        }
    }

    void ManageSnakeBody()
    {
        if (bodyParts.Count > 0)
        {
            CreateBodyParts();
        }
        for (int i = 1; i < snakeBody.Count; i++)
        {
            var cartManager = snakeBody[i].GetComponent<ChainedCartManager>();

            if (cartManager == null)
            {
                Debug.LogError("No Chained Cart Manager Component on " + snakeBody[i].name);
                continue;
            }

            // If this cart is no longer collected by the player
            if (!cartManager.isCollectedByPlayer)
            {
                // Detach this cart and all subsequent carts
                for (int j = i; j < snakeBody.Count; j++)
                {
                    snakeBody[j].transform.SetParent(null); // Detach from parent
                    snakeBody[j].GetComponent<ChainedCartManager>().OnDetach();
                }

                // Remove all subsequent carts from the list
                snakeBody.RemoveRange(i, snakeBody.Count - i);

                break; // Exit the loop as we've detached all necessary carts
            }
        }

        // If no carts are left, destroy this script
        if (snakeBody.Count == 0)
        {
            Destroy(this);
        }
    }

    public void AddBodyParts(GameObject addedObj)
    {
        bodyParts.Add(addedObj);

        StartCoroutine(DelayedPlayVFX());
        
    }

    private IEnumerator DelayedPlayVFX()
    {
        // Wait for 0.1 seconds
        yield return new WaitForSeconds(0.12f);

        // Ensure the snakeBody list has elements
        if (snakeBody.Count > 0)
        {
            // Reference the last object in the snakeBody list
            var lastCart = snakeBody[snakeBody.Count - 1];
            var cartManager = lastCart.GetComponent<ChainedCartManager>();

            if (cartManager != null)
            {
                Debug.Log("Playing VFX on: " + lastCart.name);
                cartManager.PlayVFX();
            }
            else
            {
                Debug.LogError("ChainedCartManager missing on: " + lastCart.name);
            }
        }
        else
        {
            Debug.LogError("SnakeBody list is empty. No VFX to play.");
        }
    }

    public void TemporarilyDisableDetaching()
    {
        LeadingCartRaycaster.TemporarilyDisableDetaching();
    }
}