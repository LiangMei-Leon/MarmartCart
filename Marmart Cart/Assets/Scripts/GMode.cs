using UnityEngine;

public enum GameMode { Coop, Competitive }
public class GMode : MonoBehaviour
{
    public static GMode Instance { get; private set; }

    public GameMode CurrentMode = GameMode.Coop;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        DontDestroyOnLoad(gameObject); // Optional, if needed across scenes
    }

    public bool IsCompetitive => CurrentMode == GameMode.Competitive;
    public bool IsCoop => CurrentMode == GameMode.Coop;
}
