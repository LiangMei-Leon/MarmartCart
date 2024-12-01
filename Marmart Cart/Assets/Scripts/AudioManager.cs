using UnityEngine;
using UnityEngine.VFX;

public class AudioManager : MonoBehaviour
{
    public MusicManager musicManager;
    public AudioSource sourceMusic;
    public SfxManager sfxManager;
    public AudioSource sourceSfx;

    private void Awake()
    {
        musicManager.Initialize(sourceMusic);

        sfxManager.Initialize(sourceSfx);
    }
}
