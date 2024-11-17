//#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

public class Debuggers : MonoBehaviour
{
    [SerializeField] SnakeCartManager snakeCartManager;
    [SerializeField] GameObject attachedCart;

    [SerializeField] GameObject testingCart;
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
        if(Input.GetKeyDown(KeyCode.F1))
            snakeCartManager.AddBodyParts(attachedCart);
        if (Input.GetKeyDown(KeyCode.F2))
        {
            testingCart = snakeCartManager.gameObject.transform.GetChild(1).gameObject;
            testingCart.GetComponent<ChainedCartManager>().OnDetach();
        }
            

    }
}
//#endif