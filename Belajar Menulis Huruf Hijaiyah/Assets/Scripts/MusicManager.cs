using UnityEngine;

public class MusicManager : MonoBehaviour
{
    private static MusicManager instance;
    [SerializeField] AudioSource audioSource;

    void Awake()
    {
        // Cegah duplikat
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Setup AudioSource
        audioSource.loop = true;
        audioSource.playOnAwake = true;
    }
}
