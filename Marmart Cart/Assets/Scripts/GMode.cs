using UnityEngine;

public enum GameMode { duel2P, freeForAll4P, teamBattle4P }
public class GMode : MonoBehaviour
{
    public static GMode Instance { get; private set; }

    public GameMode CurrentMode = GameMode.duel2P;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject); // Optional, if needed across scenes
    }
    public bool IsTwoPlayer => CurrentMode == GameMode.duel2P;
    public int PlayerCount()
    {
        return CurrentMode switch
        {
            GameMode.duel2P => 2,
            GameMode.freeForAll4P => 4,
            GameMode.teamBattle4P => 4,
            _ => 0
        };
    }
    public bool IsFreeForAll => CurrentMode == GameMode.freeForAll4P;
    public bool IsTeamBattle => CurrentMode == GameMode.teamBattle4P;
}
