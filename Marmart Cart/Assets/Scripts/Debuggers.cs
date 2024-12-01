
using System.Collections.Generic;
using UnityEngine;

public class Debuggers : MonoBehaviour
{
    [SerializeField] SnakeCartManager snakeCartManager;
    [SerializeField] GameObject attachedCart;
    [SerializeField] GameEvent attachedCartEvent;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        ChainOfCartsDebug();
    }

    void ChainOfCartsDebug()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            attachedCartEvent.Raise();
            

    }
}