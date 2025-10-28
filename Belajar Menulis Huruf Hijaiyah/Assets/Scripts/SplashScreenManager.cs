using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class SplashScreenManager : MonoBehaviour
{
    public VideoPlayer videoPlayer; // Drag Video Player di Inspector
    public string nextSceneName = "MainMenu"; // Scene setelah splash
    public float preDelay = 0.1f; // tunggu sebelum video mulai
    public float postDelay = 0.1f; // tunggu setelah video selesai

    void Start()
    {
        StartCoroutine(PlayVideoWithDelay());
    }

    IEnumerator PlayVideoWithDelay()
    {
        // Tunggu sebelum mulai video
        yield return new WaitForSeconds(preDelay);

        if (videoPlayer != null)
        {
            videoPlayer.Play();
            // Tunggu sampai video selesai
            while (videoPlayer.isPlaying)
            {
                yield return null;
            }
            // Tambahkan 1 detik delay sebelum pindah scene
            yield return new WaitForSeconds(postDelay);
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            // Jika tidak ada video, langsung delay total
            yield return new WaitForSeconds(preDelay + postDelay);
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
