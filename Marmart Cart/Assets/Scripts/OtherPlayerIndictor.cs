using UnityEngine;

public class OtherPlayerIndictor : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool isForPlayer1 = true;
    [SerializeField] private float offsetDistance = 2f;

    [SerializeField] private Transform thisPlayer;
    [SerializeField] private Transform otherPlayer;
    private Transform pitT;
    [SerializeField] private GameObject pit;
    private void Start()
    {
        Invoke(nameof(RegisterPlayer), 2f);
    }
    public void RegisterPlayer()
    {
        if (isForPlayer1)
        {
            thisPlayer = GameObject.FindGameObjectWithTag("Player1").transform;
            otherPlayer = GameObject.FindGameObjectWithTag("Player2").transform;
        }
        else
        {
            thisPlayer = GameObject.FindGameObjectWithTag("Player2").transform;
            otherPlayer = GameObject.FindGameObjectWithTag("Player1").transform;
        }
    }
    private void Update()
    {
        if (otherPlayer == null || thisPlayer == null)
        {
            return;
        }
        GetComponent<MeshRenderer>().enabled = true;
        Vector3 direction = otherPlayer.position - thisPlayer.position;
        direction.y = 0f;
        direction.Normalize();
        transform.position = thisPlayer.position + direction * offsetDistance + Vector3.up * -1f;

    }
}
