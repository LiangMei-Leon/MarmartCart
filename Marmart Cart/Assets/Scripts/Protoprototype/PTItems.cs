using UnityEngine;
using TMPro;

public class PTItems : MonoBehaviour
{

    public bool isRareItem = false;
    public float rareScore = 100f;
    public float normalScore = 20f;
    public float rareDisappearTime = 15f;
    public float normalDisappearTime = 30f;

    private float elapsedTime;
    private TextMeshPro timerText;

    [SerializeField] PTTimer scoresystem;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        scoresystem = GameObject.FindGameObjectWithTag("PTscore").GetComponent<PTTimer>();
        timerText = this.transform.GetChild(0).gameObject.GetComponent<TextMeshPro>();
    }

    // Update is called once per frame
    void Update()
    {
        elapsedTime += Time.deltaTime;
        
        if (isRareItem)
        {
            timerText.text = (rareDisappearTime - elapsedTime).ToString("F0");
            if (elapsedTime >= rareDisappearTime)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            timerText.text = (normalDisappearTime - elapsedTime).ToString("F0");
            if (elapsedTime >= normalDisappearTime)
            {
                Destroy(gameObject);
            }
        }

    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            if(isRareItem)
            {
                scoresystem.AddScore(rareScore);
                // Debug.Log("collect" + rareScore);
                Destroy(gameObject);
            }
            else
            {
                scoresystem.AddScore(normalScore);
                // Debug.Log("collect" + normalScore);
                Destroy(gameObject);
            }
        }
    }
}
