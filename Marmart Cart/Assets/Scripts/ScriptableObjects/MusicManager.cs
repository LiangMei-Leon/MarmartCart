using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "MusicManager", menuName = "ScriptableObjects/Audio/MusicManager", order = 1)]
public class MusicManager : ScriptableObject
{
    [System.Serializable]
    public class MusicEntry
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)]
        public float volume = 1.0f;
        public bool loop = true;
    }

    public List<MusicEntry> musicEntries = new List<MusicEntry>();
    private AudioSource audioSource;

    public void Initialize(AudioSource source)
    {
        audioSource = source;
    }

    public void PlayMusic(string musicName)
    {
        MusicEntry entry = musicEntries.Find(m => m.name == musicName);
        if (entry != null && audioSource != null)
        {
            audioSource.clip = entry.clip;
            audioSource.volume = entry.volume;
            audioSource.loop = entry.loop;
            audioSource.Play();
        }
    }

    public void StopMusic()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        else
        {
            Debug.LogWarning($"SFX Name not found or AudioSource not initialized.");
        }
    }
}