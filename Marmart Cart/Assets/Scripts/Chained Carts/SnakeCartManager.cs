using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SnakeCartManager : MonoBehaviour
{
    [SerializeField] float distanceBetween = 0.2f; // The spawn rate time difference that creates an illusion of distance in between snake bodies
    [SerializeField] List<GameObject> bodyParts = new List<GameObject>();
    List<GameObject> snakeBody = new List<GameObject>();

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
        if(bodyParts.Count > 0)
        {
            CreateBodyParts();
        }
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
        if(snakeBody.Count == 0)
        {
            GameObject temp1 = Instantiate(bodyParts[0], transform.position, transform.rotation, transform);
            if(!temp1.GetComponent<MarkerManager>())
            {
                temp1.AddComponent<MarkerManager>();
            }
            snakeBody.Add(temp1);
            setupCamera.Raise();
            bodyParts.RemoveAt(0);
        }
        MarkerManager markM = snakeBody[snakeBody.Count - 1].GetComponent<MarkerManager>();
        if(countUp == 0)
        {
            markM.ClearMarkerList();
        }
        countUp += Time.deltaTime;
        if (countUp >= distanceBetween)
        {
            GameObject temp = Instantiate(bodyParts[0], markM.markerList[0].position, markM.markerList[0].rotation, transform);
            if(!temp.GetComponent<MarkerManager>())
            {
                temp.AddComponent<MarkerManager>();
            }
            snakeBody.Add(temp);
            bodyParts.RemoveAt(0);
            temp.GetComponent<MarkerManager>().ClearMarkerList();
            countUp = 0;
        }
    }
}
