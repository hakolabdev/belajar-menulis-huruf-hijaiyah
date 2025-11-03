using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance;

    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    public AudioClip audioButtonPositive;
    public AudioClip audioButtonNegative;
    public AudioClip audioVictory;
    [SerializeField] private int poolSize = 10;

    private List<AudioSource> audioPool;
    private int currentIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioPool = new List<AudioSource>();
        for (int i = 0; i < poolSize; i++)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.outputAudioMixerGroup = sfxMixerGroup;
            a.playOnAwake = false;
            audioPool.Add(a);
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;

        AudioSource source = audioPool[currentIndex];
        source.PlayOneShot(clip);
        currentIndex = (currentIndex + 1) % poolSize;
    }

    public void PlayRandomSFX(params AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;

        // pilih acak 1 dari array
        int index = Random.Range(0, clips.Length);
        AudioClip clip = clips[index];

        AudioSource source = audioPool[currentIndex];
        source.PlayOneShot(clip);
        currentIndex = (currentIndex + 1) % poolSize;
    }
}
