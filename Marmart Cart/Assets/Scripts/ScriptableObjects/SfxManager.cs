using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SFXManager", menuName = "ScriptableObjects/Audio/SFXManager", order = 2)]
public class SfxManager : ScriptableObject
{
    [System.Serializable]
    public class SFXEntry
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)]
        public float volume = 1.0f;
    }

    public List<SFXEntry> sfxEntries = new List<SFXEntry>();
    private AudioSource audioSource;

    public void Initialize(AudioSource source)
    {
        audioSource = source;
    }

    public void PlaySFX(string sfxName)
    {
        SFXEntry entry = sfxEntries.Find(s => s.name == sfxName);
        if (entry != null && audioSource != null)
        {
            audioSource.PlayOneShot(entry.clip, entry.volume);
        }
        else
        {
            Debug.LogWarning($"SFX Name not found or AudioSource not initialized.");
        }
    }

    public void StopSFX(string sfxName)
    {
        SFXEntry entry = sfxEntries.Find(s => s.name == sfxName);
        if (entry != null && audioSource != null)
        {
            audioSource.Stop();
        }
        else
        {
            Debug.LogWarning($"SFX Name not found or AudioSource not initialized.");
        }
    }

    public void StopSFX()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
        else
        {
            Debug.LogWarning("AudioSource not initialized.");
        }
    }
}